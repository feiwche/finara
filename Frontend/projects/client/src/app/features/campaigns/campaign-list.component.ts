import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { TagModule } from 'primeng/tag';
import { MessageModule } from 'primeng/message';
import { MessageService } from 'primeng/api';

import {
  CampaignSummary,
  Client,
  CreateCampaignRequest,
  RealtimeService,
  SPENDING_PROPENSITY_OPTIONS,
  SpendingPropensity,
  TRANSACTION_CATEGORY_OPTIONS,
  TransactionCategory,
  campaignStatusLabel,
  campaignStatusSeverity,
  describeApiError,
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
        CheckboxModule,
        TagModule,
        MessageModule
    ],
    templateUrl: './campaign-list.component.html'
})
export class CampaignListComponent implements OnInit {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);
  private readonly realtimeService = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);

  protected campaigns = signal<CampaignSummary[]>([]);
  protected loading = signal(false);
  protected activatingId = signal<string | null>(null);

  protected readonly activeCount = computed(() => this.campaigns().filter(c => c.status === 'Active').length);
  protected readonly draftCount = computed(() => this.campaigns().filter(c => c.status === 'Draft').length);

  protected categories = TRANSACTION_CATEGORY_OPTIONS;
  protected propensities = SPENDING_PROPENSITY_OPTIONS;

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
    targetPropensity: null as SpendingPropensity | null,
    targetsInvestmentAccountHolders: false,
    targetsInternationalTransferUsers: false
  };

  async ngOnInit(): Promise<void> {
    await this.loadCampaigns();

    // Başka bir sekmede/kullanıcı tarafından bir kampanyanın durumu değiştirilince
    // listeyi elle yenilemeye gerek kalmadan burada da görünsün diye.
    this.realtimeService.campaignUpdated$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(campaign => {
      this.messageService.add({
        severity: 'info',
        summary: 'Kampanya güncellendi',
        detail: `${campaign.name} artık ${campaignStatusLabel(campaign.status ?? '')}`
      });
      void this.loadCampaigns();
    });
  }

  async loadCampaigns(): Promise<void> {
    this.loading.set(true);
    try {
      this.campaigns.set(await firstValueFrom(this.client.campaignsAll()));
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Kampanyalar yüklenemedi', detail: describeApiError(err) });
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
      targetPropensity: null,
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
            targetPropensity: this.newCampaign.targetPropensity ?? undefined,
            targetsInvestmentAccountHolders: this.newCampaign.targetsInvestmentAccountHolders,
            targetsInternationalTransferUsers: this.newCampaign.targetsInternationalTransferUsers
          })
        )
      );
      this.dialogVisible.set(false);
      this.messageService.add({ severity: 'success', summary: 'Kampanya oluşturuldu', detail: this.newCampaign.name });
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
      this.messageService.add({ severity: 'success', summary: 'Kampanya aktifleştirildi', detail: campaign.name });
      await this.loadCampaigns();
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Aktifleştirilemedi', detail: describeApiError(err) });
    } finally {
      this.activatingId.set(null);
    }
  }

  protected readonly statusSeverity = campaignStatusSeverity;
  protected readonly statusLabel = campaignStatusLabel;
  protected readonly categoryLabel = transactionCategoryLabel;
  protected readonly propensityLabel = spendingPropensityLabel;
}
