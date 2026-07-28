import { Component, OnInit, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MenubarModule } from 'primeng/menubar';
import { ButtonModule } from 'primeng/button';
import { MenuModule } from 'primeng/menu';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { MenuItem } from 'primeng/api';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService, RealtimeService, ThemeService } from '@finara/shared-ui';

@Component({
    selector: 'app-shell',
    imports: [RouterOutlet, RouterLink, RouterLinkActive, MenubarModule, ButtonModule, MenuModule, TagModule, TooltipModule, TranslatePipe],
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
