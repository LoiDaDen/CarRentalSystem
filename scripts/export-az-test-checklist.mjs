import ExcelJS from "exceljs";
import fs from "node:fs";
import path from "node:path";

const repoRoot = process.cwd();
const matrixPath = path.join(repoRoot, "playwright", "fixtures", "testcase-matrix.json");
const addendumPath = path.join(repoRoot, "playwright", "fixtures", "testcase-addendum.json");
const defaultOutputPath = path.join(
  repoRoot,
  "CarRentalSystem",
  "docs",
  "qa",
  "CarRentalSystem_AZ_TestChecklist_2026-06-01.xlsx",
);
const outputPath = process.env.AZ_CHECKLIST_OUTPUT || defaultOutputPath;

const colors = {
  dark: "FF111827",
  blue: "FF1D4ED8",
  teal: "FF0F766E",
  green: "FF15803D",
  orange: "FFF97316",
  red: "FFB91C1C",
  purple: "FF7C3AED",
  header: "FF1F4E79",
  lightBlue: "FFEFF6FF",
  lightGreen: "FFF0FDF4",
  lightYellow: "FFFFFBEB",
  lightRed: "FFFEF2F2",
  lightGray: "FFF8FAFC",
  white: "FFFFFFFF",
  border: "FFD0D7DE",
};

const cp1252Specials = new Map([
  [0x20ac, 0x80],
  [0x201a, 0x82],
  [0x0192, 0x83],
  [0x201e, 0x84],
  [0x2026, 0x85],
  [0x2020, 0x86],
  [0x2021, 0x87],
  [0x02c6, 0x88],
  [0x2030, 0x89],
  [0x0160, 0x8a],
  [0x2039, 0x8b],
  [0x0152, 0x8c],
  [0x017d, 0x8e],
  [0x2018, 0x91],
  [0x2019, 0x92],
  [0x201c, 0x93],
  [0x201d, 0x94],
  [0x2022, 0x95],
  [0x2013, 0x96],
  [0x2014, 0x97],
  [0x02dc, 0x98],
  [0x2122, 0x99],
  [0x0161, 0x9a],
  [0x203a, 0x9b],
  [0x0153, 0x9c],
  [0x017e, 0x9e],
  [0x0178, 0x9f],
]);

const mojibakePattern = /(?:Ã|Ä|á|Â|Æ|Å|â|Ð|ð)[\s\S]*?(?:»|º|€|™|œ|Â|Ã|Ä|á|Æ|Å|â|Ð|ð)/;
const resultOptions = ["Pass", "Fail", "Blocked", "N/A"];

function fill(argb) {
  return { type: "pattern", pattern: "solid", fgColor: { argb } };
}

function borderAll() {
  return {
    top: { style: "thin", color: { argb: colors.border } },
    left: { style: "thin", color: { argb: colors.border } },
    bottom: { style: "thin", color: { argb: colors.border } },
    right: { style: "thin", color: { argb: colors.border } },
  };
}

function decodeCp1252Utf8(value) {
  const bytes = [];
  for (const char of value) {
    const code = char.codePointAt(0);
    if (code <= 0xff) {
      bytes.push(code);
    } else if (cp1252Specials.has(code)) {
      bytes.push(cp1252Specials.get(code));
    } else {
      return value;
    }
  }

  const decoded = new TextDecoder("utf-8", { fatal: false }).decode(Uint8Array.from(bytes));
  if (decoded.includes("\uFFFD")) return value;
  return decoded;
}

function fixText(value) {
  if (typeof value === "string") {
    return mojibakePattern.test(value) ? decodeCp1252Utf8(value) : value;
  }

  if (Array.isArray(value)) return value.map(fixText);
  if (value && typeof value === "object") {
    return Object.fromEntries(Object.entries(value).map(([key, entry]) => [key, fixText(entry)]));
  }

  return value;
}

function loadJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf8"));
}

function loadCases() {
  const matrix = fixText(loadJson(matrixPath));
  const addendum = fs.existsSync(addendumPath) ? fixText(loadJson(addendumPath)) : { modules: [], cases: [] };
  const modules = [...(matrix.modules || []), ...(addendum.modules || [])].sort((a, b) =>
    String(a.code).localeCompare(String(b.code), "en", { numeric: true }),
  );
  const cases = [...(matrix.cases || []), ...(addendum.cases || [])].sort((a, b) => a.numericId - b.numericId);
  return { modules, cases, sourceWorkbook: `${matrix.sourceWorkbook}; ${addendum.source || "no addendum"}` };
}

