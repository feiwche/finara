import { defineConfig, devices } from '@playwright/test';

// E2E testleri gerçek backend + veritabanına ihtiyaç duyuyor (demo-login gerçek bir
// kullanıcı satırına karşı çalışıyor) — CI'da bu bağımlılığı bir workflow adımı ayağa
// kaldırıyor, yerelde ise geliştirici zaten `ng serve` ile çalışan sunucuları kullanıyor
// (webServer.reuseExistingServer bu ikisini ayırt ediyor).
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  workers: 1,
  reporter: process.env['CI'] ? 'github' : 'list',
  use: {
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [
    {
      name: 'client',
      testDir: './e2e/client',
      use: { ...devices['Desktop Chrome'], baseURL: 'http://localhost:4200' }
    },
    {
      name: 'backoffice',
      testDir: './e2e/backoffice',
      use: { ...devices['Desktop Chrome'], baseURL: 'http://localhost:4300' }
    }
  ],
  webServer: [
    {
      command: 'npx ng serve client --port 4200',
      url: 'http://localhost:4200',
      reuseExistingServer: !process.env['CI'],
      timeout: 120_000
    },
    {
      command: 'npx ng serve backoffice --port 4300',
      url: 'http://localhost:4300',
      reuseExistingServer: !process.env['CI'],
      timeout: 120_000
    }
  ]
});
