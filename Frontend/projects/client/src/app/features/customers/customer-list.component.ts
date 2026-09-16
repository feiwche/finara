import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription, firstValueFrom } from 'rxjs';

import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { DatePickerModule } from 'primeng/datepicker';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageModule } from 'primeng/message';
import { TagModule } from 'primeng/tag';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import {
  AuthService,
  Client,
  CreateCustomerRequest,
  CustomerImportResult,
  CustomerStats,
  CustomerSummary,
  describeApiError,
  educationLevelLabel,
  EducationLevel,
  getEducationLevelOptions,
  getUiLanguageOptions,
  propensitySeverity,
  sanitizeCsvField,
  spendingPropensityLabel,
  toUtcDateOnlyIso,
  UiLanguage
} from '@finara/shared-ui';

@Component({
    selector: 'app-customer-list',
    imports: [
        FormsModule,
        TableModule,
        ButtonModule,
        DialogModule,
        InputTextModule,
        DatePickerModule,
        CheckboxModule,
        MessageModule,
        TagModule,
        IconFieldModule,
        InputIconModule,
        SelectModule,
        TooltipModule,
        TranslatePipe
    ],
    templateUrl: './customer-list.component.html'
})
export class CustomerListComponent implements OnInit, OnDestroy {
  private readonly client = inject(Client);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  protected readonly authService = inject(AuthService);
  protected readonly translate = inject(TranslateService);

  protected readonly propensitySeverity = propensitySeverity;
  protected readonly spendingPropensityLabel = spendingPropensityLabel;

  private readonly langTick = signal(0);
  private readonly langChangeSub: Subscription;

  protected readonly educationLevelOptions = computed(() => {
    this.langTick();
    return getEducationLevelOptions();
  });

  protected readonly uiLanguageOptions = computed(() => {
    this.langTick();
    return getUiLanguageOptions();
  });

  protected readonly pageSize = 20;

  protected customers = signal<CustomerSummary[]>([]);
  protected totalRecords = signal(0);
  protected loading = signal(false);
  protected searchTerm = '';

  // KPI şeridi artık sunucu taraflı sayfalanan tabloya (tek sayfa) bakarak
  // hesaplanamaz — ayrı, tüm (bank-scoped) müşteriler üzerinden hesaplanan
  // hafif bir uçtan (bkz. GET /api/customers/stats) geliyor.
  protected stats = signal<CustomerStats | null>(null);

  // p-table'a [first]="tableFirst()" ile geri bağlanıyor — aksi halde arama/filtre
  // sıfırlaması sadece VERİYİ 1. sayfaya döndürüyordu, paginator'ın kendi iç "hangi
  // sayfa seçili" durumu (bulundu: p-table'ın kendi state'i, bizim lastLazyEvent'ten
  // tamamen bağımsız) eski sayfada görünmeye devam ediyordu.
  protected tableFirst = signal(0);

  private lastLazyEvent: TableLazyLoadEvent = { first: 0, rows: this.pageSize };
  private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;
  // Debounce'lu arama ile paginator'ın (onLazyLoad) tetiklediği istek yarışabiliyordu —
  // kullanıcı yazarken paginator'da farklı bir sayfaya tıklarsa, 300ms sonra bekleyen
  // arama isteği geç dönüp az önceki manuel sayfa değişikliğini sessizce ezebiliyordu.
  // Her istek artan bir sıra numarası taşıyor, sadece EN SON başlatılan isteğin yanıtı
  // uygulanıyor (daha önce dönen ama artık "bayat" olan bir yanıt sessizce yok sayılıyor).
  private requestSequence = 0;

  protected initials(customer: CustomerSummary): string {
    return `${customer.firstName?.[0] ?? ''}${customer.lastName?.[0] ?? ''}`.toUpperCase();
  }

  // p-table'ın yerleşik exportCSV()'i — [columns] gerçek CustomerSummary alanlarına
  // işaret ediyor, [exportFunction] enum değerlerini ham İngilizce yerine Türkçe
  // etikete çeviriyor (bkz. UI Türkçe kuralı — dışa aktarılan dosya da istisna değil).
  protected readonly exportColumns = computed(() => {
    this.langTick();
    return [
      { field: 'firstName', header: this.translate.instant('customers.export.firstName') },
      { field: 'lastName', header: this.translate.instant('customers.export.lastName') },
      { field: 'email', header: this.translate.instant('customers.export.email') },
      { field: 'phoneNumber', header: this.translate.instant('customers.export.phone') },
      { field: 'age', header: this.translate.instant('customers.export.age') },
      { field: 'propensity', header: this.translate.instant('customers.export.propensity') },
      { field: 'educationLevel', header: this.translate.instant('customers.export.educationLevel') }
    ];
  });

