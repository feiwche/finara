import { Component, OnDestroy, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';

import { AcceptInvitationRequest, Client, describeApiError, userRoleLabel } from '@finara/shared-ui';

@Component({
    selector: 'app-accept-invitation',
    imports: [FormsModule, RouterLink, PasswordModule, ButtonModule, CardModule, MessageModule],
    templateUrl: './accept-invitation.component.html'
})
export class AcceptInvitationComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly client = inject(Client);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private token = '';
  private navigateTimeout: ReturnType<typeof setTimeout> | null = null;
  protected hasToken = true;
  protected previewLoading = signal(true);
  protected previewEmail = signal<string | null>(null);
  protected previewRole = signal<string | null>(null);
  protected previewError = signal<string | null>(null);

  protected newPassword = '';
  protected confirmPassword = '';
  protected busy = signal(false);
  protected error = signal<string | null>(null);
  protected submitted = signal(false);
  protected formSubmitted = false;

  protected readonly roleLabel = userRoleLabel;

  ngOnDestroy(): void {
    if (this.navigateTimeout) clearTimeout(this.navigateTimeout);
  }

  async ngOnInit(): Promise<void> {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (!this.token) {
      this.hasToken = false;
      this.previewLoading.set(false);
      this.previewError.set('Geçersiz davet bağlantısı — e-postanızdaki bağlantıyı tekrar kullanın.');
      return;
    }

    // NSwag'ın ürettiği istemci `responseType: 'blob'` kullanıyor — Angular SSR'ın Node
    // tarafındaki HttpClient backend'i gerçek bir Blob üretmiyor, bu da sunucu tarafı
    // render sırasında bu isteği "[object Object]" is not valid JSON hatasına düşürüyordu
    // (canlı testte bulundu). Bu sayfa dışındaki hiçbir public auth sayfası (login/
    // forgot-password/reset-password) ngOnInit içinde bir API çağrısı yapmadığı için bu
    // sorunu daha önce hiçbiri açığa çıkarmamıştı. Önizleme isteği sadece tarayıcıda
    // (hydration sonrası) yapılıyor; SSR çıktısı sadece "yükleniyor" durumunu gösterir.
    if (!this.isBrowser) {
      return;
    }

    try {
      const preview = await firstValueFrom(this.client.invitations(this.token));
      this.previewEmail.set(preview.email ?? null);
      this.previewRole.set(preview.role ?? null);
    } catch (err) {
      this.hasToken = false;
      this.previewError.set(describeApiError(err));
    } finally {
      this.previewLoading.set(false);
    }
  }

  async submit(): Promise<void> {
    this.formSubmitted = true;
    if (!this.token || !this.newPassword || this.newPassword !== this.confirmPassword) return;

    this.error.set(null);
    this.busy.set(true);
    try {
      await firstValueFrom(this.client.acceptInvitation(new AcceptInvitationRequest({ token: this.token, newPassword: this.newPassword })));
      this.submitted.set(true);
      this.navigateTimeout = setTimeout(() => void this.router.navigate(['/login']), 2500);
    } catch (err) {
      this.error.set(describeApiError(err));
    } finally {
      this.busy.set(false);
    }
  }
}
