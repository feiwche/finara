import { test, expect } from '@playwright/test';

// BackOffice'te oturum açmış (admin/analist) akışlar için bilinen/sabit bir test
// hesabı olmadığından, bu paket bilinçli olarak sadece kimlik doğrulaması gerektirmeyen
// (giriş öncesi) ekranlara odaklanıyor. Kimlik doğrulamalı BackOffice akışları için
// gerçek bir test hesabı tanımlanınca bu paket genişletilebilir.
test('giriş ekranı dil seçici BackOffice başlığını da çevirir', async ({ page }) => {
  await page.goto('/login');

  await page.getByRole('button', { name: 'EN', exact: true }).click();
  await expect(page.locator('.p-card-title')).toHaveText('Log In');
  await expect(page).toHaveTitle('Finara BackOffice — Log In');

  await page.getByRole('button', { name: 'DE', exact: true }).click();
  await expect(page.locator('.p-card-title')).toHaveText('Anmelden');
  await expect(page).toHaveTitle('Finara BackOffice — Anmelden');

  await page.getByRole('button', { name: 'TR', exact: true }).click();
  await expect(page.locator('.p-card-title')).toHaveText('Giriş Yap');
  await expect(page).toHaveTitle('Finara BackOffice — Giriş');
});
