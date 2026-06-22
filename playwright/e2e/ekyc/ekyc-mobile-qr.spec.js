import { test, expect } from '@playwright/test';
import { dismissTutorialIfPresent, loginAs, resetDatabase } from '../../support/test-app.js';

const tinyPng = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=',
  'base64',
);

function ekycImage(name) {
  return {
    name,
    mimeType: 'image/png',
    buffer: tinyPng,
  };
}

async function createMobileSessionFromDesktop(page) {
  await loginAs(page, 'customerUnverified');
  await page.goto('/Ekyc', { waitUntil: 'domcontentloaded' });
  await dismissTutorialIfPresent(page);

  await expect(page.locator('#desktop-tab')).toBeVisible();
  await page.locator('#mobile-tab').click();
  await page.locator('#mobile-pane [data-ekyc-create-mobile]').click();

  const qrImage = page.locator('#mobile-pane [data-ekyc-qr-image]');
  await expect(qrImage).toBeVisible();
  await expect(qrImage).toHaveAttribute('src', /^data:image\/svg\+xml;base64,/);
  await expect(page.locator('#mobile-pane [data-ekyc-mobile-status]')).toHaveAttribute('data-state', 'Waiting');

  const mobileUrl = await page.locator('#mobile-pane [data-ekyc-mobile-url]').getAttribute('href');
  expect(mobileUrl).toContain('/Ekyc/Mobile/');
  return mobileUrl;
}

async function fillMobileEkycForm(mobilePage) {
  const mobileRoot = mobilePage.locator('[data-testid="ekyc-mobile-page"]');
  await expect(mobileRoot).toBeVisible();

  await mobileRoot.locator('input[name="idFront"]').setInputFiles(ekycImage('cccd-front.png'));
  await mobileRoot.locator('input[name="idBack"]').setInputFiles(ekycImage('cccd-back.png'));
  await mobileRoot.locator('input[name="license"]').setInputFiles(ekycImage('license-front.png'));
  await mobileRoot.locator('input[name="licenseBack"]').setInputFiles(ekycImage('license-back.png'));
  await mobileRoot.locator('input[name="face"]').setInputFiles(ekycImage('selfie.png'));
  await mobileRoot.locator('input[name="EkycPrivacyConsent"]').check();
  await mobileRoot.locator('input[name="EkycBiometricConsent"]').check();
}

test.describe('eKYC desktop and mobile QR flow', () => {
  test.beforeEach(async ({ request }) => {
    await resetDatabase(request);
  });

  test('desktop page creates a one-time mobile QR session', async ({ page }) => {
    const mobileUrl = await createMobileSessionFromDesktop(page);

    await page.goto(mobileUrl, { waitUntil: 'domcontentloaded' });
    const mobilePage = page.locator('[data-testid="ekyc-mobile-page"]');
    await expect(mobilePage).toBeVisible();
    await expect(mobilePage.locator('[data-ekyc-face-capture]')).toBeVisible();
    await expect(mobilePage.locator('[data-ekyc-face-start]')).toBeVisible();
  });

  test('mobile submission updates the desktop polling status', async ({ browser, page }) => {
    const mobileUrl = await createMobileSessionFromDesktop(page);
    const desktopStatus = page.locator('#mobile-pane [data-ekyc-mobile-status]');

    const mobileContext = await browser.newContext({
      viewport: { width: 390, height: 844 },
      isMobile: true,
      hasTouch: true,
    });
    const mobilePage = await mobileContext.newPage();

    try {
      await mobilePage.goto(mobileUrl, { waitUntil: 'domcontentloaded' });
      await fillMobileEkycForm(mobilePage);
      await Promise.all([
        mobilePage.waitForLoadState('domcontentloaded').catch(() => {}),
        mobilePage.locator('#mobileEkycForm button[type="submit"]').click(),
      ]);
      await expect(mobilePage.locator('#mobileEkycForm')).toHaveCount(0, { timeout: 10000 });
    } finally {
      await mobileContext.close();
    }

    await expect(desktopStatus).toHaveAttribute('data-state', /Verified|PendingReview|Rejected/, { timeout: 12000 });
  });
});
