import { Component, OnInit, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MenubarModule } from 'primeng/menubar';
import { ButtonModule } from 'primeng/button';
import { MenuModule } from 'primeng/menu';
import { TagModule } from 'primeng/tag';
import { MenuItem } from 'primeng/api';
import { AuthService, RealtimeService, ThemeService } from '@finara/shared-ui';

@Component({
    selector: 'app-shell',
    imports: [RouterOutlet, RouterLink, RouterLinkActive, MenubarModule, ButtonModule, MenuModule, TagModule],
    templateUrl: './app-shell.component.html'
})
export class AppShellComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly realtimeService = inject(RealtimeService);
  protected readonly authService = inject(AuthService);
  protected readonly themeService = inject(ThemeService);

  // Sağ üstteki e-posta/tema/çıkış çubuğu kalabalıklaşıyordu — tek bir açılır menüye
  // toplandı. Model getter olarak tutuluyor ki tema durumu değişince ikon/etiket bir
  // sonraki change detection turunda otomatik güncellensin (statik bir dizi olsaydı
  // menü ilk açıldığı andaki donuk değerde kalırdı).
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

  // Kabuk sadece bağlantının kendisini (giriş/çıkışa bağlı) yönetiyor — hangi olaya
  // nasıl tepki verileceği (liste yenile, toast göster) ilgili feature component'lerin
  // (Kampanyalar ekranı vb.) kendi sorumluluğu.
  ngOnInit(): void {
    void this.realtimeService.connect();
  }

  async logout(): Promise<void> {
    await this.realtimeService.disconnect();
    await this.authService.logout();
    await this.router.navigate(['/login']);
  }
}
