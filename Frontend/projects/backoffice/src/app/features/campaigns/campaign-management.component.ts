import { Component, DestroyRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Observable, Subscription, firstValueFrom } from 'rxjs';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { CheckboxModule } from 'primeng/checkbox';
import { TagModule } from 'primeng/tag';
import { MessageModule } from 'primeng/message';
import { ConfirmationService, MessageService } from 'primeng/api';

import {
  AuthService,
  CampaignSummary,
  Client,
  CreateCampaignRequest,
  RealtimeService,
  SpendingPropensity,
  TransactionCategory,
  campaignStatusLabel,
  campaignStatusSeverity,
  describeApiError,
  formatDateForExport,
  getSpendingPropensityOptions,
  getTransactionCategoryOptions,
  sanitizeCsvField,
  spendingPropensityLabel,
  toUtcDateOnlyIso,
  transactionCategoryLabel
} from '@finara/shared-ui';

@Component({
    selector: 'app-campaign-management',
    imports: [
        DatePipe,
        FormsModule,
        TableModule,
        ButtonModule,
        DialogModule,
        InputTextModule,
        TextareaModule,
        DatePickerModule,
        SelectModule,
        MultiSelectModule,
        CheckboxModule,
        TagModule,
        MessageModule,
        TranslatePipe
    ],
    templateUrl: './campaign-management.component.html'
})
export class CampaignManagementComponent implements OnInit, OnDestroy {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly realtimeService = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly authService = inject(AuthService);
  protected readonly translate = inject(TranslateService);

  protected campaigns = signal<CampaignSummary[]>([]);
  protected loading = signal(false);
  // Aynı anda tek bir kampanya üzerinde işlem yapılır; o kampanyanın tüm aksiyon
  // butonları bu id ile spinner gösterip devre dışı kalır.
  protected busyId = signal<string | null>(null);

  protected readonly activeCount = computed(() => this.campaigns().filter(c => c.status === 'Active').length);
  protected readonly draftCount = computed(() => this.campaigns().filter(c => c.status === 'Draft').length);

  private readonly langTick = signal(0);
  private readonly langChangeSub: Subscription;

  protected readonly categories = computed(() => {
    this.langTick();
    return getTransactionCategoryOptions();
  });
  protected readonly propensities = computed(() => {
    this.langTick();
    return getSpendingPropensityOptions();
  });

  protected dialogVisible = signal(false);
  protected createBusy = signal(false);
  protected createError = signal<string | null>(null);
  protected createSubmitted = false;
  protected newCampaign = this.emptyCampaign();

  constructor() {
    this.langChangeSub = this.translate.onLangChange.subscribe(() => this.langTick.update(n => n + 1));
  }

  async ngOnInit(): Promise<void> {
    await this.loadCampaigns();

    // Başka bir sekmede/kullanıcı tarafından bir kampanyanın durumu değiştirilince
    // listeyi elle yenilemeye gerek kalmadan burada da görünsün diye.
    this.realtimeService.campaignUpdated$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(campaign => {
      this.messageService.add({
        severity: 'info',
        summary: this.translate.instant('campaigns.toast.campaignUpdated'),
        detail: this.translate.instant('campaigns.toast.campaignUpdatedDetail', {
          name: campaign.name,
          status: campaignStatusLabel(campaign.status ?? '')
        })
      });
      void this.loadCampaigns();
    });
  }

  ngOnDestroy(): void {
    this.langChangeSub.unsubscribe();
  }

