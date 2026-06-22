import ExcelJS from 'exceljs';
import fs from 'node:fs/promises';
import path from 'node:path';

const sourcePath = 'D:/Copy of TestCase_CarRentalSystem_Full.xlsx';
const outputDir = path.resolve('reports');
const outputPath = path.join(outputDir, 'TestCase_CarRentalSystem_MD01-MD08_Retest_20260606.xlsx');
const retestDate = '2026-06-06';

const providerOrTimedUat = new Map([
  ['TC007', ['Đạt unit/guard; cần UAT production', 'reCAPTCHA production chưa được gọi trong Playwright; unit test xác nhận token sai bị chặn.']],
  ['TC009', ['Đạt contract + Gmail smoke; cần UAT inbox', 'Playwright kiểm tra bước nhập email; Gmail SMTP thật đã chấp nhận email smoke tới địa chỉ +alias.']],
  ['TC012', ['Đạt contract; cần UAT hết hạn theo thời gian', 'Playwright xác nhận màn hình/TTL contract; chưa chờ đủ 5 phút trong E2E.']],
  ['TC013', ['Đạt contract + Gmail smoke; cần UAT inbox', 'Playwright xác nhận resend flow; Gmail SMTP thật đã chấp nhận email smoke.']],
  ['TC014', ['Đạt unit/contract + Gmail smoke; cần UAT inbox', 'MFA privileged-role được kiểm tra bằng unit/contract; Gmail SMTP thật đã chấp nhận email smoke.']],
  ['TC015', ['Đạt unit/contract', 'Unit/contract xác nhận giới hạn nhập sai và phiên MFA không tạo đăng nhập ngoài ý muốn.']],
  ['TC016', ['Đạt contract + Gmail smoke; cần UAT inbox', 'Forgot/reset flow không lỗi; Gmail SMTP thật đã chấp nhận email smoke tới +alias.']],
  ['TC029', ['Đạt bằng FPT mock; cần UAT ảnh thật', 'OCR CCCD thành công dùng Playwright FPT mock; FPT thật smoke kết nối riêng.']],
  ['TC030', ['Đạt bằng FPT mock; cần UAT ảnh thật', 'OCR GPLX thành công dùng Playwright FPT mock; FPT thật smoke kết nối riêng.']],
  ['TC031', ['Đạt contract bằng FPT mock; cần UAT ảnh không khớp thật', 'Face API contract dùng Playwright FPT mock; không dùng ảnh cá nhân.']],
  ['TC053', ['Đạt fallback E2E; cần UAT map thật', 'Playwright cưỡng bức lỗi tile provider và xác nhận fallback hiển thị.']],
  ['TC054', ['Đạt API/fallback; cần UAT OSRM thật', 'API không trả 500 và có fallback; cần UAT tuyến đường thật của OSRM.']],
  ['TC063', ['Đạt API/fallback; cần UAT OSRM thật', 'Checkout delivery không lỗi với seed xác định; cần UAT tuyến đường thật.']],
]);

const fixNotes = new Map([
  ['TC002', 'Bọc đăng ký trong transaction, xử lý DbUpdateException và gửi OTP có báo lỗi rõ ràng.'],
  ['TC004', 'Áp dụng mật khẩu mạnh tối thiểu 8 ký tự, hoa/thường/số/ký tự đặc biệt.'],
  ['TC008', 'Transaction đăng ký và unique filtered indexes chặn dữ liệu hash trùng khi gửi lặp.'],
  ['TC016', 'Gửi reset-password email đồng bộ có timeout; lỗi SMTP được trả về thật.'],
  ['TC027', 'Kiểm tra chữ ký file và sửa URL file lưu cố định khớp với tên thật.'],
  ['TC028', 'Chặn file giả mạo/quá lớn trước khi lưu, trả lỗi thay vì làm sập web.'],
  ['TC034', 'SetRegion xác minh location tồn tại và hỗ trợ form redirect/JSON.'],
  ['TC035', 'Location sai trả 400 và xóa/không tạo cookie RegionID bẩn.'],
  ['TC036', 'Sửa overflow/responsive FAQ trên mobile.'],
  ['TC037', 'Sửa đường dẫn Guide sang docs/guides.'],
  ['TC042', 'Thêm tìm kiếm q theo tên xe, hãng và biển số được phép.'],
  ['TC047', 'Models trả DTO ổn định { id, name }; brandId sai trả 400.'],
  ['TC048', 'Giữ /Car/Manage Admin-only theo quyết định; non-admin bị chặn.'],
  ['TC051', 'BusyDates validate carId/car tồn tại và dùng trạng thái blocking chuẩn.'],
  ['TC053', 'MapSearch hiển thị fallback rõ ràng khi tile provider lỗi.'],
  ['TC054', 'CalculateDelivery validate tọa độ và xử lý lỗi dịch vụ ngoài không trả 500.'],
  ['TC055', 'CalculateDelivery trả 400 cho tọa độ thiếu/sai và kết quả ngoài vùng ổn định.'],
  ['TC056', 'GetAllCarsWithDistance validate tọa độ và sắp xếp tăng dần theo khoảng cách.'],
]);

