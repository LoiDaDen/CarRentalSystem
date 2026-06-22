import { test, expect } from '@playwright/test';

test('production preflight responds', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('body')).toBeVisible();
});