  async loadCampaigns(): Promise<void> {
    this.loading.set(true);
    try {
      this.campaigns.set(await firstValueFrom(this.client.campaignsAll()));
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('campaigns.toast.loadFailed'), detail: describeApiError(err) });
    } finally {
      this.loading.set(false);
    }
  }

  private emptyCampaign() {
    return {
      name: '',
      description: '',
      startDateUtc: null as Date | null,
      endDateUtc: null as Date | null,
      targetCategory: null as TransactionCategory | null,
      targetPropensities: [] as SpendingPropensity[],
      targetsInvestmentAccountHolders: false,
      targetsInternationalTransferUsers: false
    };
  }

  openCreateDialog(): void {
    this.newCampaign = this.emptyCampaign();
    this.createError.set(null);
    this.createSubmitted = false;
    this.dialogVisible.set(true);
  }

  async createCampaign(): Promise<void> {
    this.createError.set(null);
    this.createSubmitted = true;
    if (!this.newCampaign.name || !this.newCampaign.description || !this.newCampaign.startDateUtc) {
      return;
    }

    this.createBusy.set(true);
    try {
      await firstValueFrom(
        this.client.campaigns(
          new CreateCampaignRequest({
            name: this.newCampaign.name,
            description: this.newCampaign.description,
            startDateUtc: new Date(toUtcDateOnlyIso(this.newCampaign.startDateUtc)),
            endDateUtc: this.newCampaign.endDateUtc ? new Date(toUtcDateOnlyIso(this.newCampaign.endDateUtc)) : undefined,
            targetCategory: this.newCampaign.targetCategory ?? undefined,
            targetPropensities: this.newCampaign.targetPropensities.length ? this.newCampaign.targetPropensities : undefined,
            targetsInvestmentAccountHolders: this.newCampaign.targetsInvestmentAccountHolders,
            targetsInternationalTransferUsers: this.newCampaign.targetsInternationalTransferUsers
          })
        )
      );
      this.dialogVisible.set(false);
      this.messageService.add({ severity: 'success', summary: this.translate.instant('campaigns.toast.created'), detail: this.newCampaign.name });
      await this.loadCampaigns();
    } catch (err) {
      this.createError.set(describeApiError(err));
    } finally {
      this.createBusy.set(false);
    }
  }

  // --- Yaşam döngüsü geçişleri ---

  activate(campaign: CampaignSummary): void {
    this.runAction(campaign, this.client.activate(campaign.id!), this.translate.instant('campaigns.toast.activated'));
  }

  pause(campaign: CampaignSummary): void {
    this.runAction(campaign, this.client.pause(campaign.id!), this.translate.instant('campaigns.toast.paused'));
  }

  // Tamamlama ve arşivleme geri alınamaz durum geçişleri — onay dialogu zorunlu
  // (aktifleştir/duraklat geri döndürülebilir olduğu için onay istemiyor).
  confirmComplete(campaign: CampaignSummary): void {
    this.confirmationService.confirm({
      header: this.translate.instant('campaigns.confirm.completeHeader'),
      message: this.translate.instant('campaigns.confirm.completeMessage', { name: campaign.name }),
      icon: 'pi pi-check-circle',
      acceptLabel: this.translate.instant('campaigns.confirm.completeAccept'),
      rejectLabel: this.translate.instant('common.cancel'),
      accept: () => this.runAction(campaign, this.client.complete(campaign.id!), this.translate.instant('campaigns.toast.completed'))
    });
  }

  confirmArchive(campaign: CampaignSummary): void {
    this.confirmationService.confirm({
      header: this.translate.instant('campaigns.confirm.archiveHeader'),
      message: this.translate.instant('campaigns.confirm.archiveMessage', { name: campaign.name }),
      icon: 'pi pi-inbox',
      acceptLabel: this.translate.instant('campaigns.confirm.archiveAccept'),
      rejectLabel: this.translate.instant('common.cancel'),
      accept: () => this.runAction(campaign, this.client.archive(campaign.id!), this.translate.instant('campaigns.toast.archived'))
    });
  }

  private async runAction(campaign: CampaignSummary, action$: Observable<CampaignSummary>, successSummary: string): Promise<void> {
    this.busyId.set(campaign.id ?? null);
    try {
      await firstValueFrom(action$);
      this.messageService.add({ severity: 'success', summary: successSummary, detail: campaign.name });
      await this.loadCampaigns();
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('campaigns.toast.actionFailed'), detail: describeApiError(err) });
    } finally {
      this.busyId.set(null);
    }
  }

  protected readonly statusSeverity = campaignStatusSeverity;
  protected readonly statusLabel = campaignStatusLabel;
  protected readonly categoryLabel = transactionCategoryLabel;
  protected readonly propensityLabel = spendingPropensityLabel;

  // p-table'ın yerleşik exportCSV()'i için — enum değerleri ham İngilizce yerine
  // Türkçe etikete çevriliyor (bkz. client/campaign-list.component.ts'teki aynı desen).
  // categories/propensities computed'larındaki aynı langTick deseni — düz dizi olsaydı
  // dil değişince başlıklar eski dilde donup kalırdı (denetimde bulundu).
  protected readonly exportColumns = computed(() => {
    this.langTick();
    return [
      { field: 'name', header: this.translate.instant('campaigns.table.name') },
      { field: 'status', header: this.translate.instant('campaigns.table.status') },
      { field: 'targetCategory', header: this.translate.instant('campaigns.table.targetCategory') },
      { field: 'targetPropensities', header: this.translate.instant('campaigns.table.targetPropensities') },
      { field: 'startDateUtc', header: this.translate.instant('campaigns.table.start') },
      { field: 'endDateUtc', header: this.translate.instant('campaigns.table.end') }
    ];
  });

  protected exportFn = (event: { data: unknown; field: string }): string => {
    if (event.data == null) return '';
    switch (event.field) {
      case 'status':
        return campaignStatusLabel(event.data as string);
      case 'targetCategory':
        return transactionCategoryLabel(event.data as string);
      case 'targetPropensities':
        return (event.data as string[]).map(spendingPropensityLabel).join(' / ');
      case 'startDateUtc':
      case 'endDateUtc':
        return formatDateForExport(event.data as string, this.translate.currentLang());
      default:
        return sanitizeCsvField(String(event.data));
    }
  };
}
