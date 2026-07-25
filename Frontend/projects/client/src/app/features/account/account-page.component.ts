import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { ConfirmationService, MessageService } from 'primeng/api';

import { AuthService, RealtimeService, TwoFactorSetupResult, describeApiError, userRoleLabel } from '@finara/shared-ui';

@Component({
    selector: 'app-account-page',
    imports: [FormsModule, CardModule, ButtonModule, InputTextModule, MessageModule],
    templateUrl: './account-page.component.html'
})
export class AccountPageComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly realtimeService = inject(RealtimeService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);
  protected readonly authService = inject(AuthService);

  protected readonly roleLabel = computed(() => {
    const role = this.authService.role();
    return role ? userRoleLabel(role) : '';
  });

  protected logoutBusy = signal(false);
  protected logoutAllBusy = signal(false);

  protected twoFactorEnabled = signal(false);
  protected setupState = signal<'idle' | 'awaitingCode'>('idle');
  protected setupData = signal<TwoFactorSetupResult | null>(null);
  protected setupCode = '';
  protected disableCode = '';
  protected twoFactorBusy = signal(false);
  protected twoFactorError = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    const me = await this.authService.fetchCurrentUser();
    this.twoFactorEnabled.set(me.twoFactorEnabled ?? false);
  }

  async logoutThisDevice(): Promise<void> {
    this.logoutBusy.set(true);
    try {
      await this.realtimeService.disconnect();
      await this.authService.logout();
      await this.router.navigate(['/login']);
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'İşlem başarısız', detail: describeApiError(err) });
      this.logoutBusy.set(false);
    }
  }

  confirmLogoutAllDevices(): void {
    this.confirmationService.confirm({
      header: 'Tüm cihazlardan çıkış yap',
      message: 'Bu hesapla giriş yapılmış tüm cihazlar/tarayıcılar oturumdan çıkarılacak, siz de dahil herkes yeniden giriş yapmak zorunda kalacak. Devam edilsin mi?',
      icon: 'pi pi-sign-out',
      acceptLabel: 'Çıkış Yap',
      rejectLabel: 'Vazgeç',
      accept: () => this.logoutAllDevices()
    });
  }

  private async logoutAllDevices(): Promise<void> {
    this.logoutAllBusy.set(true);
    try {
      await this.realtimeService.disconnect();
      await this.authService.logoutAllDevices();
      await this.router.navigate(['/login']);
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'İşlem başarısız', detail: describeApiError(err) });
      this.logoutAllBusy.set(false);
    }
  }

  async beginTwoFactorSetup(): Promise<void> {
    this.twoFactorError.set(null);
    this.twoFactorBusy.set(true);
    try {
      const setup = await this.authService.beginTwoFactorSetup();
      this.setupData.set(setup);
      this.setupState.set('awaitingCode');
    } catch (err) {
      this.messageService.add({ severity: 'error', summary: 'Kurulum başlatılamadı', detail: describeApiError(err) });
    } finally {
      this.twoFactorBusy.set(false);
    }
  }

  async confirmTwoFactorSetup(): Promise<void> {
    this.twoFactorError.set(null);
    this.twoFactorBusy.set(true);
    try {
      await this.authService.confirmTwoFactorSetup(this.setupCode);
      this.twoFactorEnabled.set(true);
      this.setupState.set('idle');
      this.setupData.set(null);
      this.setupCode = '';
    } catch (err) {
      this.twoFactorError.set(describeApiError(err));
    } finally {
      this.twoFactorBusy.set(false);
    }
  }

  cancelTwoFactorSetup(): void {
    this.twoFactorError.set(null);
    this.setupState.set('idle');
    this.setupData.set(null);
    this.setupCode = '';
  }

  async disableTwoFactor(): Promise<void> {
    this.twoFactorError.set(null);
    this.twoFactorBusy.set(true);
    try {
      await this.authService.disableTwoFactor(this.disableCode);
      this.twoFactorEnabled.set(false);
      this.disableCode = '';
    } catch (err) {
      this.twoFactorError.set(describeApiError(err));
    } finally {
      this.twoFactorBusy.set(false);
    }
  }
}
