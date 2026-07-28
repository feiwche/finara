import { Page, expect } from '@playwright/test';

// Demo giriş butonunun metni aktif dile göre değişiyor (auth.login.demoLogin çeviri
// anahtarı) — testler her üç dildeki metni de tanıyor ki hangi dilde başlarsak başlayalım
// (localStorage'da kalan önceki bir seçim yüzünden) güvenilir şekilde tıklayabilsin.
const DEMO_LOGIN_TEXT = /Demo Hesabıyla Gir|Continue with Demo Account|Mit Demo-Konto fortfahren/;

export async function setGuestLanguage(page: Page, lang: 'TR' | 'EN' | 'DE'): Promise<void> {
  await page.getByRole('button', { name: lang, exact: true }).click();
}

// Uygulama SSR kullandığı için giriş yaptıktan sonra korumalı bir rotaya (ör. /campaigns)
// tam sayfa navigasyonuyla (page.goto) gidilirse sunucu tarafı token'ı (localStorage'da,
// sadece istemcide) göremediği için giriş ekranına geri yönlendiriyor — bu yüzden testler
// giriş sonrası HER ZAMAN uygulama içi (client-side) tıklamalarla gezinmeli, page.goto ile
// değil (bkz. Ayarlar/dil oturumlarında canlı testte defalarca doğrulanan bu kısıt).
//
// Zaten /login'deyken TEKRAR page.goto('/login') yapmak (tam sayfa yeniden yükleme) bilerek
// ATLANIYOR — bu, GuestLanguageService'in setLanguage()'ın işaretlediği bellek-içi
// "manuallySet" bayrağını (localStorage'da DEĞİL, sayfa belleğinde tutuluyor) sıfırlayıp
// setGuestLanguage()'ın hemen ardından çağrıldığı testlerde demo girişinin giriş ekranında
// seçilen dili backend'in kayıtlı tercihinin önüne geçirmesini engelliyordu (canlı testte
// bulundu — gerçek uygulama davranışında bu sorun yok, sadece testin gereksiz yeniden
// yüklemesi yüzünden ortaya çıkıyordu).
export async function demoLogin(page: Page): Promise<void> {
  if (!/\/login$/.test(new URL(page.url()).pathname)) {
    await page.goto('/login');
  }
  await page.getByRole('button', { name: DEMO_LOGIN_TEXT }).click();
  await expect(page.getByRole('button', { name: 'demo@finara.dev' })).toBeVisible();
}

export async function goToCustomers(page: Page): Promise<void> {
  await page.locator('a[routerLink="/customers"]').click();
}

export async function goToCampaigns(page: Page): Promise<void> {
  await page.locator('a[routerLink="/campaigns"]').click();
}

export async function goToTools(page: Page): Promise<void> {
  await page.locator('a[routerLink="/tools"]').click();
}

// Kullanıcı menüsü PrimeNG p-menu ile oluşturuluyor — öğeler diller arasında metni
// değiştiği için ikon sınıfına (pi-cog/pi-user/pi-sign-out) göre seçiliyor, bu ikonlar
// dilden bağımsız (bkz. app-shell.component.html).
export async function goToSettings(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'demo@finara.dev' }).click();
  await page.locator('a.p-menu-item-link:has(.pi-cog)').click();
}
