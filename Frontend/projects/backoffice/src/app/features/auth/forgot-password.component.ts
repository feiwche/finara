import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { TranslatePipe } from '@ngx-translate/core';

import { Client, ForgotPasswordRequest, describeApiError } from '@finara/shared-ui';

@Component({
    selector: 'app-forgot-password',
    imports: [FormsModule, RouterLink, InputTextModule, ButtonModule, CardModule, MessageModule, TranslatePipe],
    templateUrl: './forgot-password.component.html'
})
export class ForgotPasswordComponent {
  private readonly client = inject(Client);

  protected email = '';
  protected busy = signal(false);
  protected error = signal<string | null>(null);
  protected submitted = signal(false);

  async submit(): Promise<void> {
    if (!this.email) return;
    this.error.set(null);
    this.busy.set(true);
    try {
      // resetUrlBase = bu uygulamanın kendi origin'i (backend, client/backoffice
      // ayrımını bilmediği için izin verilen origin listesine karşı bunu doğruluyor).
      await firstValueFrom(
        this.client.forgotPassword(new ForgotPasswordRequest({ email: this.email, resetUrlBase: window.location.origin }))
      );
      this.submitted.set(true);
    } catch (err) {
      this.error.set(describeApiError(err));
    } finally {
      this.busy.set(false);
    }
  }
}