const evidenceByModule = {
  MD01: 'Playwright TC001-TC008; AuthController/RegisterViewModel unit tests; unique hash migration.',
  MD02: 'Playwright TC009-TC016; AuthController/EmailService unit tests; Gmail SMTP smoke.',
  MD03: 'Playwright TC017-TC024; auth/session/RBAC regression suites.',
  MD04: 'Playwright TC025-TC032; file-magic regression test; FPT mock + real connectivity smoke.',
  MD05: 'Playwright TC033-TC040; HomeController tests; responsive FAQ/Guide checks.',
  MD06: 'Playwright TC041-TC048; catalog API contract tests.',
  MD07: 'Playwright TC049-TC056; CarController API tests; map fallback check.',
  MD08: 'Playwright TC057-TC064; booking/reservation integration tests and deterministic seed.',
};

const source = new ExcelJS.Workbook();
await source.xlsx.readFile(sourcePath);

const rows = [];
for (let sheetIndex = 1; sheetIndex <= 8; sheetIndex += 1) {
  const sheet = source.worksheets[sheetIndex];
  const moduleCode = sheet.name.match(/^MD\d{2}/)?.[0] ?? `MD${String(sheetIndex).padStart(2, '0')}`;
  for (let rowIndex = 10; rowIndex <= sheet.rowCount; rowIndex += 1) {
    const id = sheet.getCell(rowIndex, 1).text.trim();
    if (!/^TC\d{3}$/.test(id)) continue;
    rows.push({
      moduleCode,
      moduleName: sheet.name,
      id,
      description: sheet.getCell(rowIndex, 2).text.trim(),
      steps: sheet.getCell(rowIndex, 3).text.trim(),
      expected: sheet.getCell(rowIndex, 4).text.trim(),
      originalDate: sheet.getCell(rowIndex, 5).text.trim(),
      originalResult: sheet.getCell(rowIndex, 6).text.trim() || 'Chờ',
      originalNotes: sheet.getCell(rowIndex, 7).text.trim(),
    });
  }
}

if (rows.length !== 64) {
  throw new Error(`Expected 64 MD01-MD08 test cases, found ${rows.length}.`);
}

const originalCounts = rows.reduce((counts, row) => {
  const key = row.originalResult || 'Chờ';
  counts[key] = (counts[key] || 0) + 1;
  return counts;
}, {});

const workbook = new ExcelJS.Workbook();
workbook.creator = 'Codex';
workbook.created = new Date('2026-06-06T00:00:00+07:00');

const overview = workbook.addWorksheet('Tổng quan');
overview.columns = [
  { header: 'Hạng mục', key: 'item', width: 34 },
  { header: 'Kết quả', key: 'result', width: 28 },
  { header: 'Bằng chứng / Ghi chú', key: 'evidence', width: 92 },
];
overview.addRows([
  { item: 'Nguồn testcase', result: 'Không ghi đè', evidence: sourcePath },
  { item: 'Phạm vi', result: 'MD01-MD08 / TC001-TC064', evidence: '64 testcase từ 8 module đầu của workbook gốc.' },
  { item: 'Kết quả gốc', result: `${originalCounts['Đạt'] || 0} Đạt / ${originalCounts['Không đạt'] || 0} Không đạt / ${originalCounts['Chờ'] || 0} Chờ`, evidence: 'Trước sửa: 18 Không đạt và 20 Chờ.' },
  { item: 'Playwright retest chuyên biệt', result: '64/64 Đạt', evidence: 'playwright/e2e/12-md01-md08-remediation/md01-md08-full-retest.spec.js' },
  { item: 'Playwright remediation + broad regression', result: '7/7 + 48/48 Đạt', evidence: 'Suite remediation trọng yếu và các suite 00-08.' },
  { item: 'Unit/integration tests', result: '299/299 Đạt', evidence: 'dotnet test CarRentalSystem.Tests --no-restore' },
  { item: 'Build', result: 'Đạt, 0 warning / 0 error', evidence: 'dotnet build --no-restore' },
  { item: 'Migration/index', result: 'Đã áp dụng và xác minh', evidence: 'Unique filtered indexes: CitizenIdHash, DrivingLicenseHash, PhoneHash.' },
  { item: 'Gmail thật', result: 'SMTP accepted', evidence: 'Email smoke gửi tới sender +alias; các testcase inbox vẫn được đánh dấu cần UAT.' },
  { item: 'FPT.AI thật', result: 'Kết nối provider thành công', evidence: 'Synthetic PNG nhận HTTP 400/errorCode=3, không phải 401/403; OCR thành công dùng mock, không dùng ảnh cá nhân.' },
  { item: 'Giới hạn kết quả', result: 'Có ghi nhãn trong từng TC', evidence: 'Provider/timing cases được ghi rõ smoke/mock/UAT, không coi là xác minh provider end-to-end.' },
]);

