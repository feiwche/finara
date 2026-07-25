import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { ConfirmationService, MessageService } from 'primeng/api';

import { AuthService, RealtimeService, describeApiError, userRoleLabel } from '@finara/shared-ui';

@Component({
    selector: 'app-account-page',
    imports: [CardModule, ButtonModule],
    templateUrl: './account-page.component.html'
})
export class AccountPageComponent {
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
}
