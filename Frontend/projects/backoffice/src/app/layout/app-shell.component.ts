import { Component, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MenubarModule } from 'primeng/menubar';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { Menu, MenuModule } from 'primeng/menu';
import { TooltipModule } from 'primeng/tooltip';
import { MenuItem } from 'primeng/api';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { AuthService, RealtimeService, ThemeService } from '@finara/shared-ui';

@Component({
    selector: 'app-shell',
    imports: [RouterOutlet, RouterLink, RouterLinkActive, MenubarModule, ButtonModule, TagModule, MenuModule, TooltipModule, TranslatePipe],
    templateUrl: './app-shell.component.html'
})
export class AppShellComponent implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly realtimeService = inject(RealtimeService);
  private readonly translate = inject(TranslateService);
  protected readonly authService = inject(AuthService);
  protected readonly themeService = inject(ThemeService);

  // KRİTİK — bulundu (canlı testte): bu iki liste önceden bir GETTER'dı ("dil değişince
  // otomatik güncellensin" gerekçesiyle) — bu, Angular'ın her change detection turunda
  // (ki zone.js sayesinde çok sık tetikleniyor) [model]="managementMenuItems" ifadesine
  // HER SEFERİNDE yeni bir dizi/yeni obje referansları veriyordu. PrimeNG'in p-menu'sü bu
  // referans değişikliğini "girdi değişti" sayıp kendi iç açık/kapalı durumunu sürekli
  // sıfırlıyordu — sonuç: kullanıcı bir öğeye tıklayıp yönlendirme gerçekleşiyordu ama
  // menü ekranda açık kalmaya devam ediyordu, hatta tekrar tetikleyiciye tıklanınca eskisini
  // kapatmak yerine ikinci bir kopya panel açılıyordu. settings-page.component.ts'teki AYNI
  // langTick+computed() deseniyle düzeltildi — dizi referansı artık sadece dil GERÇEKTEN
  // değişince yenileniyor, PrimeNG'in kendi iç durumu artık her CD turunda bozulmuyor.
  private readonly langTick = signal(0);
  private readonly langChangeSub: Subscription;

  // Bir öğeye tıklanınca PrimeNG'in KENDİ auto-hide davranışı bazen tetiklenmiyordu (canlı
  // testte bulundu — muhtemelen zone.js/CD zamanlamasıyla ilgili, RealtimeService'teki
  // SignalR düzeltmesinden ayrı bir kalıntı sorun). Garanti altına almak için her command
  // sonrasında ViewChild referansıyla ELLE .hide() çağrılıyor — PrimeNG'in kendi mekanizması
  // çalışsa da çalışmasa da menü artık her zaman kapanıyor.
  @ViewChild('managementMenu') private managementMenu?: Menu;
  @ViewChild('userMenu') private userMenu?: Menu;

  protected readonly userMenuItems = computed<MenuItem[]>(() => {
    this.langTick();
    return [
      { label: this.translate.instant('nav.account'), icon: 'pi pi-user', command: () => this.navigateAndClose('/hesabim', this.userMenu) },
      { label: this.translate.instant('nav.settings'), icon: 'pi pi-cog', command: () => this.navigateAndClose('/ayarlar', this.userMenu) },
      { separator: true },
      { label: this.translate.instant('nav.logout'), icon: 'pi pi-sign-out', command: () => { this.userMenu?.hide(); void this.logout(); } }
    ];
  });

  // Sol nav 7 öğeye kadar çıkıyordu (SuperAdmin için) — sık kullanılan Genel Bakış/
  // Kampanyalar direkt bağlantı olarak kaldı, geri kalan ikincil/yönetimsel ekranlar
  // "Yönetim" açılır menüsüne toplandı.
  protected readonly managementMenuItems = computed<MenuItem[]>(() => {
    this.langTick();
    const items: MenuItem[] = [
      { label: this.translate.instant('nav.auditLogs'), icon: 'pi pi-list', command: () => this.navigateAndClose('/audit-logs', this.managementMenu) },
      { label: this.translate.instant('nav.adminAccounts'), icon: 'pi pi-users', command: () => this.navigateAndClose('/admin-accounts', this.managementMenu) }
    ];

    if (this.authService.isSuperAdmin()) {
      items.push({ label: this.translate.instant('nav.bankManagement'), icon: 'pi pi-building', command: () => this.navigateAndClose('/banks', this.managementMenu) });
    }

    items.push(
      { label: this.translate.instant('nav.cohortExplorer'), icon: 'pi pi-sitemap', command: () => this.navigateAndClose('/cohorts', this.managementMenu) },
      { label: this.translate.instant('nav.notificationLog'), icon: 'pi pi-send', command: () => this.navigateAndClose('/notifications', this.managementMenu) }
    );

    return items;
  });

  private navigateAndClose(path: string, menu: Menu | undefined): void {
    menu?.hide();
    void this.router.navigate([path]);
  }

  constructor() {
    this.langChangeSub = this.translate.onLangChange.subscribe(() => this.langTick.update(n => n + 1));
  }

  ngOnDestroy(): void {
    this.langChangeSub.unsubscribe();
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
