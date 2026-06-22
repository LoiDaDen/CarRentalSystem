import { test, expect } from '@playwright/test';

test.describe('public smoke', () => {
  test('home page renders without test-runner assets', async ({ page }) => {
    const errors = [];
    page.on('pageerror', (error) => errors.push(error.message));

    await page.goto('/', { waitUntil: 'commit' });
    await expect(page.locator('body')).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('map search page loads Leaflet/OpenStreetMap wrapper when available', async ({ page }) => {
    await page.goto('/Car/MapSearch', { waitUntil: 'commit' });
    await expect(page.locator('body')).toBeVisible();
    await expect.poll(() => page.evaluate(() => Boolean(window.SmartMap))).toBeTruthy();
  });
});
