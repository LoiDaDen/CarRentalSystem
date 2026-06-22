const fs = require("fs");
const path = require("path");
const { chromium } = require("D:/Antigravity IDE/resources/app/node_modules/playwright");

const baseUrl = "http://localhost:5168";
const outputRoot = path.resolve(__dirname, "..", "docs", "evidence", "screenshots");

const accounts = {
  admin: { email: "hloi78274@gmail.com", password: "Admin@123" },
  manager: { email: "manager@demo.com", password: "Manager@123" },
  staff: { email: "staff@demo.com", password: "Staff@123" },
  customer: { email: "customer@demo.com", password: "Customer@123" },
  owner: { email: "partner@demo.com", password: "Partner@123" },
};

const routes = [
  ["public", "/", "customer_home"],
  ["public", "/Car", "customer_car_list"],
  ["public", "/Car/MapSearch", "customer_car_map"],
  ["customer", "/Booking/MyBookings", "customer_booking_history"],
  ["customer", "/Support/Mine", "customer_support"],
  ["customer", "/Ekyc", "customer_ekyc_mock"],
  ["owner", "/OwnerCars", "owner_dashboard"],
  ["owner", "/OwnerCars/Create", "owner_create_car"],
  ["owner", "/OwnerCars/Wallet", "owner_revenue_payout"],
  ["owner", "/OwnerCars/Contracts", "owner_contracts"],
  ["staff", "/Staff/Dashboard", "staff_dashboard"],
  ["staff", "/Staff/Handover", "staff_handover_list"],
  ["staff", "/Staff/Return", "staff_return_vehicle"],
  ["staff", "/Staff/Support", "staff_ticket_handling"],
  ["manager", "/Manager/Dashboard", "manager_dashboard"],
  ["manager", "/Manager/Operation", "manager_operations"],
  ["manager", "/Manager/Report", "manager_reports"],
  ["manager", "/Manager/Staff", "manager_staff_assignment"],
  ["manager", "/Manager/Revenue", "manager_revenue"],
  ["admin", "/Admin/Dashboard", "admin_dashboard"],
  ["admin", "/Admin/User", "admin_user_role"],
  ["admin", "/Admin/CarApproval", "admin_car_approval"],
  ["admin", "/Admin/Payment", "admin_payment_reconciliation"],
  ["admin", "/Admin/ApiKeys", "admin_api_keys"],
  ["admin", "/Admin/Privacy", "admin_privacy"],
  ["admin", "/Admin/AuditLog", "admin_audit_log"],
  ["admin", "/Admin/Sre/Slo", "admin_sre_monitoring"],
  ["admin", "/Admin/Settlement", "admin_settlement"],
];

async function login(page, account) {
  await page.context().clearCookies();
  await page.goto(`${baseUrl}/Auth/Login`, { waitUntil: "networkidle" });
  await page.locator('input[name="email"]').fill(account.email);
  await page.locator('input[name="password"]').fill(account.password);
  await page.locator('input[name="email"]').locator("xpath=ancestor::form").first().locator('button[type="submit"]').click();
  await page.waitForLoadState("networkidle");
  if (page.url().includes("/Auth/Login")) {
    throw new Error(`Login failed for ${account.email}`);
  }
}

async function main() {
  fs.mkdirSync(outputRoot, { recursive: true });
  const browser = await chromium.launch({
    headless: true,
    executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();
  const results = [];
  let currentRole = "public";

  for (const [role, route, name] of routes) {
    try {
      if (role !== "public" && role !== currentRole) {
        await login(page, accounts[role]);
        currentRole = role;
      }
      if (role === "public" && currentRole !== "public") {
        await context.clearCookies();
        currentRole = "public";
      }
      const response = await page.goto(`${baseUrl}${route}`, { waitUntil: "networkidle", timeout: 30000 });
      if (name === "admin_user_role" && await page.locator('#pinModal').count()) {
        await page.locator('input[name="pin"]').fill("123456");
        await page.locator('input[name="pin"]').locator("xpath=ancestor::form").first().locator('button[type="submit"]').click();
        await page.waitForLoadState("networkidle");
      }
      await page.evaluate(() => {
        document.querySelectorAll("input[type=password]").forEach((node) => node.value = "******");
      });
      const roleDir = path.join(outputRoot, role);
      fs.mkdirSync(roleDir, { recursive: true });
      const target = path.join(roleDir, `${name}.png`);
      await page.screenshot({ path: target, fullPage: true, animations: "disabled" });
      results.push({ role, route, name, status: response?.status() || null, finalUrl: page.url(), file: target });
      console.log(`CAPTURED ${role} ${route} -> ${response?.status()} ${target}`);
    } catch (error) {
      results.push({ role, route, name, status: "ERROR", error: error.message });
      console.error(`FAILED ${role} ${route}: ${error.message}`);
    }
  }

  fs.writeFileSync(path.join(outputRoot, "capture_results.json"), JSON.stringify(results, null, 2), "utf8");
  await browser.close();
  if (results.some((item) => item.status === "ERROR")) process.exitCode = 1;
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
