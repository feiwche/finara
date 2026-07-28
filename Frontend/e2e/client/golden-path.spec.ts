import { test, expect } from '@playwright/test';
import { demoLogin, goToCampaigns, goToCustomers, goToTools } from './support';

// Demo hesabıyla temel gezinme akışının uçtan uca (gerçek backend'e karşı) hâlâ
// çalıştığını doğrulayan, en kritik "hiçbir şey kırılmamış" duman testi.
test('demo hesabıyla giriş yapıp müşteri listesini görüntüleme ve detay açma', async ({ page }) => {
  await demoLogin(page);
  await goToCustomers(page);

  const rows = page.locator('table tbody tr');
  await expect(rows.first()).toBeVisible();
  const rowCountBefore = await rows.count();
  expect(rowCountBefore).toBeGreaterThan(0);

  await rows.first().click();
  await expect(page).toHaveURL(/\/customers\/.+/);
  await expect(page.locator('main')).not.toBeEmpty();
});

test('demo hesabıyla Kampanyalar ve Araçlar sayfaları hatasız açılır', async ({ page }) => {
  await demoLogin(page);

  await goToCampaigns(page);
  await expect(page).toHaveURL(/\/campaigns/);

  await goToTools(page);
  await expect(page).toHaveURL(/\/tools/);
});
