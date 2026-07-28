import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { Client, ResetPasswordRequest, describeApiError } from '@finara/shared-ui';

@Component({
    selector: 'app-reset-password',
    imports: [FormsModule, RouterLink, PasswordModule, ButtonModule, CardModule, MessageModule, TranslatePipe],
    templateUrl: './reset-password.component.html'
})
export class ResetPasswordComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly client = inject(Client);
  private readonly translate = inject(TranslateService);

  private token = '';
  private navigateTimeout: ReturnType<typeof setTimeout> | null = null;
  protected hasToken = true;
  protected newPassword = '';
  protected confirmPassword = '';
  protected busy = signal(false);
  protected error = signal<string | null>(null);
  protected submitted = signal(false);
  protected formSubmitted = false;

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (!this.token) {
      this.hasToken = false;
      this.error.set(this.translate.instant('auth.resetPassword.invalidToken'));
    }
  }

  ngOnDestroy(): void {
    if (this.navigateTimeout) clearTimeout(this.navigateTimeout);
  }

  async submit(): Promise<void> {
    this.formSubmitted = true;
    if (!this.token || !this.newPassword || this.newPassword !== this.confirmPassword) return;

    this.error.set(null);
    this.busy.set(true);
    try {
      await firstValueFrom(this.client.resetPassword(new ResetPasswordRequest({ token: this.token, newPassword: this.newPassword })));
      this.submitted.set(true);
      this.navigateTimeout = setTimeout(() => void this.router.navigate(['/login']), 2500);
    } catch (err) {
      this.error.set(describeApiError(err));
    } finally {
      this.busy.set(false);
    }
  }
}