  protected exportFn = (event: { data: unknown; field: string }): string => {
    if (event.data == null) return '';
    switch (event.field) {
      case 'propensity':
        return spendingPropensityLabel(event.data as string);
      case 'educationLevel':
        return educationLevelLabel(event.data as string);
      default:
        return sanitizeCsvField(String(event.data));
    }
  };

  protected dialogVisible = signal(false);
  protected createBusy = signal(false);
  protected createError = signal<string | null>(null);
  // Dialog açılır açılmaz kırmızı alan hatalarıyla karşılaşılmasın diye, alan bazlı
  // validasyon mesajları sadece bir kez "Oluştur"a basıldıktan sonra gösteriliyor.
  protected createSubmitted = false;
  protected newCustomer = {
    firstName: '',
    lastName: '',
    email: '',
    phoneNumber: '',
    dateOfBirth: null as Date | null,
    hasInvestmentAccount: false,
    hasInternationalTransfers: false,
    educationLevel: null as EducationLevel | null,
    preferredLanguage: UiLanguage.Turkish
  };

  constructor() {
    this.langChangeSub = this.translate.onLangChange.subscribe(() => this.langTick.update(n => n + 1));
  }

  async ngOnInit(): Promise<void> {
    // Tablo satırlarını yüklemek burada YAPILMIYOR — [lazy]="true" + p-table, ilk
    // render'da kendiliğinden (onLazyLoad) tetikliyor. Burada sadece tablodan bağımsız
    // KPI şeridi yükleniyor.
    await this.loadStats();
  }

