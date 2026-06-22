import { test, expect } from '@playwright/test';
import { dismissTutorialIfPresent, loginAs, logout, resetDatabase } from '../../support/test-app.js';

test.describe('Owner cars popup and approval workflow', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await resetDatabase(request);
  });

  test('owner sees detail popup, submits edit, and Staff Manager Admin approve it', async ({ page }) => {
    await loginAs(page, 'owner');
    await page.goto('/OwnerCars');
    await dismissTutorialIfPresent(page);

    const firstDetailButton = page.locator('[data-owner-car-detail]').first();
    await expect(firstDetailButton).toBeVisible();
    await firstDetailButton.click();

    const modal = page.locator('#ownerCarModal');
    await expect(modal).toBeVisible();
    await expect(modal).toContainText('Hoa hồng DriveGo');
    await expect(modal).toContainText('Việc cần làm');
    await expect(modal.locator('.leaflet-container')).toBeVisible();

    await modal.locator('[data-owner-car-edit]').click();
    await expect(modal.locator('[data-owner-car-edit-form]')).toBeVisible();

    const priceInput = modal.locator('input[name="PricePerDay"]');
    await priceInput.fill('990000');
    await expect(modal.locator('[data-owner-commission-fee]')).toContainText('198.000');
    await expect(modal.locator('[data-owner-income]')).toContainText('792.000');
    await modal.locator('[data-owner-car-edit-form]').evaluate((form) => form.requestSubmit());
    await expect(modal).toContainText('Bản sửa xe đã được gửi');

    await logout(page);
    await loginAs(page, 'staff');
    await page.goto('/Staff/CarApproval');
    await dismissTutorialIfPresent(page);
    await expect(page.locator('.card').filter({ hasText: 'Update' }).first()).toBeVisible();
    await page.locator('form[action*="/Approve"]').first().locator('button[type="submit"]').click();
    await expect(page.locator('.alert-success').first()).toContainText('Đã duyệt');

    await logout(page);
    await loginAs(page, 'manager');
    await page.goto('/Manager/CarApproval');
    await dismissTutorialIfPresent(page);
    await expect(page.locator('.card').filter({ hasText: 'Update' }).first()).toBeVisible();
    await page.locator('form[action*="/Approve"]').first().locator('button[type="submit"]').click();
    await expect(page.locator('.alert-success').first()).toContainText('Đã duyệt');

    await logout(page);
    await loginAs(page, 'admin');
    await page.goto('/Admin/CarApproval');
    await dismissTutorialIfPresent(page);
    await expect(page.locator('.card').filter({ hasText: 'Update' }).first()).toBeVisible();
    await page.locator('form[action*="/Approve"]').first().locator('button[type="submit"]').click();
    await expect(page.locator('.alert-success').first()).toContainText('Đã duyệt');

    await logout(page);
    await loginAs(page, 'owner');
    await page.goto('/OwnerCars');
    await dismissTutorialIfPresent(page);
    await page.locator('[data-owner-car-detail]').first().click();
    await expect(page.locator('#ownerCarModal')).toContainText('990.000');
  });

  test('delete request stops new booking while waiting for approval', async ({ page }) => {
    await loginAs(page, 'owner');
    await page.goto('/OwnerCars');
    await dismissTutorialIfPresent(page);

    await page.locator('[data-owner-car-detail]').first().click();
    const modal = page.locator('#ownerCarModal');
    page.once('dialog', (dialog) => dialog.accept());
    await modal.locator('[data-owner-car-delete] button[type="submit"]').click();
    await expect(modal).toContainText('tạm ngưng nhận booking mới');

    await page.reload();
    await expect(page.locator('.owner-badge.danger').filter({ hasText: 'Tạm ngưng booking mới' }).first()).toBeVisible();
  });
});
