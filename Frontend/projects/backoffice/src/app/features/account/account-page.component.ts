import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { firstValueFrom } from 'rxjs';

import { AuthService, BankSummary, Client, RealtimeService, bankDataProviderTypeLabel, describeApiError, userRoleLabel } from '@finara/shared-ui';

@Component({
    selector: 'app-account-page',
    imports: [CardModule, ButtonModule, TranslatePipe],
    templateUrl: './account-page.component.html'
})
export class AccountPageComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly realtimeService = inject(RealtimeService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);
  private readonly client = inject(Client);
  private readonly translate = inject(TranslateService);
  protected readonly authService = inject(AuthService);

  protected readonly roleLabel = computed(() => {
    const role = this.authService.role();
    return role ? userRoleLabel(role) : '';
  });

  // SuperAdmin'in bağlı bir bankası olmadığı için /banks/mine 404 döner — bu beklenen,
  // hataya değer bir durum değil, kart sessizce gösterilmez (bkz. ngOnInit).
  protected bank = signal<BankSummary | null>(null);
  protected readonly bankDataProviderTypeLabel = bankDataProviderTypeLabel;

  protected logoutAllBusy = signal(false);

  async ngOnInit(): Promise<void> {
    try {
      this.bank.set(await firstValueFrom(this.client.mine()));
    } catch {
      // 404 = hesabın bağlı bir bankası yok (SuperAdmin) — kart zaten gösterilmeyecek.
      this.bank.set(null);
    }
  }

  confirmLogoutAllDevices(): void {
    this.confirmationService.confirm({
      header: this.translate.instant('account.security.confirmHeader'),
      message: this.translate.instant('account.security.confirmMessage'),
      icon: 'pi pi-sign-out',
      acceptLabel: this.translate.instant('account.security.confirmAccept'),
      rejectLabel: this.translate.instant('account.security.confirmReject'),
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
      this.messageService.add({ severity: 'error', summary: this.translate.instant('account.security.actionFailed'), detail: describeApiError(err) });
      this.logoutAllBusy.set(false);
    }
  }
}
