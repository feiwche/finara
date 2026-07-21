import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable, firstValueFrom } from 'rxjs';

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
import { ConfirmationService, MessageService } from 'primeng/api';

import {
  CampaignSummary,
  Client,
  CreateCampaignRequest,
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
        CheckboxModule,
        TagModule,
        MessageModule
    ],
    templateUrl: './campaign-management.component.html'
})
export class CampaignManagementComponent implements OnInit {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected campaigns = signal<CampaignSummary[]>([]);
  protected loading = signal(false);
  // Aynı anda tek bir kampanya üzerinde işlem yapılır; o kampanyanın tüm aksiyon
  // butonları bu id ile spinner gösterip devre dışı kalır.
  protected busyId = signal<string | null>(null);

  protected categories = TRANSACTION_CATEGORY_OPTIONS;
  protected propensities = SPENDING_PROPENSITY_OPTIONS;

  protected dialogVisible = signal(false);
  protected createBusy = signal(false);
  protected createError = signal<string | null>(null);
  protected createSubmitted = false;
  protected newCampaign = this.emptyCampaign();

  async ngOnInit(): Promise<void> {
    await this.loadCampaigns();
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

  private emptyCampaign() {
    return {
      name: '',
      description: '',
      startDateUtc: null as Date | null,
      endDateUtc: null as Date | null,
      targetCategory: null as TransactionCategory | null,
      targetPropensity: null as SpendingPropensity | null,
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

  // --- Yaşam döngüsü geçişleri ---

  activate(campaign: CampaignSummary): void {
    this.runAction(campaign, this.client.activate(campaign.id!), 'Kampanya aktifleştirildi');
  }

  pause(campaign: CampaignSummary): void {
    this.runAction(campaign, this.client.pause(campaign.id!), 'Kampanya duraklatıldı');
  }

  // Tamamlama ve arşivleme geri alınamaz durum geçişleri — onay dialogu zorunlu
  // (aktifleştir/duraklat geri döndürülebilir olduğu için onay istemiyor).
  confirmComplete(campaign: CampaignSummary): void {
    this.confirmationService.confirm({
      header: 'Kampanyayı tamamla',
      message: `"${campaign.name}" kampanyasını tamamlamak istediğinize emin misiniz? Tamamlanan bir kampanya tekrar aktifleştirilemez.`,
      icon: 'pi pi-check-circle',
      acceptLabel: 'Tamamla',
      rejectLabel: 'Vazgeç',
      accept: () => this.runAction(campaign, this.client.complete(campaign.id!), 'Kampanya tamamlandı')
    });
  }

  confirmArchive(campaign: CampaignSummary): void {
    this.confirmationService.confirm({
      header: 'Kampanyayı arşivle',
      message: `"${campaign.name}" kampanyasını arşivlemek istediğinize emin misiniz? Arşivlenen bir kampanya artık kullanılamaz.`,
      icon: 'pi pi-inbox',
      acceptLabel: 'Arşivle',
      rejectLabel: 'Vazgeç',
      accept: () => this.runAction(campaign, this.client.archive(campaign.id!), 'Kampanya arşivlendi')
    });
  }

  private async runAction(campaign: CampaignSummary, action$: Observable<CampaignSummary>, successSummary: string): Promise<void> {
    this.busyId.set(campaign.id ?? null);
    try {
      await firstValueFrom(action$);
      this.messageService.add({ severity: 'success', summary: successSummary, detail: campaign.name });
      await this.loadCampaigns();
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'İşlem başarısız', detail: describeApiError(err) });
    } finally {
      this.busyId.set(null);
    }
  }

  protected readonly statusSeverity = campaignStatusSeverity;
  protected readonly statusLabel = campaignStatusLabel;
  protected readonly categoryLabel = transactionCategoryLabel;
  protected readonly propensityLabel = spendingPropensityLabel;
}
