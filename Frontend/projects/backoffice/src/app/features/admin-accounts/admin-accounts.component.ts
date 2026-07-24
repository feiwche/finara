import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { SelectModule } from 'primeng/select';
import { TabViewModule } from 'primeng/tabview';
import { TagModule } from 'primeng/tag';
import { MessageModule } from 'primeng/message';
import { ConfirmationService, MessageService } from 'primeng/api';

import {
  AdminUserSummary,
  AuthService,
  BankSummary,
  Client,
  CreateAdminRequest,
  CreateInvitationRequest,
  InvitationSummary,
  USER_ROLE_OPTIONS,
  UserRole,
  describeApiError,
  userRoleLabel
} from '@finara/shared-ui';

@Component({
    selector: 'app-admin-accounts',
    imports: [DatePipe, FormsModule, TableModule, ButtonModule, DialogModule, InputTextModule, PasswordModule, SelectModule, TabViewModule, TagModule, MessageModule],
    templateUrl: './admin-accounts.component.html'
})
export class AdminAccountsComponent implements OnInit {
  private readonly client = inject(Client);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  protected readonly authService = inject(AuthService);

  protected admins = signal<AdminUserSummary[]>([]);
  protected banks = signal<BankSummary[]>([]);
  protected loading = signal(false);
  protected busyId = signal<string | null>(null);

  protected readonly activeCount = computed(() => this.admins().filter(a => a.isActive).length);
  protected readonly inactiveCount = computed(() => this.admins().filter(a => !a.isActive).length);

  protected readonly roleOptions = USER_ROLE_OPTIONS;

  protected dialogVisible = signal(false);
  protected createBusy = signal(false);
  protected createError = signal<string | null>(null);
  protected createSubmitted = false;
  protected newAdmin = this.emptyNewAdmin();

  // --- Davetler (accept-invitation akışı) ---
  protected invitations = signal<InvitationSummary[]>([]);
  protected invitationsLoading = signal(false);
  protected invitationBusyId = signal<string | null>(null);
  protected inviteDialogVisible = signal(false);
  protected inviteBusy = signal(false);
  protected inviteError = signal<string | null>(null);
  protected inviteSubmitted = false;
  protected newInvitation = this.emptyNewInvitation();

  async ngOnInit(): Promise<void> {
    await this.loadAdmins();
    await this.loadInvitations();
    // Banka listesi sadece SuperAdmin'in "Yeni Yönetici"/"Yeni Davet" dialoglarında
    // BankAdmin için banka seçebilmesi amacıyla yükleniyor — BankAdmin zaten kendi
    // bankasına sabitlendiği için (backend bunu zorluyor) bu isteğe hiç ihtiyaç duymaz.
    if (this.authService.isSuperAdmin()) {
      await this.loadBanks();
    }
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

  async loadBanks(): Promise<void> {
    try {
      this.banks.set(await firstValueFrom(this.client.banksAll()));
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Banka listesi yüklenemedi', detail: describeApiError(err) });
    }
  }

  private emptyNewAdmin() {
    return { email: '', password: '', role: UserRole.BankAdmin as UserRole | null, bankId: null as string | null };
  }

  openCreateDialog(): void {
    this.newAdmin = this.emptyNewAdmin();
    this.createError.set(null);
    this.createSubmitted = false;
    this.dialogVisible.set(true);
  }

  async createAdmin(): Promise<void> {
    this.createError.set(null);
    this.createSubmitted = true;
    const needsBank = this.authService.isSuperAdmin() && this.newAdmin.role === UserRole.BankAdmin;
    if (!this.newAdmin.email || !this.newAdmin.password || (needsBank && !this.newAdmin.bankId)) return;

    this.createBusy.set(true);
    try {
      await firstValueFrom(
        this.client.adminUsers(new CreateAdminRequest({
          email: this.newAdmin.email,
          password: this.newAdmin.password,
          role: this.newAdmin.role ?? undefined,
          bankId: needsBank ? this.newAdmin.bankId! : undefined
        }))
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

  bankName(bankId: string | undefined): string {
    if (!bankId) return '—';
    return this.banks().find(b => b.id === bankId)?.name ?? bankId;
  }

  protected readonly roleLabel = userRoleLabel;
  protected readonly UserRole = UserRole;

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

  // --- Davetler ---

  async loadInvitations(): Promise<void> {
    this.invitationsLoading.set(true);
    try {
      this.invitations.set(await firstValueFrom(this.client.adminInvitationsAll()));
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Davetler yüklenemedi', detail: describeApiError(err) });
    } finally {
      this.invitationsLoading.set(false);
    }
  }

  private emptyNewInvitation() {
    return { email: '', role: UserRole.BankAdmin as UserRole | null, bankId: null as string | null };
  }

  openInviteDialog(): void {
    this.newInvitation = this.emptyNewInvitation();
    this.inviteError.set(null);
    this.inviteSubmitted = false;
    this.inviteDialogVisible.set(true);
  }

  async createInvitation(): Promise<void> {
    this.inviteError.set(null);
    this.inviteSubmitted = true;
    const needsBank = this.authService.isSuperAdmin() && this.newInvitation.role === UserRole.BankAdmin;
    if (!this.newInvitation.email || (needsBank && !this.newInvitation.bankId)) return;

    this.inviteBusy.set(true);
    try {
      await firstValueFrom(
        this.client.adminInvitations(new CreateInvitationRequest({
          email: this.newInvitation.email,
          inviteUrlBase: window.location.origin,
          role: this.newInvitation.role ?? undefined,
          bankId: needsBank ? this.newInvitation.bankId! : undefined
        }))
      );
      this.inviteDialogVisible.set(false);
      this.messageService.add({ severity: 'success', summary: 'Davet gönderildi', detail: this.newInvitation.email });
      await this.loadInvitations();
    } catch (err) {
      this.inviteError.set(describeApiError(err));
    } finally {
      this.inviteBusy.set(false);
    }
  }

  confirmRevokeInvitation(invitation: InvitationSummary): void {
    this.confirmationService.confirm({
      header: 'Daveti iptal et',
      message: `"${invitation.email}" adresine gönderilen daveti iptal etmek istediğinize emin misiniz?`,
      icon: 'pi pi-ban',
      acceptLabel: 'İptal Et',
      rejectLabel: 'Vazgeç',
      accept: () => this.revokeInvitation(invitation)
    });
  }

  invitationStatus(invitation: InvitationSummary): { label: string; severity: 'success' | 'danger' | 'warn' | 'info' } {
    if (invitation.acceptedAtUtc) return { label: 'Kabul Edildi', severity: 'success' };
    if (invitation.revokedAtUtc) return { label: 'İptal Edildi', severity: 'danger' };
    if (invitation.expiresAtUtc && new Date(invitation.expiresAtUtc) <= new Date()) return { label: 'Süresi Doldu', severity: 'warn' };
    return { label: 'Bekliyor', severity: 'info' };
  }

  isPendingInvitation(invitation: InvitationSummary): boolean {
    return this.invitationStatus(invitation).label === 'Bekliyor';
  }

  private async revokeInvitation(invitation: InvitationSummary): Promise<void> {
    this.invitationBusyId.set(invitation.id ?? null);
    try {
      await firstValueFrom(this.client.cancel(invitation.id!));
      this.messageService.add({ severity: 'success', summary: 'Davet iptal edildi', detail: invitation.email });
      await this.loadInvitations();
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'İşlem başarısız', detail: describeApiError(err) });
    } finally {
      this.invitationBusyId.set(null);
    }
  }
}