function normalizeSheetName(name, fallback) {
  const invalid = /[\\/*?:[\]]/g;
  return String(name || fallback).replace(invalid, " ").slice(0, 31);
}

function autoFilterRange(rowNumber, columnCount) {
  return {
    from: { row: rowNumber, column: 1 },
    to: { row: rowNumber, column: columnCount },
  };
}

function setTitle(ws, title, subtitle, width) {
  ws.mergeCells(1, 1, 1, width);
  const cell = ws.getCell(1, 1);
  cell.value = title;
  cell.font = { bold: true, size: 18, color: { argb: colors.white } };
  cell.fill = fill(colors.header);
  cell.alignment = { horizontal: "center", vertical: "middle" };
  ws.getRow(1).height = 32;

  if (subtitle) {
    ws.mergeCells(2, 1, 2, width);
    const subtitleCell = ws.getCell(2, 1);
    subtitleCell.value = subtitle;
    subtitleCell.font = { italic: true, color: { argb: colors.dark } };
    subtitleCell.fill = fill(colors.lightBlue);
    subtitleCell.alignment = { vertical: "middle", wrapText: true };
    ws.getRow(2).height = 26;
  }
}

function styleHeader(row) {
  row.eachCell((cell) => {
    cell.font = { bold: true, color: { argb: colors.white } };
    cell.fill = fill(colors.header);
    cell.alignment = { horizontal: "center", vertical: "middle", wrapText: true };
    cell.border = borderAll();
  });
  row.height = 28;
}

function styleBodyRow(row, index) {
  row.eachCell({ includeEmpty: true }, (cell) => {
    cell.border = borderAll();
    cell.alignment = { vertical: "top", wrapText: true };
    cell.fill = fill(index % 2 === 0 ? colors.white : colors.lightGray);
  });
}

function addValidation(ws, rowNumber) {
  ws.getCell(rowNumber, 9).dataValidation = {
    type: "list",
    allowBlank: true,
    formulae: [`"${resultOptions.join(",")}"`],
    showErrorMessage: true,
    errorTitle: "Gia tri khong hop le",
    error: "Chon Pass, Fail, Blocked hoac N/A.",
  };
}

function makeGuideSheet(workbook) {
  const ws = workbook.addWorksheet("Huong dan test", { views: [{ state: "frozen", ySplit: 4 }] });
  ws.columns = [
    { width: 24 },
    { width: 118 },
  ];
  setTitle(ws, "HUONG DAN TEST WEB THAT A-Z", "Checklist thu cong cho website that/staging/production; Playwright chi la du lieu tham khao local.", 2);

  const rows = [
    ["Muc dich", "Dung workbook nay de test thu cong tren web that hoac staging/production-like: public UI, customer flow, owner flow, admin/area ops, security, NFR va legal compliance."],
    ["URL web that", "Dien URL can test vao cot ghi chu/evidence cua moi case, vi du https://your-domain.vn. Neu test staging, ghi ro staging; neu test production, chi dung tai khoan va du lieu duoc phep test."],
    ["Tai khoan web that", "Dung cac tai khoan test that do admin tao san: Admin, Manager, Staff, Viewer, Customer verified, Customer unverified va Owner/Partner. Khong mac dinh dung tai khoan @playwright.local tren web that."],
    ["Du lieu nghiep vu that", "Chuan bi xe test, khu vuc test, booking test, voucher test va payment amount nho neu can thanh toan that. Moi du lieu phai co ten/ma de truy vet trong cot Evidence."],
    ["Khong reset production", "Khong goi /__test/reset, khong dung reset secret, khong xoa/sua du lieu that ngoai pham vi test da duoc phe duyet. Cac case destructive nen danh Blocked neu chua co moi truong staging."],
    ["Chay local neu can", "Neu muon doi chieu local: dotnet restore CarRentalSystem/CarRentalSystem.csproj; dotnet run --project CarRentalSystem/CarRentalSystem.csproj. Mac dinh local/Playwright base URL thuong la http://localhost:5168."],
    ["Seed Playwright local", "Chi khi test local/Playwright moi dung script CarRentalSystem/scripts/Run-PlaywrightA2Z.ps1 hoac endpoint __test/reset voi reset secret local-playwright-reset. Khong dung cach nay tren web that."],
    ["Cach ghi ket qua", "Moi dong test case dien Ngay test, Tester, Ket qua, Bug/Issue, Evidence va Ghi chu. Ket qua nen dung Pass, Fail, Blocked hoac N/A."],
    ["Quy tac Fail", "Danh Fail khi co loi 500, loi JavaScript nghiem trong, sai workflow, sai phan quyen, lo PII/secret, layout che CTA, hoac ket qua khong dung Expected."],
    ["Payment/eKYC that", "Neu test PayOS/FPT that, dung so tien nho va tai khoan sandbox/production duoc phe duyet. Ghi lai order code, transaction id, booking id va anh chup man hinh vao Evidence."],
    ["Vendor sandbox", "Cac case PayOS/FPT sandbox chi thuc hien khi da co cau hinh sandbox va public callback/tunnel. Neu chua co cau hinh, danh Blocked va ghi ro khoa cau hinh thieu."],
    ["Traceability", "TC001-TC525 lay tu testcase-matrix.json; TC526-TC543 lay tu testcase-addendum.json. Workbook nay khong thay doi code ung dung hay database schema."],
  ];

  const header = ws.getRow(4);
  header.values = ["Hang muc", "Huong dan"];
  styleHeader(header);
  rows.forEach((item, index) => {
    const row = ws.getRow(5 + index);
    row.values = item;
    row.height = 42;
    styleBodyRow(row, index);
    row.getCell(1).font = { bold: true, color: { argb: colors.header } };
  });
}

function makeDataSheet(workbook) {
  const ws = workbook.addWorksheet("Du lieu test", { views: [{ state: "frozen", ySplit: 4 }] });
  ws.columns = [
    { width: 22 },
    { width: 32 },
    { width: 44 },
    { width: 78 },
  ];
  setTitle(ws, "DU LIEU TEST WEB THAT", "Dien du lieu web that/staging truoc khi test; Playwright chi la tham khao local.", 4);
  const header = ws.getRow(4);
  header.values = ["Nhom", "Ten du lieu", "Gia tri", "Ghi chu"];
  styleHeader(header);

  const rows = [
    ["Web that", "Base URL", "[Dien URL web that/staging]", "Vi du https://your-domain.vn. Ghi ro moi truong: staging, production, UAT."],
    ["Web that - tai khoan", "Admin", "[Dien email / mat khau test that]", "Quan tri dashboard, user, reports, snapshot, API keys. Tai khoan do admin tao san tren web that."],
    ["Web that - tai khoan", "Manager", "[Dien email / mat khau test that]", "Quan ly theo khu vuc va nghiep vu van hanh tren du lieu that/staging."],
    ["Web that - tai khoan", "Staff", "[Dien email / mat khau test that]", "Nhan vien khu vuc, booking/car/maintenance theo quyen."],
    ["Web that - tai khoan", "Viewer", "[Dien email / mat khau test that]", "Role chi xem neu he thong bat Viewer."],
    ["Web that - tai khoan", "Customer verified", "[Dien email / mat khau test that]", "Khach hang da eKYC/verified de dat xe, cart, checkout, profile."],
    ["Web that - tai khoan", "Customer unverified", "[Dien email / mat khau test that]", "Khach hang chua verified de test chan booking/checkout va luong eKYC."],
    ["Web that - tai khoan", "Owner/Partner approved", "[Dien email / mat khau test that]", "Chu xe/partner da duyet de test OwnerCars va hop dong doi tac."],
    ["Web that - du lieu", "Xe test available", "[Dien car id / bien so / ten xe]", "Xe duoc phep dat test, co gia va trang thai phu hop."],
    ["Web that - du lieu", "Xe owner pending", "[Dien car id / bien so]", "Xe cho duyet de test admin approval hoac owner flow."],
    ["Web that - du lieu", "Khu vuc test", "[Dien area id / ma khu vuc]", "Khu vuc gan voi Manager/Staff/Viewer test."],
    ["Web that - du lieu", "Booking test", "[Dien booking id]", "Booking dung de test payment, contract, handover, tracking, refund."],
    ["Web that - du lieu", "Voucher/payment amount", "[Dien voucher / so tien test nho]", "Dung so tien that nho neu phai thanh toan qua PayOS/FPT production/sandbox."],
    ["Canh bao production", "Reset DB", "KHONG DUNG /__test/reset", "Khong reset, xoa, restore snapshot hay thao tac destructive tren production neu chua duoc phe duyet."],
    ["Local/Playwright tham khao", "Admin", "admin@admin.com / Playwright@123", "Chi dung local/Playwright, khong mac dinh dung cho web that."],
    ["Local/Playwright tham khao", "Manager", "manager@playwright.local / Playwright@123", "Chi dung local/Playwright."],
    ["Local/Playwright tham khao", "Staff", "staff@playwright.local / Playwright@123", "Chi dung local/Playwright."],
    ["Local/Playwright tham khao", "Viewer", "viewer@playwright.local / Playwright@123", "Chi dung local/Playwright."],
    ["Local/Playwright tham khao", "Customer verified", "customer@playwright.local / Playwright@123", "Chi dung local/Playwright."],
    ["Local/Playwright tham khao", "Customer unverified", "customer-unverified@playwright.local / Playwright@123", "Chi dung local/Playwright."],
    ["Local/Playwright tham khao", "Owner/Partner approved", "owner@playwright.local / Playwright@123", "Chi dung local/Playwright."],
    ["Local/Playwright tham khao", "First available car", "seed.firstAvailableCarId / PW-READY", "Fixture local/Playwright cho booking/catalog/detail."],
    ["Local/Playwright tham khao", "Reset endpoint", "POST /__test/reset", "Chi dung trong moi truong Playwright voi X-Playwright-Reset-Secret."],
  ];

  rows.forEach((item, index) => {
    const row = ws.getRow(5 + index);
    row.values = item;
    row.height = 34;
    styleBodyRow(row, index);
    row.getCell(1).font = { bold: true };
    row.getCell(2).font = { bold: true };
  });
  ws.autoFilter = autoFilterRange(4, 4);
}

function makeSummarySheet(workbook, modules, cases) {
  const ws = workbook.addWorksheet("Tong quan", { views: [{ state: "frozen", ySplit: 4 }] });
  ws.columns = [
    { width: 10 },
    { width: 42 },
    { width: 15 },
    { width: 12 },
    { width: 12 },
    { width: 14 },
    { width: 14 },
    { width: 14 },
    { width: 18 },
  ];
  setTitle(ws, "TONG QUAN TEST CHECKLIST", `Tong ${cases.length} test case tren ${modules.length} module.`, 9);

  const header = ws.getRow(4);
  header.values = ["Module", "Ten module", "Requirement", "Tong TC", "P0", "P1", "Pass", "Fail", "Blocked/NA"];
  styleHeader(header);

  modules.forEach((module, index) => {
    const moduleCases = cases.filter((testCase) => testCase.moduleCode === module.code);
    const rowNumber = 5 + index;
    const row = ws.getRow(rowNumber);
    const sheetName = normalizeSheetName(module.name, module.code);
    const p0 = moduleCases.filter((testCase) => testCase.priority === "P0").length;
    const p1 = moduleCases.filter((testCase) => testCase.priority !== "P0").length;
    row.values = [
      module.code,
      module.name,
      module.requirement,
      moduleCases.length,
      p0,
      p1,
      { formula: `COUNTIF('${sheetName}'!I:I,"Pass")` },
      { formula: `COUNTIF('${sheetName}'!I:I,"Fail")` },
      { formula: `COUNTIF('${sheetName}'!I:I,"Blocked")+COUNTIF('${sheetName}'!I:I,"N/A")` },
    ];
    styleBodyRow(row, index);
    row.getCell(1).font = { bold: true, color: { argb: colors.header } };
  });

  const totalRowNumber = 5 + modules.length;
  const totalRow = ws.getRow(totalRowNumber);
  totalRow.values = [
    "TOTAL",
    "Toan bo he thong",
    "",
    cases.length,
    cases.filter((testCase) => testCase.priority === "P0").length,
    cases.filter((testCase) => testCase.priority !== "P0").length,
    { formula: `SUM(G5:G${totalRowNumber - 1})` },
    { formula: `SUM(H5:H${totalRowNumber - 1})` },
    { formula: `SUM(I5:I${totalRowNumber - 1})` },
  ];
  totalRow.eachCell({ includeEmpty: true }, (cell) => {
    cell.border = borderAll();
    cell.font = { bold: true, color: { argb: colors.white } };
    cell.fill = fill(colors.dark);
    cell.alignment = { vertical: "middle", horizontal: "center", wrapText: true };
  });
  ws.autoFilter = autoFilterRange(4, 9);
}

function makeModuleSheet(workbook, module, moduleCases) {
  const ws = workbook.addWorksheet(normalizeSheetName(module.name, module.code), {
    views: [{ state: "frozen", ySplit: 6 }],
    properties: { tabColor: { argb: module.code === "MD26" ? colors.purple : colors.teal } },
  });
  ws.columns = [
    { width: 11 },
    { width: 10 },
    { width: 12 },
    { width: 34 },
    { width: 54 },
    { width: 66 },
    { width: 72 },
    { width: 14 },
    { width: 13 },
    { width: 24 },
    { width: 34 },
    { width: 34 },
    { width: 48 },
  ];

  setTitle(ws, module.name, `${module.requirement || ""} | ${moduleCases.length} test case`, 13);
  ws.getRow(4).values = [
    "Ma TC",
    "Module",
    "Priority",
    "Tieu de",
    "Route/API",
    "Cac buoc thuc hien",
    "Ket qua mong doi",
    "Ngay test",
    "Ket qua",
    "Tester",
    "Bug/Issue",
    "Evidence",
    "Ghi chu",
  ];
  styleHeader(ws.getRow(4));

  moduleCases.forEach((testCase, index) => {
    const rowNumber = 5 + index;
    const row = ws.getRow(rowNumber);
    row.values = [
      testCase.id,
      testCase.moduleCode,
      testCase.priority,
      testCase.title,
      testCase.routeApi,
      testCase.steps,
      testCase.expected,
      "",
      "",
      "",
      "",
      "",
      `Role: ${testCase.role || ""}\nSource: ${testCase.source || ""}\nWorkbook row: ${testCase.row || ""}`,
    ];
    row.height = Math.max(52, Math.min(170, 34 + Math.ceil(`${testCase.steps || ""}${testCase.expected || ""}`.length / 105) * 11));
    styleBodyRow(row, index);
    row.getCell(1).font = { bold: true, color: { argb: colors.header } };
    row.getCell(3).font = { bold: true, color: { argb: testCase.priority === "P0" ? colors.red : colors.orange } };
    row.getCell(8).numFmt = "dd/mm/yyyy";
    addValidation(ws, rowNumber);
  });

  ws.autoFilter = autoFilterRange(4, 13);
}

function validate(cases, modules) {
  const ids = cases.map((testCase) => testCase.id);
  const uniqueIds = new Set(ids);
  if (cases.length !== 543) throw new Error(`Expected 543 test cases, found ${cases.length}.`);
  if (modules.length !== 26) throw new Error(`Expected 26 modules, found ${modules.length}.`);
  if (uniqueIds.size !== ids.length) throw new Error("Duplicate test case IDs detected.");
  for (let index = 0; index < 543; index += 1) {
    const expectedId = `TC${String(index + 1).padStart(3, "0")}`;
    if (ids[index] !== expectedId) throw new Error(`Expected ${expectedId}, found ${ids[index]}.`);
  }
  const moduleCodes = modules.map((module) => module.code);
  for (let index = 1; index <= 26; index += 1) {
    const expectedModule = `MD${String(index).padStart(2, "0")}`;
    if (!moduleCodes.includes(expectedModule)) throw new Error(`Missing module ${expectedModule}.`);
  }
}

async function main() {
  const { modules, cases, sourceWorkbook } = loadCases();
  validate(cases, modules);

  const workbook = new ExcelJS.Workbook();
  workbook.creator = "Codex - CarRentalSystem QA";
  workbook.lastModifiedBy = "Codex - CarRentalSystem QA";
  workbook.created = new Date("2026-06-01T00:00:00+07:00");
  workbook.modified = new Date("2026-06-01T00:00:00+07:00");
  workbook.title = "CarRentalSystem A-Z Test Checklist 2026-06-01";
  workbook.subject = `Manual QA checklist from ${sourceWorkbook}`;

  makeGuideSheet(workbook);
  makeSummarySheet(workbook, modules, cases);
  makeDataSheet(workbook);
  for (const module of modules) {
    makeModuleSheet(
      workbook,
      module,
      cases.filter((testCase) => testCase.moduleCode === module.code),
    );
  }

  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  await workbook.xlsx.writeFile(outputPath);

  const verification = new ExcelJS.Workbook();
  await verification.xlsx.readFile(outputPath);
  const moduleSheets = verification.worksheets.filter((sheet) => /^MD\d+/.test(sheet.name));
  const testRows = moduleSheets.reduce((sum, sheet) => {
    let count = 0;
    sheet.eachRow((row) => {
      if (/^TC\d{3}$/.test(String(row.getCell(1).value || ""))) count += 1;
    });
    return sum + count;
  }, 0);

  if (moduleSheets.length !== 26 || testRows !== 543) {
    throw new Error(`Workbook verification failed: moduleSheets=${moduleSheets.length}, testRows=${testRows}`);
  }

  console.log(`Created: ${outputPath}`);
  console.log(`Modules: ${moduleSheets.length}`);
  console.log(`Test cases: ${testRows}`);
}

main().catch((error) => {
  console.error(error.message || error);
  process.exitCode = 1;
});
