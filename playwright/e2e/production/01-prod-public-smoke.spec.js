import { test, expect } from '@playwright/test';

test('production public smoke renders home page', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('body')).toBeVisible();
});
