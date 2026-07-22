import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { TableModule } from 'primeng/table';
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
import { MessageService } from 'primeng/api';

import {
  Client,
  CreateCustomerRequest,
  CustomerSummary,
  describeApiError,
  educationLevelLabel,
  EDUCATION_LEVEL_OPTIONS,
  EducationLevel,
  propensitySeverity,
  spendingPropensityLabel,
  toUtcDateOnlyIso
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
        SelectModule
    ],
    templateUrl: './customer-list.component.html'
})
export class CustomerListComponent implements OnInit {
  private readonly client = inject(Client);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly propensitySeverity = propensitySeverity;
  protected readonly spendingPropensityLabel = spendingPropensityLabel;
  protected readonly educationLevelOptions = EDUCATION_LEVEL_OPTIONS;

  protected customers = signal<CustomerSummary[]>([]);
  protected loading = signal(false);
  protected searchTerm = '';

  protected readonly highlyResponsiveCount = computed(
    () => this.customers().filter(c => c.propensity === 'HighlyResponsive').length
  );
  protected readonly needsEngagementCount = computed(
    () => this.customers().filter(c => c.propensity === 'NeedsEngagement').length
  );

  protected initials(customer: CustomerSummary): string {
    return `${customer.firstName?.[0] ?? ''}${customer.lastName?.[0] ?? ''}`.toUpperCase();
  }

  // p-table'ın yerleşik exportCSV()'i — [columns] gerçek CustomerSummary alanlarına
  // işaret ediyor, [exportFunction] enum değerlerini ham İngilizce yerine Türkçe
  // etikete çeviriyor (bkz. UI Türkçe kuralı — dışa aktarılan dosya da istisna değil).
  protected readonly exportColumns = [
    { field: 'firstName', header: 'Ad' },
    { field: 'lastName', header: 'Soyad' },
    { field: 'email', header: 'E-posta' },
    { field: 'phoneNumber', header: 'Telefon' },
    { field: 'age', header: 'Yaş' },
    { field: 'propensity', header: 'Eğilim' },
    { field: 'educationLevel', header: 'Eğitim Durumu' }
  ];

  protected exportFn = (event: { data: unknown; field: string }): string => {
    if (event.data == null) return '';
    switch (event.field) {
      case 'propensity':
        return spendingPropensityLabel(event.data as string);
      case 'educationLevel':
        return educationLevelLabel(event.data as string);
      default:
        return String(event.data);
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
    educationLevel: null as EducationLevel | null
  };

  async ngOnInit(): Promise<void> {
    await this.loadCustomers();
  }

  async loadCustomers(): Promise<void> {
    this.loading.set(true);
    try {
      this.customers.set(await firstValueFrom(this.client.customersAll()));
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Müşteriler yüklenemedi', detail: describeApiError(err) });
    } finally {
      this.loading.set(false);
    }
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
      educationLevel: null
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
        this.client.customers(
          new CreateCustomerRequest({
            firstName: this.newCustomer.firstName,
            lastName: this.newCustomer.lastName,
            email: this.newCustomer.email,
            phoneNumber: this.newCustomer.phoneNumber || undefined,
            dateOfBirth: new Date(toUtcDateOnlyIso(this.newCustomer.dateOfBirth)),
            hasInvestmentAccount: this.newCustomer.hasInvestmentAccount,
            hasInternationalTransfers: this.newCustomer.hasInternationalTransfers,
            educationLevel: this.newCustomer.educationLevel ?? undefined
          })
        )
      );
      this.dialogVisible.set(false);
      this.messageService.add({ severity: 'success', summary: 'Müşteri oluşturuldu', detail: `${this.newCustomer.firstName} ${this.newCustomer.lastName}` });
      await this.loadCustomers();
    } catch (err) {
      this.createError.set(describeApiError(err));
    } finally {
      this.createBusy.set(false);
    }
  }

  openDetail(customer: CustomerSummary): void {
    this.router.navigate(['/customers', customer.id]);
  }
}