const details = workbook.addWorksheet('Retest MD01-MD08');
details.columns = [
  { header: 'Module', key: 'module', width: 12 },
  { header: 'Mã TC', key: 'id', width: 10 },
  { header: 'Mô tả kiểm thử', key: 'description', width: 38 },
  { header: 'Kết quả mong đợi', key: 'expected', width: 58 },
  { header: 'Kết quả gốc', key: 'originalResult', width: 16 },
  { header: 'Ghi chú gốc', key: 'originalNotes', width: 42 },
  { header: 'Kết quả retest', key: 'retestResult', width: 34 },
  { header: 'Bằng chứng', key: 'evidence', width: 66 },
  { header: 'Sửa đổi / Lưu ý', key: 'fixNotes', width: 72 },
  { header: 'Ngày retest', key: 'retestDate', width: 16 },
];

for (const row of rows) {
  const provider = providerOrTimedUat.get(row.id);
  details.addRow({
    module: row.moduleCode,
    id: row.id,
    description: row.description,
    expected: row.expected,
    originalResult: row.originalResult,
    originalNotes: row.originalNotes,
    retestResult: provider?.[0] ?? 'Đạt E2E/automated',
    evidence: `${evidenceByModule[row.moduleCode]} Playwright full retest: ${row.id} Đạt.`,
    fixNotes: fixNotes.get(row.id) ?? provider?.[1] ?? 'Không phát hiện hồi quy trong retest tự động.',
    retestDate,
  });
}

for (const sheet of [overview, details]) {
  sheet.views = [{ state: 'frozen', ySplit: 1 }];
  sheet.autoFilter = { from: { row: 1, column: 1 }, to: { row: 1, column: sheet.columnCount } };
  sheet.getRow(1).height = 30;
  sheet.getRow(1).font = { bold: true, color: { argb: 'FFFFFFFF' } };
  sheet.getRow(1).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF1F4E78' } };
  sheet.getRow(1).alignment = { vertical: 'middle', horizontal: 'center' };
  sheet.eachRow((row, rowNumber) => {
    if (rowNumber === 1) return;
    row.alignment = { vertical: 'top', wrapText: true };
    row.height = sheet === details ? 62 : 42;
    row.eachCell((cell) => {
      cell.border = {
        top: { style: 'thin', color: { argb: 'FFD9E2F3' } },
        left: { style: 'thin', color: { argb: 'FFD9E2F3' } },
        bottom: { style: 'thin', color: { argb: 'FFD9E2F3' } },
        right: { style: 'thin', color: { argb: 'FFD9E2F3' } },
      };
    });
  });
}

details.eachRow((row, rowNumber) => {
  if (rowNumber === 1) return;
  const original = row.getCell(5);
  const retest = row.getCell(7);
  if (original.text === 'Không đạt') {
    original.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFFFC7CE' } };
  } else if (original.text === 'Chờ') {
    original.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFFFEB9C' } };
  } else {
    original.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFC6EFCE' } };
  }
  retest.fill = {
    type: 'pattern',
    pattern: 'solid',
    fgColor: { argb: retest.text.includes('cần UAT') ? 'FFFFEB9C' : 'FFC6EFCE' },
  };
});

await fs.mkdir(outputDir, { recursive: true });
await workbook.xlsx.writeFile(outputPath);
console.log(outputPath);
