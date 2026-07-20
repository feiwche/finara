import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { TagModule } from 'primeng/tag';
import { MessageModule } from 'primeng/message';
import { ConfirmationService, MessageService } from 'primeng/api';

import {
  AdminUserSummary,
  AuthService,
  Client,
  CreateAdminRequest,
  describeApiError,
  userRoleLabel
} from '@finara/shared-ui';

@Component({
  selector: 'app-admin-accounts',
  standalone: true,
  imports: [DatePipe, FormsModule, TableModule, ButtonModule, DialogModule, InputTextModule, PasswordModule, TagModule, MessageModule],
  templateUrl: './admin-accounts.component.html'
})
export class AdminAccountsComponent implements OnInit {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  protected readonly authService = inject(AuthService);

  protected admins = signal<AdminUserSummary[]>([]);
  protected loading = signal(false);
  protected busyId = signal<string | null>(null);

  protected dialogVisible = signal(false);
  protected createBusy = signal(false);
  protected createError = signal<string | null>(null);
  protected createSubmitted = false;
  protected newAdmin = { email: '', password: '' };

  async ngOnInit(): Promise<void> {
    await this.loadAdmins();
  }

  async loadAdmins(): Promise<void> {
    this.loading.set(true);
    try {
      this.admins.set(await firstValueFrom(this.client.adminUsersAll()));
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Yönetici hesapları yüklenemedi', detail: describeApiError(err) });
    } finally {
      this.loading.set(false);
    }
  }

  openCreateDialog(): void {
    this.newAdmin = { email: '', password: '' };
    this.createError.set(null);
    this.createSubmitted = false;
    this.dialogVisible.set(true);
  }

  async createAdmin(): Promise<void> {
    this.createError.set(null);
    this.createSubmitted = true;
    if (!this.newAdmin.email || !this.newAdmin.password) return;

    this.createBusy.set(true);
    try {
      await firstValueFrom(
        this.client.adminUsers(new CreateAdminRequest({ email: this.newAdmin.email, password: this.newAdmin.password }))
      );
      this.dialogVisible.set(false);
      this.messageService.add({ severity: 'success', summary: 'Yönetici hesabı oluşturuldu', detail: this.newAdmin.email });
      await this.loadAdmins();
    } catch (err) {
      this.createError.set(describeApiError(err));
    } finally {
      this.createBusy.set(false);
    }
  }

  confirmDeactivate(admin: AdminUserSummary): void {
    this.confirmationService.confirm({
      header: 'Yönetici hesabını pasifleştir',
      message: `"${admin.email}" hesabını pasifleştirmek istediğinize emin misiniz? Bu hesap artık giriş yapamayacak.`,
      icon: 'pi pi-user-minus',
      acceptLabel: 'Pasifleştir',
      rejectLabel: 'Vazgeç',
      accept: () => this.deactivate(admin)
    });
  }

  // Backend de kendi kendini pasifleştirmeyi reddediyor (400) — burada butonu baştan
  // gizlemek sadece kullanıcı deneyimini iyileştiriyor, tek güvenlik katmanı bu değil.
  isSelf(admin: AdminUserSummary): boolean {
    return admin.email === this.authService.email();
  }

  protected readonly roleLabel = userRoleLabel;

  private async deactivate(admin: AdminUserSummary): Promise<void> {
    this.busyId.set(admin.id ?? null);
    try {
      await firstValueFrom(this.client.deactivate(admin.id!));
      this.messageService.add({ severity: 'success', summary: 'Hesap pasifleştirildi', detail: admin.email });
      await this.loadAdmins();
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'İşlem başarısız', detail: describeApiError(err) });
    } finally {
      this.busyId.set(null);
    }
  }
}