  async loadStats(): Promise<void> {
    try {
      this.stats.set(await firstValueFrom(this.client.stats()));
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('customers.toast.statsLoadFailed'), detail: describeApiError(err) });
    }
  }

  async loadCustomers(event?: TableLazyLoadEvent): Promise<void> {
    if (event) this.lastLazyEvent = event;
    this.tableFirst.set(this.lastLazyEvent.first ?? 0);

    const requestId = ++this.requestSequence;
    this.loading.set(true);
    try {
      const first = this.lastLazyEvent.first ?? 0;
      const page = Math.floor(first / this.pageSize) + 1;
      const result = await firstValueFrom(this.client.paged2(page, this.pageSize, this.searchTerm || undefined));
      // Bu istek beklerken daha yeni bir istek (arama/sayfalama) başlatıldıysa, bu yanıt
      // artık bayat — uygulanırsa kullanıcının en son gördüğü durumu geriye sarardı.
      if (requestId !== this.requestSequence) return;
      this.customers.set(result.items ?? []);
      this.totalRecords.set(result.totalCount ?? 0);
    } catch (err) {
      if (requestId !== this.requestSequence) return;
      this.messageService.add({ severity: 'error', summary: this.translate.instant('customers.toast.loadFailed'), detail: describeApiError(err) });
    } finally {
      if (requestId === this.requestSequence) this.loading.set(false);
    }
  }

  // Arama kutusu artık sunucu taraflı — her tuş vuruşunda istek atmamak için debounce'lu,
  // aramaya başlarken sayfa 1'e sıfırlanıyor (aksi halde ör. 3. sayfadayken arama yapılırsa
  // "sonuç yok" gibi görünürdü, o sayfada eski/eşleşmeyen veri kalırdı).
  onSearchInput(): void {
    if (this.searchDebounceHandle) clearTimeout(this.searchDebounceHandle);
    this.searchDebounceHandle = setTimeout(() => {
      this.searchDebounceHandle = null;
      this.resetToFirstPageAndReload();
    }, 300);
  }

  ngOnDestroy(): void {
    if (this.searchDebounceHandle) clearTimeout(this.searchDebounceHandle);
    this.langChangeSub.unsubscribe();
  }

  private resetToFirstPageAndReload(): void {
    this.lastLazyEvent = { ...this.lastLazyEvent, first: 0 };
    void this.loadCustomers();
  }

  // Yeni müşteri oluşturma/CSV içe aktarma sonrası da 1. sayfaya dönülüyor — aksi halde
  // kullanıcı ör. 3. sayfadaysa "başarılı" mesajını görür ama eklediği kaydı (sıralamaya
  // göre başka bir sayfaya düşmüşse) ekranda hiç göremezdi.
  private async reload(): Promise<void> {
    this.lastLazyEvent = { ...this.lastLazyEvent, first: 0 };
    await Promise.all([this.loadCustomers(), this.loadStats()]);
  }

  openCreateDialog(): void {
    this.newCustomer = {
      firstName: '',
      lastName: '',
      email: '',
      phoneNumber: '',
      dateOfBirth: null,
      hasInvestmentAccount: false,
      hasInternationalTransfers: false,
      educationLevel: null,
      preferredLanguage: UiLanguage.Turkish
    };
    this.createError.set(null);
    this.createSubmitted = false;
    this.dialogVisible.set(true);
  }

  async createCustomer(): Promise<void> {
    this.createError.set(null);
    this.createSubmitted = true;
    if (!this.newCustomer.firstName || !this.newCustomer.lastName || !this.newCustomer.email || !this.newCustomer.dateOfBirth) {
      return;
    }

    this.createBusy.set(true);
    try {
      await firstValueFrom(
        this.client.customersPOST(
          new CreateCustomerRequest({
            firstName: this.newCustomer.firstName,
            lastName: this.newCustomer.lastName,
            email: this.newCustomer.email,
            phoneNumber: this.newCustomer.phoneNumber || undefined,
            dateOfBirth: new Date(toUtcDateOnlyIso(this.newCustomer.dateOfBirth)),
            hasInvestmentAccount: this.newCustomer.hasInvestmentAccount,
            hasInternationalTransfers: this.newCustomer.hasInternationalTransfers,
            educationLevel: this.newCustomer.educationLevel ?? undefined,
            preferredLanguage: this.newCustomer.preferredLanguage
          })
        )
      );
      this.dialogVisible.set(false);
      this.messageService.add({ severity: 'success', summary: this.translate.instant('customers.toast.created'), detail: `${this.newCustomer.firstName} ${this.newCustomer.lastName}` });
      await this.reload();
    } catch (err) {
      this.createError.set(describeApiError(err));
    } finally {
      this.createBusy.set(false);
    }
  }

  openDetail(customer: CustomerSummary): void {
    this.router.navigate(['/customers', customer.id]);
  }

  navigateToTools(): void {
    this.router.navigate(['/tools']);
  }

  protected importBusy = signal(false);
  protected importResultDialogVisible = signal(false);
  protected importResult = signal<CustomerImportResult | null>(null);

  async onImportFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    this.importBusy.set(true);
    try {
      const result = await firstValueFrom(this.client.import({ data: file, fileName: file.name }));
      this.importResult.set(result);
      this.importResultDialogVisible.set(true);
      if ((result.successCount ?? 0) > 0) {
        await this.reload();
      }
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('customers.toast.importFailed'), detail: describeApiError(err) });
    } finally {
      this.importBusy.set(false);
    }
  }

  downloadImportTemplate(): void {
    // BİLEREK Türkçe ve SABİT — backend'in CSV parser'ı (CustomerService.ImportCustomersAsync,
    // NormalizeHeader) sütun adlarını yalnızca bu Türkçe kelimelerle (AD/SOYAD/EPOSTA/...)
    // eşleştiriyor, arayüz dilinden bağımsız bir teknik sözleşme. Önceden bu satır
    // this.translate.instant(...) ile başlıkları çeviriyordu — yani İngilizce/Almanca arayüzde
    // indirilen şablon ("First Name,Last Name,...") geri yüklendiğinde backend "zorunlu sütun
    // eksik" hatası verirdi (bulundu, örnek veri de İngilizce/Almanca'ya çevrilmiş ama sütun
    // ADLARI çevrilmemiş olsaydı bile aynı sorun oluşurdu). Kullanıcıya gösterilen sütun
    // AÇIKLAMALARI (customers.importTemplate.* — dosya adı hariç) hâlâ çevrili, sadece CSV'nin
    // kendi başlık satırı sabit Türkçe.
    const header = 'Ad,Soyad,E-posta,Doğum Tarihi,Telefon,Eğitim Durumu,Yatırım Hesabı Var,Yurtdışı Transfer Var,Dil';
    // ParseBool de aynı şekilde sadece "evet/var/true/1" kabul ediyor (backend) — "Yes"/"No"
    // gibi çevrilmiş bir örnek değer de aynı sınıftan bir hataya yol açardı, o yüzden bu da sabit.
    const example = 'Ayşe,Yılmaz,ayse.yilmaz@example.com,1990-05-12,+905551234567,Lisans,Evet,Hayır,Türkçe';
    const blob = new Blob([`${header}\n${example}\n`], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'musteri-ice-aktarma-sablonu.csv';
    link.click();
    URL.revokeObjectURL(url);
  }
}
