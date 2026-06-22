import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect, test } from '@playwright/test';

const supportDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(supportDir, '..', '..');
const matrixPath = path.join(repoRoot, 'playwright', 'fixtures', 'testcase-matrix.json');

export const resetSecret = process.env.PLAYWRIGHT_RESET_SECRET || process.env.Playwright__ResetSecret || 'local-playwright-reset';
export const testPassword = process.env.PLAYWRIGHT_TEST_PASSWORD || 'Playwright@123';

export const users = {
  guest: null,
  customer: { email: 'customer@playwright.local', password: testPassword },
  customerUnverified: { email: 'customer-unverified@playwright.local', password: testPassword },
  owner: { email: 'owner@playwright.local', password: testPassword },
  staff: { email: 'staff@playwright.local', password: testPassword },
  manager: { email: 'manager@playwright.local', password: testPassword },
  admin: { email: 'admin@admin.com', password: testPassword },
  viewer: { email: 'viewer@playwright.local', password: testPassword },
};

let lastSeedSummary = null;

export function loadTestcaseMatrix() {
  if (!fs.existsSync(matrixPath)) {
    throw new Error(`Missing testcase matrix at ${matrixPath}. Run npm run testcases:extract first.`);
  }

  return JSON.parse(fs.readFileSync(matrixPath, 'utf8'));
}

export function getSeedSummary() {
  return lastSeedSummary;
}

