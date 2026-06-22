import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.PLAYWRIGHT_BASE_URL || process.env.Playwright__BaseUrl || 'http://127.0.0.1:5168';
const isProductionE2E = process.env.PLAYWRIGHT_E2E_ENV === 'production';
const workers = Number(process.env.PLAYWRIGHT_WORKERS || '1');

export default defineConfig({
  testDir: './playwright/e2e',
  timeout: 45_000,
  expect: {
    timeout: 8_000,
  },
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers,
  testIgnore: isProductionE2E ? [] : [/.*[\\/]production[\\/].*\.spec\.(js|ts)$/],
  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright/reports/html', open: 'never' }],
    ['json', { outputFile: 'playwright/reports/results.json' }],
  ],
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    locale: 'vi-VN',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'firefox-smoke',
      testMatch: /.*smoke.*\.spec\.(js|ts)/,
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit-smoke',
      testMatch: /.*smoke.*\.spec\.(js|ts)/,
      use: { ...devices['Desktop Safari'] },
    },
  ],
});
