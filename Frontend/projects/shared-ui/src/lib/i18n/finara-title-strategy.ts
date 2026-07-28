import { Injectable, Injector, inject } from '@angular/core';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { TranslateService } from '@ngx-translate/core';

// Router'ın yerleşik TitleStrategy'si route.title'ı SADECE navigasyon anında bir kere
// okuyor — dil daha sonra (ör. Ayarlar'dan) değişirse, aynı sayfada kalan kullanıcı için
// sekme başlığı eski dilde donup kalıyordu (canlı testte bulundu). Bu yüzden route'lar artık
// düz bir `title` string'i değil, `data: { titleKey: 'pageTitle.xxx' }` taşıyor — bu strateji
// hem navigasyonda hem de translate.onLangChange tetiklendiğinde başlığı yeniden hesaplıyor
// (son snapshot'ı saklayıp diline göre yeniden uyguluyor).
//
// TranslateService'i DOĞRUDAN (eager) enjekte etmek NG0200 döngüsel bağımlılığa yol açıyordu —
// Router bootstrap'ta çok erken kurulduğu için TitleStrategy → TranslateService → HTTP loader
// → authInterceptor → AuthService → UserPreferencesService → TranslateService döngüsüne
// giriyordu (auth.service.ts'teki aynı kısıtlama, bkz. oradaki yorum). Bu yüzden burada da
// aynı lazy Injector.get() deseni kullanılıyor.
@Injectable({ providedIn: 'root' })
export class FinaraTitleStrategy extends TitleStrategy {
  private readonly injector = inject(Injector);
  private lastSnapshot: RouterStateSnapshot | null = null;
  private subscribed = false;

  private get title(): Title {
    return this.injector.get(Title);
  }

  private get translate(): TranslateService {
    return this.injector.get(TranslateService);
  }

  override updateTitle(snapshot: RouterStateSnapshot): void {
    this.lastSnapshot = snapshot;
    if (!this.subscribed) {
      this.subscribed = true;
      this.translate.onLangChange.subscribe(() => {
        if (this.lastSnapshot) this.updateTitle(this.lastSnapshot);
      });
    }
    const titleKey = this.buildTitleKey(snapshot);
    if (!titleKey) return;
    this.title.setTitle(this.translate.instant(titleKey));
  }

  private buildTitleKey(snapshot: RouterStateSnapshot): string | undefined {
    let route = snapshot.root;
    let titleKey: string | undefined;
    while (route.firstChild) {
      route = route.firstChild;
      if (route.data['titleKey']) titleKey = route.data['titleKey'] as string;
    }
    return titleKey ?? snapshot.root.data['titleKey'] as string | undefined;
  }
}
