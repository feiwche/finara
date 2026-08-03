import { Component, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MenubarModule } from 'primeng/menubar';
import { ButtonModule } from 'primeng/button';
import { Menu, MenuModule } from 'primeng/menu';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { MenuItem } from 'primeng/api';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { AuthService, RealtimeService, ThemeService } from '@finara/shared-ui';

@Component({
    selector: 'app-shell',
    imports: [RouterOutlet, RouterLink, RouterLinkActive, MenubarModule, ButtonModule, MenuModule, TagModule, TooltipModule, TranslatePipe],
    templateUrl: './app-shell.component.html'
})
export class AppShellComponent implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly realtimeService = inject(RealtimeService);
  private readonly translate = inject(TranslateService);
  protected readonly authService = inject(AuthService);
  protected readonly themeService = inject(ThemeService);

  // BackOffice'in app-shell.component.ts'inde bulunan AYNI hata burada da vardı — bir
  // GETTER, her change detection turunda [model]="userMenuItems" ifadesine yeni bir dizi/obje
  // referansı veriyordu, bu da PrimeNG'in p-menu'sünün açık/kapalı durumunu sürekli sıfırlayıp
  // menünün bir öğeye tıklandıktan sonra kapanmamasına yol açıyordu (bkz. o dosyadaki detaylı
  // yorum). Aynı langTick+computed() deseniyle düzeltildi.
  private readonly langTick = signal(0);
  private readonly langChangeSub: Subscription;

  // Bir öğeye tıklanınca PrimeNG'in KENDİ auto-hide davranışı bazen tetiklenmiyordu (canlı
  // testte bulundu). Garanti altına almak için her command sonrasında ViewChild referansıyla
  // ELLE .hide() çağrılıyor.
  @ViewChild('userMenu') private userMenu?: Menu;

  protected readonly userMenuItems = computed<MenuItem[]>(() => {
    this.langTick();
    return [
      { label: this.translate.instant('nav.account'), icon: 'pi pi-user', command: () => { this.userMenu?.hide(); void this.router.navigate(['/hesabim']); } },
      { label: this.translate.instant('nav.settings'), icon: 'pi pi-cog', command: () => { this.userMenu?.hide(); void this.router.navigate(['/ayarlar']); } },
      { separator: true },
      { label: this.translate.instant('nav.logout'), icon: 'pi pi-sign-out', command: () => { this.userMenu?.hide(); void this.logout(); } }
    ];
  });

  constructor() {
    this.langChangeSub = this.translate.onLangChange.subscribe(() => this.langTick.update(n => n + 1));
  }

  ngOnDestroy(): void {
    this.langChangeSub.unsubscribe();
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
