import { Component, OnInit, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MenubarModule } from 'primeng/menubar';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { MenuModule } from 'primeng/menu';
import { MenuItem } from 'primeng/api';
import { AuthService, RealtimeService, ThemeService } from '@finara/shared-ui';

@Component({
    selector: 'app-shell',
    imports: [RouterOutlet, RouterLink, RouterLinkActive, MenubarModule, ButtonModule, TagModule, MenuModule],
    templateUrl: './app-shell.component.html'
})
export class AppShellComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly realtimeService = inject(RealtimeService);
  protected readonly authService = inject(AuthService);
  protected readonly themeService = inject(ThemeService);

  // Sağ üstteki e-posta/tema/çıkış çubuğu, sol taraftaki kalabalık nav ile birlikte
  // dar ekranlarda üst üste biniyordu — tek bir açılır menüye toplandı. Model getter
  // olarak tutuluyor ki tema durumu değişince ikon/etiket otomatik güncellensin.
  protected get userMenuItems(): MenuItem[] {
    return [
      { label: 'Hesabım', icon: 'pi pi-user', command: () => this.router.navigate(['/hesabim']) },
      {
        label: this.themeService.isDark() ? 'Açık Moda Geç' : 'Karanlık Moda Geç',
        icon: this.themeService.isDark() ? 'pi pi-sun' : 'pi pi-moon',
        command: () => this.themeService.toggle()
      },
      { separator: true },
      { label: 'Çıkış', icon: 'pi pi-sign-out', command: () => this.logout() }
    ];
  }

  // Sol nav 7 öğeye kadar çıkıyordu (SuperAdmin için) — sık kullanılan Genel Bakış/
  // Kampanyalar direkt bağlantı olarak kaldı, geri kalan ikincil/yönetimsel ekranlar
  // "Yönetim" açılır menüsüne toplandı.
  protected get managementMenuItems(): MenuItem[] {
    const items: MenuItem[] = [
      { label: 'Denetim Kaydı', icon: 'pi pi-list', command: () => this.router.navigate(['/audit-logs']) },
      { label: 'Yönetici Hesapları', icon: 'pi pi-users', command: () => this.router.navigate(['/admin-accounts']) }
    ];

    if (this.authService.isSuperAdmin()) {
      items.push({ label: 'Banka Yönetimi', icon: 'pi pi-building', command: () => this.router.navigate(['/banks']) });
    }

    items.push(
      { label: 'Kohort Gezgini', icon: 'pi pi-sitemap', command: () => this.router.navigate(['/cohorts']) },
      { label: 'Bildirim Kaydı', icon: 'pi pi-send', command: () => this.router.navigate(['/notifications']) }
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
