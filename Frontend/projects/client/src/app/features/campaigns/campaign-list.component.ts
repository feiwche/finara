import { Component, OnInit, inject, signal } from '@angular/core';
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

import { CampaignSummary, Client, CreateCampaignRequest, SpendingPropensity, TransactionCategory } from '../../api/finara-client';
import { describeApiError } from '../../core/api-error.util';
import { toUtcDateOnlyIso } from '../../core/date.util';
import { campaignStatusSeverity } from '../../core/severity.util';

@Component({
  selector: 'app-campaign-list',
  standalone: true,
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

  protected campaigns = signal<CampaignSummary[]>([]);
  protected loading = signal(false);
  protected activatingId = signal<string | null>(null);

  protected categories = Object.values(TransactionCategory);
  protected propensities = Object.values(SpendingPropensity);

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
}
