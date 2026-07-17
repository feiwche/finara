import { registerLocaleData } from '@angular/common';
import localeTr from '@angular/common/locales/tr';
import { BootstrapContext, bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { config } from './app/app.config.server';

// main.ts'deki ile aynı kayıt burada da gerekli — SSR/prerender sırasında currency/date
// pipe'ları locale'e ihtiyaç duyuyor; kaydedilmezse sunucu render'ı ile istemci hydration'ı
// farklı formatta sayı üretip hydration mismatch'e yol açabilirdi.
registerLocaleData(localeTr);

const bootstrap = (context: BootstrapContext) =>
    bootstrapApplication(AppComponent, config, context);

export default bootstrap;
