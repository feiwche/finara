import { test, expect } from '@playwright/test';
import { demoLogin, goToCampaigns, goToCustomers, goToSettings, goToTools, setGuestLanguage } from './support';

// Bu dosya, bu oturumda canlı olarak elle bulunup düzeltilen üç regresyonu otomatik
// olarak sabitliyor: (1) giriş ekranındaki dil seçicinin gerçekten metni değiştirmesi,
// (2) seçilen dilin oturuma taşınıp Müşteriler/Kampanyalar/Araçlar gibi ekranlarda da
// (sadece üst menüde değil, sayfa içeriğinde de) uygulanması, (3) Ayarlar'dan dil
// değiştirmenin hem sayfa içeriğini hem sekme başlığını sayfa yenilenmeden güncellemesi.

test.describe('Giriş ekranı dil seçici', () => {
  test('TR/EN/DE arasında geçiş yapınca kart metni değişir', async ({ page }) => {
    await page.goto('/login');

    await setGuestLanguage(page, 'EN');
    await expect(page.locator('.p-card-title')).toHaveText('Log In');

    await setGuestLanguage(page, 'DE');
    await expect(page.locator('.p-card-title')).toHaveText('Anmelden');

    await setGuestLanguage(page, 'TR');
    await expect(page.locator('.p-card-title')).toHaveText('Giriş Yap');
  });
});

test.describe('Giriş ekranında seçilen dil oturuma taşınır', () => {
  test('İngilizce seçilip demo girişi yapılınca Müşteriler/Kampanyalar/Araçlar İngilizce görünür', async ({ page }) => {
    await page.goto('/login');
    await setGuestLanguage(page, 'EN');
    await demoLogin(page);

    await expect(page.locator('a[routerLink="/customers"]')).toHaveText(/Customers/);
    await expect(page.locator('a[routerLink="/campaigns"]')).toHaveText(/Campaigns/);
    await expect(page.locator('a[routerLink="/tools"]')).toHaveText(/Tools/);

    // Bu tam olarak kullanıcının ekran görüntüsüyle bildirdiği hata: üst menü İngilizce
    // olsa da sayfa İÇERİĞİ (başlık, stat etiketleri, tablo başlıkları) Türkçe kalıyordu.
    await expect(page.getByRole('heading', { name: 'Customers', exact: true })).toBeVisible();
    await expect(page.locator('.stat-chip-label').getByText('TOTAL CUSTOMERS', { exact: true })).toBeVisible();
    await expect(page.locator('.stat-chip-label').getByText('HIGHLY RESPONSIVE', { exact: true })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Full Name' })).toBeVisible();

    await goToCampaigns(page);
    await expect(page.getByRole('heading', { name: 'Campaigns', exact: true })).toBeVisible();

    await goToTools(page);
    await expect(page.getByRole('heading', { name: 'Tools', exact: true })).toBeVisible();
    await expect(page.locator('.p-card-title').getByText('Generate Mock Data', { exact: true })).toBeVisible();
  });
});

test.describe('Ayarlar sayfasından dil değiştirme', () => {
  test('Almanca seçip kaydedince sayfa içeriği ve sekme başlığı anında güncellenir', async ({ page }) => {
    await page.goto('/login');
    await setGuestLanguage(page, 'EN');
    await demoLogin(page);
    await goToSettings(page);

    await expect(page).toHaveTitle('Finara — Settings');

    await page.getByRole('combobox').first().click();
    await page.getByRole('option', { name: 'Deutsch', exact: true }).click();
    await page.getByRole('button', { name: /Kaydet|Save|Speichern/ }).click();

    // Ayarlar sayfasından kaydetmenin HEM sayfa içeriğini HEM sekme başlığını (Router
    // TitleStrategy üzerinden) navigasyon olmadan güncellediği — daha önce sadece bir
    // sonraki tam navigasyonda güncellenip "donuk" kalıyordu.
    await expect(page).toHaveTitle('Finara — Einstellungen');
    await expect(page.locator('a[routerLink="/customers"]')).toHaveText(/Kunden/);

    // Demo hesabının kalıcı tercihini bozmamak için İngilizce'ye geri alınıyor.
    await page.getByRole('combobox').first().click();
    await page.getByRole('option', { name: 'English', exact: true }).click();
    await page.getByRole('button', { name: /Kaydet|Save|Speichern/ }).click();
    await expect(page).toHaveTitle('Finara — Settings');
  });
});

test.describe('Müşteriler ekranındaki eğilim etiketleri', () => {
  test('enum etiketleri de aktif dile göre görünür (Highly Responsive / Needs Engagement)', async ({ page }) => {
    await page.goto('/login');
    await setGuestLanguage(page, 'EN');
    await demoLogin(page);
    await goToCustomers(page);

    const propensityTag = page.locator('td').getByText(/Highly Responsive|Needs Engagement/).first();
    await expect(propensityTag).toBeVisible();
  });
});
