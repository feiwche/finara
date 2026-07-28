import { Component, DestroyRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription, firstValueFrom } from 'rxjs';

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
import { MessageService } from 'primeng/api';

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
  getSpendingPropensityOptions,
  getTransactionCategoryOptions,
  sanitizeCsvField,
  spendingPropensityLabel,
  toUtcDateOnlyIso,
  transactionCategoryLabel
} from '@finara/shared-ui';

@Component({
    selector: 'app-campaign-list',
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
    templateUrl: './campaign-list.component.html'
})
export class CampaignListComponent implements OnInit, OnDestroy {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);
  private readonly realtimeService = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly authService = inject(AuthService);
  protected readonly translate = inject(TranslateService);

  protected campaigns = signal<CampaignSummary[]>([]);
  protected loading = signal(false);
  protected activatingId = signal<string | null>(null);

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
  protected newCampaign = {
    name: '',
    description: '',
    startDateUtc: null as Date | null,
    endDateUtc: null as Date | null,
    targetCategory: null as TransactionCategory | null,
    targetPropensities: [] as SpendingPropensity[],
    targetsInvestmentAccountHolders: false,
    targetsInternationalTransferUsers: false
  };

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
        summary: this.translate.instant('campaigns.toast.updated'),
        detail: this.translate.instant('campaigns.toast.updatedDetail', { name: campaign.name, status: campaignStatusLabel(campaign.status ?? '') })
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

  openCreateDialog(): void {
    this.newCampaign = {
      name: '',
      description: '',
      startDateUtc: null,
      endDateUtc: null,
      targetCategory: null,
      targetPropensities: [],
      targetsInvestmentAccountHolders: false,
      targetsInternationalTransferUsers: false
    };
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

  async activateCampaign(campaign: CampaignSummary): Promise<void> {
    this.activatingId.set(campaign.id ?? null);
    try {
      await firstValueFrom(this.client.activate(campaign.id!));
      this.messageService.add({ severity: 'success', summary: this.translate.instant('campaigns.toast.activated'), detail: campaign.name });
      await this.loadCampaigns();
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('campaigns.toast.activateFailed'), detail: describeApiError(err) });
    } finally {
      this.activatingId.set(null);
    }
  }

  protected readonly statusSeverity = campaignStatusSeverity;
  protected readonly statusLabel = campaignStatusLabel;
  protected readonly categoryLabel = transactionCategoryLabel;
  protected readonly propensityLabel = spendingPropensityLabel;

  // p-table'ın yerleşik exportCSV()'i için — enum değerleri ham İngilizce yerine
  // Türkçe etikete çevriliyor (bkz. customer-list.component.ts'teki aynı desen).
  protected readonly exportColumns = computed(() => {
    this.langTick();
    return [
      { field: 'name', header: this.translate.instant('campaigns.exportColumns.name') },
      { field: 'status', header: this.translate.instant('campaigns.exportColumns.status') },
      { field: 'targetCategory', header: this.translate.instant('campaigns.exportColumns.targetCategory') },
      { field: 'targetPropensities', header: this.translate.instant('campaigns.exportColumns.targetPropensities') },
      { field: 'startDateUtc', header: this.translate.instant('campaigns.exportColumns.start') },
      { field: 'endDateUtc', header: this.translate.instant('campaigns.exportColumns.end') }
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
        return new Date(event.data as string).toLocaleDateString('tr-TR');
      default:
        return sanitizeCsvField(String(event.data));
    }
  };
}
