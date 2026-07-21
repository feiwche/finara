import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { MessageModule } from 'primeng/message';
import { ConfirmationService, MessageService } from 'primeng/api';

import {
  BankSummary,
  Client,
  CreateBankRequest,
  bankDataProviderTypeLabel,
  describeApiError
} from '@finara/shared-ui';

@Component({
    selector: 'app-bank-management',
    imports: [FormsModule, TableModule, ButtonModule, DialogModule, InputTextModule, TagModule, MessageModule],
    templateUrl: './bank-management.component.html'
})
export class BankManagementComponent implements OnInit {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected banks = signal<BankSummary[]>([]);
  protected loading = signal(false);
  protected busyId = signal<string | null>(null);

  protected dialogVisible = signal(false);
  protected createBusy = signal(false);
  protected createError = signal<string | null>(null);
  protected createSubmitted = false;
  protected newBank = this.emptyNewBank();

  protected readonly dataProviderTypeLabel = bankDataProviderTypeLabel;

  async ngOnInit(): Promise<void> {
    await this.loadBanks();
  }

  async loadBanks(): Promise<void> {
    this.loading.set(true);
    try {
      this.banks.set(await firstValueFrom(this.client.banksAll()));
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Banka listesi yüklenemedi', detail: describeApiError(err) });
    } finally {
      this.loading.set(false);
    }
  }

  private emptyNewBank() {
    return { name: '', code: '' };
  }

  openCreateDialog(): void {
    this.newBank = this.emptyNewBank();
    this.createError.set(null);
    this.createSubmitted = false;
    this.dialogVisible.set(true);
  }

  async createBank(): Promise<void> {
    this.createError.set(null);
    this.createSubmitted = true;
    if (!this.newBank.name || !this.newBank.code) return;

    this.createBusy.set(true);
    try {
      await firstValueFrom(
        this.client.banks(new CreateBankRequest({ name: this.newBank.name, code: this.newBank.code }))
      );
      this.dialogVisible.set(false);
      this.messageService.add({ severity: 'success', summary: 'Banka oluşturuldu', detail: this.newBank.name });
      await this.loadBanks();
    } catch (err) {
      this.createError.set(describeApiError(err));
    } finally {
      this.createBusy.set(false);
    }
  }

  confirmDeactivate(bank: BankSummary): void {
    this.confirmationService.confirm({
      header: 'Bankayı pasifleştir',
      message: `"${bank.name}" bankasını pasifleştirmek istediğinize emin misiniz? Bu geri alınamaz.`,
      icon: 'pi pi-building',
      acceptLabel: 'Pasifleştir',
      rejectLabel: 'Vazgeç',
      accept: () => this.deactivate(bank)
    });
  }

  private async deactivate(bank: BankSummary): Promise<void> {
    this.busyId.set(bank.id ?? null);
    try {
      await firstValueFrom(this.client.deactivate2(bank.id!));
      this.messageService.add({ severity: 'success', summary: 'Banka pasifleştirildi', detail: bank.name });
      await this.loadBanks();
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'İşlem başarısız', detail: describeApiError(err) });
    } finally {
      this.busyId.set(null);
    }
  }
}
