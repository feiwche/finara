import { Component, OnInit, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MenubarModule } from 'primeng/menubar';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { MenuModule } from 'primeng/menu';
import { TooltipModule } from 'primeng/tooltip';
import { MenuItem } from 'primeng/api';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService, RealtimeService, ThemeService } from '@finara/shared-ui';

@Component({
    selector: 'app-shell',
    imports: [RouterOutlet, RouterLink, RouterLinkActive, MenubarModule, ButtonModule, TagModule, MenuModule, TooltipModule, TranslatePipe],
    templateUrl: './app-shell.component.html'
})
export class AppShellComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly realtimeService = inject(RealtimeService);
  private readonly translate = inject(TranslateService);
  protected readonly authService = inject(AuthService);
  protected readonly themeService = inject(ThemeService);

  // Koyu/açık mod tetikleyicisi bilinçli olarak burada DEĞİL — kullanıcı isteğiyle eski
  // yerine (bağımsız bir ikon butonu, şablonda) geri taşındı, sadece "Hesabım"/"Ayarlar"/
  // "Çıkış" bu menüde kalıyor. Model getter olarak tutuluyor ki dil değişince etiketler
  // bir sonraki change detection turunda otomatik güncellensin.
  protected get userMenuItems(): MenuItem[] {
    return [
      { label: this.translate.instant('nav.account'), icon: 'pi pi-user', command: () => this.router.navigate(['/hesabim']) },
      { label: this.translate.instant('nav.settings'), icon: 'pi pi-cog', command: () => this.router.navigate(['/ayarlar']) },
      { separator: true },
      { label: this.translate.instant('nav.logout'), icon: 'pi pi-sign-out', command: () => this.logout() }
    ];
  }

  // Sol nav 7 öğeye kadar çıkıyordu (SuperAdmin için) — sık kullanılan Genel Bakış/
  // Kampanyalar direkt bağlantı olarak kaldı, geri kalan ikincil/yönetimsel ekranlar
  // "Yönetim" açılır menüsüne toplandı.
  protected get managementMenuItems(): MenuItem[] {
    const items: MenuItem[] = [
      { label: this.translate.instant('nav.auditLogs'), icon: 'pi pi-list', command: () => this.router.navigate(['/audit-logs']) },
      { label: this.translate.instant('nav.adminAccounts'), icon: 'pi pi-users', command: () => this.router.navigate(['/admin-accounts']) }
    ];

    if (this.authService.isSuperAdmin()) {
      items.push({ label: this.translate.instant('nav.bankManagement'), icon: 'pi pi-building', command: () => this.router.navigate(['/banks']) });
    }

    items.push(
      { label: this.translate.instant('nav.cohortExplorer'), icon: 'pi pi-sitemap', command: () => this.router.navigate(['/cohorts']) },
      { label: this.translate.instant('nav.notificationLog'), icon: 'pi pi-send', command: () => this.router.navigate(['/notifications']) }
    );

    return items;
  }

  // "Yönetim" tetikleyicisinin kendisi bir routerLink olmadığı için routerLinkActive
  // otomatik çalışmıyor — aktif olup olmadığı geçerli URL'e bakılarak elle hesaplanıyor.
  protected get isManagementActive(): boolean {
    return ['/audit-logs', '/admin-accounts', '/banks', '/cohorts', '/notifications']
      .some(path => this.router.url.startsWith(path));
  }

  // Kabuk sadece bağlantının kendisini (giriş/çıkışa bağlı) yönetiyor — hangi olaya
  // nasıl tepki verileceği (liste yenile, toast göster) ilgili feature component'lerin
  // (Dashboard, Kampanya Yönetimi, Bildirim Kaydı) kendi sorumluluğu.
  ngOnInit(): void {
    void this.realtimeService.connect();
  }

  async logout(): Promise<void> {
    await this.realtimeService.disconnect();
    await this.authService.logout();
    await this.router.navigate(['/login']);
  }
}
