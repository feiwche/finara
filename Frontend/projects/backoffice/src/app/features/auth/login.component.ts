import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';

import { AuthService, describeApiError } from '@finara/shared-ui';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, InputTextModule, PasswordModule, ButtonModule, CardModule, MessageModule],
  templateUrl: './login.component.html'
})
export class LoginComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected email = 'admin@finara.dev';
  protected password = '';
  protected busy = signal(false);
  protected error = signal<string | null>(null);

  ngOnInit(): void {
    // restoreSession() APP_INITIALIZER'da bu bileşen render edilmeden önce tamamlanmış
    // olur — oturum zaten geçerliyse (ör. kullanıcı elle /login'e geldiyse) tekrar
    // giriş formuyla karşılaşmasın.
    if (this.auth.isAuthenticated()) {
      this.router.navigate(['/campaigns']);
    }
  }

  async login(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.auth.login(this.email, this.password);
      await this.router.navigate(['/campaigns']);
    } catch (err) {
      this.error.set(describeApiError(err));
    } finally {
      this.busy.set(false);
    }
  }
}