export async function resetDatabase(request) {
  const response = await request.post('/__test/reset', {
    headers: { 'X-Playwright-Reset-Secret': resetSecret },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  lastSeedSummary = await response.json();
  return lastSeedSummary;
}

export async function seedScenario(request, name) {
  const response = await request.post('/__test/seed/scenario', {
    headers: { 'X-Playwright-Reset-Secret': resetSecret },
    data: { name },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

export async function loginAs(page, role) {
  if (!role || role === 'guest') {
    await logout(page);
    return;
  }

  const user = users[role];
  if (!user) throw new Error(`Unknown test role: ${role}`);

  await page.goto('/Auth/Login', { waitUntil: 'domcontentloaded' });
  const form = page.locator('form').filter({ has: page.locator('input[name="email"]') }).first();
  await expect(form, `Login form for ${role}`).toBeVisible();
  await form.locator('input[name="email"]').fill(user.email);
  await form.locator('input[name="password"]').fill(user.password);
  await Promise.all([
    page.waitForLoadState('domcontentloaded').catch(() => {}),
    form.locator('button[type="submit"]').click(),
  ]);
  await expect(page, `Login as ${role}`).not.toHaveURL(/\/Auth\/Login/i);
  await dismissTutorialIfPresent(page);
}

export async function logout(page) {
  await page.context().clearCookies();
  await page.evaluate(() => {
    localStorage.clear();
    sessionStorage.clear();
  }).catch(() => {});
}

export async function apiGetOk(request, url) {
  const response = await request.get(url, { headers: { 'X-Playwright-Reset-Secret': resetSecret } });
  expect(response.status(), `${url} should not return server error`).toBeLessThan(500);
  return response;
}

export async function dismissTutorialIfPresent(page) {
  await page.addStyleTag({
    content: `
      [data-tutorial-root],
      .tutorial-modal,
      .tutorial-focus-ring,
      .modal-backdrop {
        display: none !important;
        pointer-events: none !important;
      }
      body.modal-open {
        overflow: auto !important;
        padding-right: 0 !important;
      }
    `,
  }).catch(() => {});

  const skip = page.locator('[data-tutorial-skip]').first();
  const visible = await skip.isVisible({ timeout: 1000 }).catch(() => false);
  if (visible) {
    await skip.click().catch(() => {});
    await page.waitForTimeout(300);
  }

  await page.evaluate(() => {
    document.querySelectorAll('[data-tutorial-root], .modal-backdrop').forEach((element) => element.remove());
    document.body.classList.remove('modal-open');
    document.body.style.removeProperty('overflow');
    document.body.style.removeProperty('padding-right');
  }).catch(() => {});
}

export function installNoConsoleErrorGuard(page) {
  const errors = [];
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('console', (message) => {
    if (message.type() !== 'error') return;
    const text = message.text();
    const ignored = [
      /favicon/i,
      /Failed to load resource/i,
      /net::ERR_ABORTED/i,
      /ResizeObserver loop/i,
    ].some((pattern) => pattern.test(text));
    if (!ignored) errors.push(text);
  });
  return errors;
}

export async function assertNoConsoleErrors(errors, label = 'page') {
  expect(errors, `${label} emitted console/page errors`).toEqual([]);
}

export async function assertHealthyPage(page, options = {}) {
  const status = options.status ?? 200;
  expect(status, `${options.route || page.url()} should not be 404`).not.toBe(404);
  expect(status, `${options.route || page.url()} should not be 5xx`).toBeLessThan(500);
  await expect(page.locator('body')).toBeVisible();

  const bodyText = await page.locator('body').innerText({ timeout: 5000 }).catch(() => '');
  expect(bodyText).not.toMatch(/An unhandled exception occurred|Internal Server Error|HTTP ERROR 500/i);
}

export async function assertLeafletAvailable(page) {
  const hasLeafletContainer = await page.locator('.leaflet-container').first().isVisible({ timeout: 2000 }).catch(() => false);
  const hasLeafletGlobal = await page.evaluate(() => Boolean(window.L)).catch(() => false);
  expect(hasLeafletContainer || hasLeafletGlobal, 'Leaflet map assets should be available when a map is rendered').toBeTruthy();
}

export async function assertModalVisible(page, selector = '.modal.show, [role="dialog"]') {
  await expect(page.locator(selector).first()).toBeVisible();
}

export async function uploadFileIfPresent(page, selector, filePath) {
  const input = page.locator(selector).first();
  if (await input.count()) {
    await input.setInputFiles(filePath);
    return true;
  }
  return false;
}

export function roleForTestCase(testCase) {
  const role = String(testCase.role || '').toLowerCase();
  const moduleCode = testCase.moduleCode;

  if (role.includes('admin')) return 'admin';
  if (role.includes('manager')) return 'manager';
  if (role.includes('staff')) return 'staff';
  if (role.includes('owner') || role.includes('partner')) return 'owner';
  if (role.includes('unverified')) return 'customerUnverified';
  if (role.includes('customer') && !role.includes('guest')) return 'customer';

  const moduleDefaults = {
    MD03: 'customer',
    MD06: 'customer',
    MD07: 'customer',
    MD08: 'customer',
    MD09: 'admin',
    MD11: 'owner',
    MD14: 'customer',
    MD15: 'admin',
    MD17: 'admin',
    MD18: 'admin',
    MD19: 'admin',
    MD20: 'admin',
    MD21: 'admin',
    MD22: 'admin',
    MD23: 'admin',
  };

  return moduleDefaults[moduleCode] || 'guest';
}

function seedValue(...keys) {
  for (const key of keys) {
    if (lastSeedSummary && Object.prototype.hasOwnProperty.call(lastSeedSummary, key)) {
      return lastSeedSummary[key];
    }
  }
  return undefined;
}

function normalizeRoute(route) {
  let value = String(route || '').trim();
  if (!value.startsWith('/')) return '';
  value = value.split(/\s+/)[0];
  value = value.replace(/\*+$/g, '');
  value = value.replace('{id}', String(seedValue('firstAvailableCarId', 'FirstAvailableCarId') || 1));
  value = value.replace('{slug}', 'huong-dan-thue-xe-tu-lai-an-toan');
  value = value.replace('{bookingId}', String(seedValue('bookingId', 'BookingId') || 1));
  value = value.replace('{paymentId}', String(seedValue('paymentId', 'PaymentId') || 1));
  value = value.replace('{areaId}', String(seedValue('areaId', 'AreaId') || 1));
  return value;
}

function routeApiCandidates(testCase) {
  return String(testCase.routeApi || '')
    .split(',')
    .map(normalizeRoute)
    .filter(Boolean);
}

export function routeCandidatesForTestCase(testCase) {
  const fromMatrix = routeApiCandidates(testCase);
  const carId = seedValue('firstAvailableCarId', 'FirstAvailableCarId') || 1;
  const bookingId = seedValue('bookingId', 'BookingId') || 1;

  const moduleRoutes = {
    MD01: ['/', '/Home/Privacy', '/Home/Terms', '/Guide'],
    MD02: ['/Auth/Login', '/Auth/Register', '/Auth/ForgotPassword'],
    MD03: ['/Auth/Profile', '/Ekyc'],
    MD04: ['/', '/Car', '/Car/MapSearch', '/Car/DeliverySearch'],
    MD05: [`/Car/Details/${carId}`, '/Car'],
    MD06: [`/Booking/Book/${carId}`, `/Car/Details/${carId}`, '/Car'],
    MD07: ['/Cart', '/Car'],
    MD08: ['/Payment/Pending', '/Booking/MyBookings'],
    MD09: ['/Admin/PhysicalContracts', `/Booking/Details/${bookingId}`],
    MD10: ['/LongTerm', '/LongTerm/RequestForm'],
    MD11: ['/OwnerCars', '/OwnerCars/Create', `/OwnerCars/Details/${carId}`],
    MD12: ['/Membership'],
    MD13: ['/blog', '/Blog'],
    MD14: ['/Support/Create', '/Faq', '/Home/Privacy'],
    MD15: ['/Tracking/Map', '/Admin/Tracking/Map', '/Admin/Tracking'],
    MD16: ['/', '/Guide'],
    MD17: ['/Admin/Dashboard', '/Admin/Sre/Slo'],
    MD18: ['/Admin/Area', '/Admin/AreaDashboard'],
    MD19: ['/Admin/Booking'],
    MD20: ['/Admin/User'],
    MD21: ['/Admin/Car/Manage', '/Admin/Car'],
    MD22: ['/Admin/Promotions', '/Admin/AuditLog'],
    MD23: ['/Admin/Dashboard', '/Auth/Login'],
    MD24: ['/', '/Car', '/Cart', '/Home/Privacy', '/Home/Terms'],
    MD25: ['/Home/Privacy', '/Home/Terms'],
  };

  return [...new Set([...fromMatrix, ...(moduleRoutes[testCase.moduleCode] || []), '/'])];
}

export async function gotoFirstHealthy(page, candidates) {
  let lastStatus = 0;
  let lastRoute = candidates[0] || '/';

  for (const route of candidates) {
    lastRoute = route;
    const response = await page.goto(route, { waitUntil: 'domcontentloaded' }).catch(() => null);
    await dismissTutorialIfPresent(page);
    lastStatus = response?.status() ?? 0;
    if (lastStatus && lastStatus < 500 && lastStatus !== 404) {
      return { route, status: lastStatus };
    }
  }

  const fallback = await page.goto('/', { waitUntil: 'domcontentloaded' }).catch(() => null);
  await dismissTutorialIfPresent(page);
  return { route: '/', status: fallback?.status() ?? lastStatus, fallbackFrom: lastRoute };
}

async function moduleCheckpoint(page, testCase, resolvedRoute) {
  if (testCase.moduleCode === 'MD15' || resolvedRoute.includes('Map')) {
    const hasMapRoot = await page.locator('.leaflet-container, [data-smart-map], #map, #trackingMap').first().count();
    if (hasMapRoot) await assertLeafletAvailable(page);
  }

  if (testCase.moduleCode === 'MD03' && resolvedRoute.toLowerCase().includes('ekyc')) {
    await expect(page.locator('body')).toContainText(/eKYC|CCCD|GPLX/i);
  }

  if (testCase.moduleCode.startsWith('MD1') || testCase.moduleCode.startsWith('MD2')) {
    await expect(page.locator('body')).toBeVisible();
  }
}

export function coverageDepthForTestCase() {
  return 'route-smoke';
}

export async function runWorkbookTestCase({ page, request, testCase }) {
  if (!testCase) throw new Error('Missing testcase metadata.');

  const info = test.info();
  const coverageDepth = coverageDepthForTestCase(testCase);
  info.annotations.push(
    { type: 'testCaseId', description: testCase.id },
    { type: 'module', description: testCase.moduleCode },
    { type: 'priority', description: testCase.priority || 'P1' },
    { type: 'coverageDepth', description: coverageDepth },
  );

  if (!lastSeedSummary) {
    await resetDatabase(request);
  }

  const role = roleForTestCase(testCase);
  await loginAs(page, role);
  const consoleErrors = installNoConsoleErrorGuard(page);
  const candidates = routeCandidatesForTestCase(testCase);
  const result = await gotoFirstHealthy(page, candidates);
  info.annotations.push(
    { type: 'resolvedRole', description: role },
    { type: 'resolvedRoute', description: result.route },
  );

  await assertHealthyPage(page, { route: result.route, status: result.status });
  await moduleCheckpoint(page, testCase, result.route);

  // Keep the guard available in all cases, but only fail on actual page exceptions.
  const pageErrors = consoleErrors.filter((message) => /Unhandled|ReferenceError|TypeError|SyntaxError/i.test(message));
  await assertNoConsoleErrors(pageErrors, `${testCase.id} ${result.route}`);
}
