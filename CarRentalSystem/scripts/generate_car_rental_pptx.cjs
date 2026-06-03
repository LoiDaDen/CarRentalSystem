'use strict';

const pptxgen = require('pptxgenjs');
const path = require('path');

const pptx = new pptxgen();
pptx.layout = 'LAYOUT_WIDE';
pptx.author = 'CarRentalSystem';
pptx.company = 'CarRentalSystem';
pptx.subject = 'SmartRental Management System presentation';
pptx.title = 'SmartRental - He thong quan ly cho thue xe';
pptx.lang = 'vi-VN';
pptx.theme = {
  headFontFace: 'Aptos Display',
  bodyFontFace: 'Aptos',
  lang: 'vi-VN',
};
pptx.defineLayout({ name: 'CUSTOM_WIDE', width: 13.333, height: 7.5 });
pptx.layout = 'CUSTOM_WIDE';

const C = {
  navy: '06285B',
  navy2: '0A3A7A',
  teal: '18B7B0',
  teal2: '6FE7DD',
  red: 'D94B4B',
  orange: 'F3A43B',
  green: '2CA85F',
  ink: '101828',
  muted: '667085',
  pale: 'F3FAFF',
  line: 'CFE7F2',
  white: 'FFFFFF',
  softBlue: 'EAF7FF',
  softTeal: 'E7FBF8',
};

const W = 13.333;
const H = 7.5;
const logoPath = path.join(__dirname, '..', 'wwwroot', 'images', 'car_rental_logo.png');

function addBackground(slide, variant = 'plain') {
  slide.background = { color: variant === 'blue' ? 'F1FAFF' : 'F8FCFF' };
  slide.addShape(pptx.ShapeType.rect, {
    x: 0, y: 0, w: W, h: H,
    fill: { color: variant === 'blue' ? 'F1FAFF' : 'F8FCFF' },
    line: { color: variant === 'blue' ? 'F1FAFF' : 'F8FCFF' },
  });

  // Technical grid accents similar to the provided sample.
  for (let i = 0; i < 18; i += 1) {
    slide.addShape(pptx.ShapeType.line, {
      x: 9.2 + i * 0.12, y: 0.05,
      w: 1.8, h: 1.2 + i * 0.02,
      line: { color: 'BFE8EE', transparency: 60, width: 0.5, dash: 'dash' },
      rotate: 36,
    });
  }
  for (let x = 0.2; x < 3.8; x += 0.28) {
    for (let y = 6.1; y < 7.35; y += 0.22) {
      slide.addShape(pptx.ShapeType.ellipse, {
        x, y, w: 0.025, h: 0.025,
        fill: { color: '7CCAD4', transparency: 45 },
        line: { color: '7CCAD4', transparency: 100 },
      });
    }
  }
  slide.addText('CarRentalSystem', {
    x: 11.15, y: 7.12, w: 1.8, h: 0.18,
    fontFace: 'Aptos', fontSize: 7.5, color: C.ink, bold: true,
    align: 'right', margin: 0,
  });
}

function title(slide, text, y = 0.52, size = 26) {
  slide.addText(text, {
    x: 0.7, y, w: 12, h: 0.65,
    fontFace: 'Aptos Display', fontSize: size, bold: true,
    color: C.navy, margin: 0, breakLine: false,
    fit: 'shrink',
  });
}

function subtitle(slide, text, y) {
  slide.addText(text, {
    x: 0.73, y, w: 11.5, h: 0.35,
    fontFace: 'Aptos', fontSize: 12.5, color: C.muted,
    margin: 0, fit: 'shrink',
  });
}

function pill(slide, x, y, text, color = C.softBlue, textColor = C.navy) {
  slide.addShape(pptx.ShapeType.roundRect, {
    x, y, w: Math.max(1.15, text.length * 0.085 + 0.35), h: 0.34,
    rectRadius: 0.12,
    fill: { color },
    line: { color, transparency: 100 },
  });
  slide.addText(text, {
    x: x + 0.13, y: y + 0.065, w: Math.max(0.8, text.length * 0.085 + 0.1), h: 0.18,
    fontFace: 'Aptos', fontSize: 8.5, bold: true, color: textColor, margin: 0,
  });
}

function card(slide, x, y, w, h, opts = {}) {
  slide.addShape(pptx.ShapeType.roundRect, {
    x, y, w, h,
    rectRadius: opts.radius || 0.1,
    fill: { color: opts.fill || C.white, transparency: opts.transparency || 0 },
    line: { color: opts.line || C.line, width: opts.lineWidth || 1 },
    shadow: opts.shadow === false ? undefined : { type: 'outer', color: '9DB5C8', opacity: 0.14, blur: 2, angle: 45, distance: 2 },
  });
}

function bulletList(slide, x, y, items, opts = {}) {
  const gap = opts.gap || 0.48;
  items.forEach((item, idx) => {
    const yy = y + idx * gap;
    slide.addShape(pptx.ShapeType.ellipse, {
      x, y: yy + 0.07, w: 0.11, h: 0.11,
      fill: { color: opts.dot || C.teal },
      line: { color: opts.dot || C.teal },
    });
    slide.addText(item, {
      x: x + 0.23, y: yy, w: opts.w || 4.6, h: opts.h || 0.34,
      fontFace: 'Aptos', fontSize: opts.size || 12.5, color: opts.color || C.ink,
      bold: opts.bold || false, margin: 0, fit: 'shrink',
    });
  });
}

function sectionLabel(slide, x, y, text, color = C.teal) {
  slide.addShape(pptx.ShapeType.rect, { x, y, w: 0.08, h: 0.42, fill: { color }, line: { color } });
  slide.addText(text, { x: x + 0.18, y: y - 0.02, w: 5, h: 0.34, fontSize: 15, bold: true, color: C.navy, margin: 0, fit: 'shrink' });
}

function arrow(slide, x, y, w, h, color = C.teal) {
  slide.addShape(pptx.ShapeType.rightArrow, {
    x, y, w, h,
    fill: { color, transparency: 8 },
    line: { color, transparency: 0, width: 1 },
  });
}

function roleCard(slide, x, y, titleText, tag, items, color) {
  card(slide, x, y, 3.05, 1.75, { line: color, lineWidth: 1.3 });
  pill(slide, x + 0.18, y + 0.16, titleText, color === C.orange ? 'FFF2D9' : C.softTeal, C.navy);
  slide.addText(tag, { x: x + 1.95, y: y + 0.23, w: 0.9, h: 0.2, fontSize: 8, color: C.muted, margin: 0, align: 'right' });
  bulletList(slide, x + 0.35, y + 0.62, items, { w: 2.4, size: 10.4, gap: 0.31, dot: color, h: 0.23 });
}

// Slide 1
{
  const slide = pptx.addSlide();
  addBackground(slide);
  card(slide, 1.9, 1.34, 9.55, 4.85, { fill: C.white, line: 'F2F6F8', shadow: true });
  slide.addText('SMARTRENTAL', {
    x: 3.5, y: 2.22, w: 6.4, h: 0.62,
    fontFace: 'Aptos Display', fontSize: 34, bold: true, color: C.navy,
    align: 'center', margin: 0, fit: 'shrink',
  });
  slide.addText('HỆ THỐNG QUẢN LÝ CHO THUÊ XE', {
    x: 2.45, y: 3.0, w: 8.5, h: 0.48,
    fontFace: 'Aptos Display', fontSize: 23.5, bold: true, color: C.navy,
    align: 'center', margin: 0, fit: 'shrink',
  });
  slide.addShape(pptx.ShapeType.line, { x: 2.55, y: 3.82, w: 8.25, h: 0, line: { color: C.teal, width: 3 } });
  slide.addText('Nền tảng số hóa quy trình thuê xe, thanh toán, hợp đồng và vận hành đội xe.', {
    x: 2.5, y: 4.08, w: 8.3, h: 0.32,
    fontSize: 14.2, color: C.muted, align: 'center', margin: 0, fit: 'shrink',
  });
  pill(slide, 2.35, 5.08, 'ASP.NET Core MVC', C.softBlue);
  pill(slide, 4.4, 5.08, 'SQL Server + EF Core', C.softBlue);
  pill(slide, 6.75, 5.08, 'PayOS, eKYC, Tracking', C.softBlue);
  pill(slide, 9.25, 5.08, 'Admin + Partner Portal', C.softBlue);
}

// Slide 2
{
  const slide = pptx.addSlide();
  addBackground(slide);
  title(slide, 'TỪ QUẢN LÝ THỦ CÔNG ĐẾN NỀN TẢNG SỐ TẬP TRUNG');
  card(slide, 0.7, 1.72, 5.75, 4.95, { line: 'F0B0B0', fill: 'FFF8F8' });
  slide.addShape(pptx.ShapeType.line, { x: 0.72, y: 1.72, w: 5.68, h: 0, line: { color: C.red, width: 3 } });
  slide.addText('Phương pháp truyền thống', { x: 1.15, y: 2.08, w: 4.6, h: 0.34, fontSize: 17, bold: true, color: C.navy, margin: 0 });
  bulletList(slide, 1.12, 2.82, [
    'Dữ liệu phân tán trên giấy tờ và Excel.',
    'Khó xác minh danh tính và bằng lái.',
    'Dễ trùng lịch xe, sai giá, thiếu kiểm soát.',
    'Hợp đồng giấy khó tra cứu và đối soát.',
  ], { dot: C.red, w: 4.6, gap: 0.72 });

  card(slide, 6.9, 1.72, 5.75, 4.95, { line: C.teal, fill: 'FFFFFF' });
  slide.addShape(pptx.ShapeType.line, { x: 6.92, y: 1.72, w: 5.68, h: 0, line: { color: C.teal, width: 3 } });
  slide.addText('Hệ thống SmartRental', { x: 7.35, y: 2.08, w: 4.6, h: 0.34, fontSize: 17, bold: true, color: C.navy, margin: 0 });
  bulletList(slide, 7.32, 2.82, [
    'Database SQL Server tập trung, đồng bộ real-time.',
    'eKYC/OCR, RBAC và luồng duyệt hồ sơ.',
    'Giữ chỗ, thanh toán, hoàn cọc và ledger.',
    'Hợp đồng số, bàn giao xe, audit và báo cáo.',
  ], { dot: C.teal, w: 4.55, gap: 0.72 });
  slide.addShape(pptx.ShapeType.rightArrow, { x: 5.95, y: 3.35, w: 1.35, h: 0.82, fill: { color: C.teal }, line: { color: C.teal } });
  slide.addText('Chuyển\nđổi', { x: 5.98, y: 3.43, w: 0.92, h: 0.48, fontSize: 13.5, bold: true, color: C.white, align: 'center', margin: 0 });
}

// Slide 3
{
  const slide = pptx.addSlide();
  addBackground(slide);
  title(slide, 'HỆ SINH THÁI ĐA TÁC NHÂN', 0.48, 29);
  subtitle(slide, 'Một core nghiệp vụ phục vụ đồng thời khách hàng, chủ xe, nhân viên vận hành và quản trị viên.', 1.24);
  slide.addShape(pptx.ShapeType.ellipse, { x: 5.36, y: 2.08, w: 2.4, h: 2.4, fill: { color: C.navy }, line: { color: '1F74C9', width: 2 }, shadow: { type: 'outer', color: '2E8BD6', opacity: 0.22, blur: 3, distance: 2 } });
  slide.addText('Trung tâm\nCore System', { x: 5.75, y: 2.62, w: 1.62, h: 0.7, fontSize: 16, bold: true, color: C.white, align: 'center', margin: 0 });
  slide.addText('Xử lý booking, thanh toán,\nhợp đồng, audit và dữ liệu.', { x: 5.56, y: 3.35, w: 1.98, h: 0.48, fontSize: 9.8, color: 'D9F5FF', align: 'center', margin: 0 });
  roleCard(slide, 1.02, 2.18, 'Khách hàng', 'Customer', ['Đăng ký & eKYC', 'Tìm xe trên bản đồ', 'Đặt xe & thanh toán', 'Ký hợp đồng số'], C.teal);
  roleCard(slide, 9.25, 2.18, 'Đối tác / Chủ xe', 'Partner', ['Đăng xe lên sàn', 'Theo dõi trạng thái xe', 'Quản lý doanh thu', 'Nhận bàn giao'], C.orange);
  roleCard(slide, 5.12, 5.0, 'Quản trị viên', 'Admin', ['Duyệt hồ sơ', 'Phân quyền nhân viên', 'Giám sát hợp đồng', 'Báo cáo tổng quan'], C.navy2);
  arrow(slide, 4.22, 3.02, 0.9, 0.26); arrow(slide, 8.02, 3.02, 0.9, 0.26); arrow(slide, 6.38, 4.56, 0.28, 0.58);
}

// Slide 4
{
  const slide = pptx.addSlide();
  addBackground(slide, 'blue');
  title(slide, 'KIẾN TRÚC TỔNG QUAN HỆ THỐNG');
  subtitle(slide, 'ASP.NET Core MVC tổ chức theo Controllers, Services, Repositories, EF Core và các background jobs.', 1.13);
  const layers = [
    { x: 0.8, y: 2.0, w: 2.2, h: 1.1, t: 'Web UI', s: 'MVC Views\nAdmin, Partner, Customer' },
    { x: 3.45, y: 2.0, w: 2.2, h: 1.1, t: 'Controllers', s: 'Booking, Payment,\nContract, Tracking' },
    { x: 6.1, y: 2.0, w: 2.2, h: 1.1, t: 'Services', s: 'Pricing, eKYC,\nLedger, Compliance' },
    { x: 8.75, y: 2.0, w: 2.2, h: 1.1, t: 'Repositories', s: 'Generic data\naccess boundary' },
    { x: 5.28, y: 4.35, w: 2.6, h: 1.18, t: 'SQL Server', s: 'EF Core models,\nmigrations, audit data' },
  ];
  layers.forEach((l, idx) => {
    card(slide, l.x, l.y, l.w, l.h, { line: idx === 4 ? C.navy2 : C.teal, fill: C.white });
    slide.addText(l.t, { x: l.x + 0.18, y: l.y + 0.16, w: l.w - 0.36, h: 0.28, fontSize: 14, bold: true, color: C.navy, align: 'center', margin: 0 });
    slide.addText(l.s, { x: l.x + 0.18, y: l.y + 0.52, w: l.w - 0.36, h: 0.4, fontSize: 9.4, color: C.muted, align: 'center', margin: 0, fit: 'shrink' });
  });
  [3.05, 5.7, 8.35].forEach((x) => arrow(slide, x, 2.36, 0.32, 0.28));
  slide.addShape(pptx.ShapeType.downArrow, { x: 6.48, y: 3.25, w: 0.38, h: 0.76, fill: { color: C.teal }, line: { color: C.teal } });
  sectionLabel(slide, 0.82, 5.78, 'Tích hợp vận hành');
  const integrations = ['PayOS payment', 'Email background job', 'Camera ANPR', 'PWA offline', 'SLO metrics'];
  integrations.forEach((t, i) => pill(slide, 2.6 + i * 1.85, 5.78, t, i % 2 ? C.softTeal : C.softBlue));
}

// Slide 5
{
  const slide = pptx.addSlide();
  addBackground(slide);
  title(slide, '[TÍNH NĂNG CỐT LÕI 1] XÁC THỰC & PHÂN QUYỀN RBAC', 0.45, 25);
  card(slide, 0.82, 3.02, 2.55, 1.75, { fill: C.white });
  slide.addText('Bước 1\nRequest', { x: 1.16, y: 3.32, w: 1.85, h: 0.58, fontSize: 16, bold: true, color: C.ink, align: 'center', margin: 0 });
  slide.addText('Người dùng đăng nhập,\nmật khẩu được hash.', { x: 1.1, y: 4.08, w: 1.95, h: 0.48, fontSize: 11, color: C.ink, align: 'center', margin: 0 });
  arrow(slide, 3.55, 3.7, 1.08, 0.28);
  slide.addShape(pptx.ShapeType.diamond, { x: 4.65, y: 3.08, w: 1.35, h: 1.35, fill: { color: C.white }, line: { color: C.line }, shadow: { type: 'outer', color: '9DB5C8', opacity: 0.16, blur: 2, distance: 2 } });
  slide.addText('Bước 2\nPhân tích\nRole', { x: 4.84, y: 3.28, w: 0.96, h: 0.75, fontSize: 12, bold: true, color: C.ink, align: 'center', margin: 0 });
  arrow(slide, 6.1, 3.7, 1.0, 0.28);
  const targets = [
    ['Khách hàng', 'Trang tìm kiếm & đặt xe', C.teal],
    ['Partner', 'Dashboard đội xe của tôi', C.orange],
    ['Admin', 'Bảng điều khiển quản trị', C.navy2],
  ];
  targets.forEach((r, i) => {
    const y = 2.28 + i * 1.3;
    card(slide, 7.2, y, 4.9, 0.78, { fill: C.white });
    slide.addText(r[0], { x: 7.55, y: y + 0.19, w: 1.4, h: 0.24, fontSize: 13, bold: true, color: C.ink, margin: 0 });
    slide.addText('Điều hướng tới', { x: 8.9, y: y + 0.21, w: 1.35, h: 0.22, fontSize: 11, color: C.ink, margin: 0 });
    slide.addText(r[1], { x: 10.05, y: y + 0.2, w: 1.65, h: 0.24, fontSize: 11.2, bold: true, color: r[2], margin: 0, fit: 'shrink' });
  });
}

// Slide 6
{
  const slide = pptx.addSlide();
  addBackground(slide);
  title(slide, '[TÍNH NĂNG CỐT LÕI 2] ĐỊNH DANH ĐIỆN TỬ BẰNG AI (eKYC)', 0.42, 25);
  const steps = [
    ['1.', 'Tải lên', 'CCCD, GPLX và ảnh selfie.'],
    ['2.', 'Nhận diện OCR', 'Bóc tách dữ liệu từ hình ảnh.'],
    ['3.', 'Đối chiếu', 'Khớp selfie, giấy tờ và hạn GPLX.'],
    ['4.', 'Phê duyệt', 'Admin đưa hồ sơ hợp lệ vào trạng thái xác minh.'],
  ];
  steps.forEach((s, i) => {
    const y = 1.62 + i * 1.15;
    slide.addShape(pptx.ShapeType.ellipse, { x: 1.15, y, w: 0.7, h: 0.7, fill: { color: C.softTeal }, line: { color: C.teal, width: 3 } });
    slide.addText(s[0], { x: 1.28, y: y + 0.16, w: 0.44, h: 0.24, fontSize: 16, bold: true, color: C.navy, align: 'center', margin: 0 });
    slide.addText(`${s[1]} - `, { x: 2.22, y: y + 0.1, w: 1.35, h: 0.26, fontSize: 13.5, bold: true, color: C.ink, margin: 0 });
    slide.addText(s[2], { x: 3.25, y: y + 0.1, w: 2.2, h: 0.44, fontSize: 12.2, color: C.ink, margin: 0, fit: 'shrink' });
  });
  slide.addShape(pptx.ShapeType.line, { x: 1.5, y: 2.28, w: 0, h: 3.22, line: { color: C.teal, width: 4 } });
  arrow(slide, 5.2, 3.12, 1.1, 0.54, C.orange);
  card(slide, 6.35, 1.48, 5.85, 4.75, { fill: C.white, line: C.line });
  slide.addShape(pptx.ShapeType.roundRect, { x: 6.7, y: 1.78, w: 5.15, h: 0.68, rectRadius: 0.08, fill: { color: C.navy }, line: { color: C.navy } });
  slide.addText('Hồ sơ eKYC', { x: 6.95, y: 1.98, w: 4.6, h: 0.24, fontSize: 14, bold: true, color: C.white, align: 'center', margin: 0 });
  const fields = ['CCCD mặt trước', 'CCCD mặt sau', 'GPLX mặt trước', 'GPLX mặt sau', 'Ảnh chân dung', 'Ảnh selfie'];
  fields.forEach((f, i) => {
    const x = 6.85 + (i % 2) * 2.48;
    const y = 2.78 + Math.floor(i / 2) * 0.78;
    slide.addShape(pptx.ShapeType.roundRect, { x, y, w: 2.2, h: 0.48, rectRadius: 0.04, fill: { color: 'FAFCFF' }, line: { color: '9AA4B2', dash: 'dash' } });
    slide.addText(f, { x: x + 0.14, y: y + 0.15, w: 1.9, h: 0.16, fontSize: 8.8, color: C.ink, align: 'center', margin: 0 });
  });
  slide.addShape(pptx.ShapeType.roundRect, { x: 8.25, y: 5.45, w: 2.0, h: 0.35, rectRadius: 0.05, fill: { color: C.green }, line: { color: C.green } });
  slide.addText('Tải lên ngay', { x: 8.25, y: 5.535, w: 2, h: 0.14, fontSize: 8.5, color: C.white, bold: true, align: 'center', margin: 0 });
}

// Slide 7
{
  const slide = pptx.addSlide();
  addBackground(slide, 'blue');
  title(slide, '[TÍNH NĂNG CỐT LÕI 3] ĐẶT XE, GIỮ CHỖ & THANH TOÁN');
  subtitle(slide, 'Luồng checkout bảo đảm xe không bị đặt trùng, giá được snapshot và thanh toán được đối soát.', 1.13);
  const flow = [
    ['Tìm xe', 'Lọc khu vực, thời gian,\nloại xe, giá thuê'],
    ['Giữ chỗ', 'BookingHold khóa xe\ntheo thời hạn'],
    ['Báo giá', 'SelfDrivePricing /\nWithDriverPricing'],
    ['Thanh toán', 'PayOS hold,\nledger, hoàn cọc'],
    ['Xác nhận', 'Booking confirmed,\nhóa đơn & email'],
  ];
  flow.forEach((f, i) => {
    const x = 0.7 + i * 2.45;
    card(slide, x, 2.38, 1.9, 1.35, { fill: C.white, line: i % 2 ? C.teal : C.navy2 });
    slide.addText(f[0], { x: x + 0.16, y: 2.65, w: 1.55, h: 0.24, fontSize: 14, bold: true, color: C.navy, align: 'center', margin: 0 });
    slide.addText(f[1], { x: x + 0.16, y: 3.02, w: 1.55, h: 0.42, fontSize: 8.8, color: C.muted, align: 'center', margin: 0, fit: 'shrink' });
    if (i < flow.length - 1) arrow(slide, x + 1.93, 2.9, 0.44, 0.28);
  });
  sectionLabel(slide, 0.8, 5.0, 'Điểm kiểm soát nghiệp vụ');
  bulletList(slide, 1.0, 5.62, [
    'Kiểm tra trạng thái xe, lịch bảo trì và lịch bàn giao trước khi giữ chỗ.',
    'Ghi nhận ledger cho tiền thuê, tiền cọc, hoàn cọc và referral.',
    'Background jobs tự động hết hạn hold, polling payout và gửi email.',
  ], { w: 10.4, gap: 0.42, size: 11.5 });
}

// Slide 8
{
  const slide = pptx.addSlide();
  addBackground(slide);
  title(slide, '[TÍNH NĂNG CỐT LÕI 4] HỢP ĐỒNG SỐ & BÀN GIAO XE');
  const cols = [
    ['Hợp đồng số', ['DigitalContract', 'ContractVersion', 'Ký/duyệt trạng thái', 'Render hồ sơ tra cứu']],
    ['Bàn giao xe', ['InspectionRecord', 'HandoverRecord', 'Ảnh 4 góc xe', 'Biên bản nhận/trả xe']],
    ['Tuân thủ pháp lý', ['ConsentRecord', 'PrivacyRequest', 'PII encryption', 'Audit hash chain']],
  ];
  cols.forEach((c, i) => {
    const x = 0.82 + i * 4.12;
    card(slide, x, 1.75, 3.45, 4.55, { fill: C.white, line: i === 1 ? C.orange : C.teal });
    slide.addShape(pptx.ShapeType.line, { x: x + 0.18, y: 2.22, w: 3.05, h: 0, line: { color: i === 1 ? C.orange : C.teal, width: 2.4 } });
    slide.addText(c[0], { x: x + 0.28, y: 2.0, w: 2.8, h: 0.26, fontSize: 15.5, bold: true, color: C.navy, align: 'center', margin: 0 });
    bulletList(slide, x + 0.45, 2.85, c[1], { w: 2.45, gap: 0.58, size: 11.4, dot: i === 1 ? C.orange : C.teal });
  });
}

// Slide 9
{
  const slide = pptx.addSlide();
  addBackground(slide, 'blue');
  title(slide, 'MÔ HÌNH DỮ LIỆU TRỌNG TÂM', 0.45, 26);
  subtitle(slide, 'Các entity chính trong EF Core tập trung quanh người dùng, xe, đơn thuê, thanh toán và hợp đồng.', 1.08);
  const entities = [
    ['Users', 1.0, 2.0, C.navy2],
    ['Cars', 3.0, 2.0, C.teal],
    ['Bookings', 5.0, 2.0, C.orange],
    ['Payments', 7.25, 2.0, C.green],
    ['Contracts', 9.5, 2.0, C.navy2],
    ['Drivers', 3.0, 4.25, C.teal],
    ['Handover', 5.0, 4.25, C.orange],
    ['Audit Logs', 7.25, 4.25, C.navy2],
    ['Areas', 9.5, 4.25, C.teal],
  ];
  entities.forEach(([name, x, y, color]) => {
    card(slide, x, y, 1.55, 0.72, { fill: C.white, line: color, lineWidth: 1.4 });
    slide.addText(name, { x: x + 0.12, y: y + 0.22, w: 1.31, h: 0.18, fontSize: 11.5, bold: true, color: C.navy, align: 'center', margin: 0 });
  });
  const lines = [
    [2.55, 2.36, 0.35, 0], [4.55, 2.36, 0.35, 0], [6.55, 2.36, 0.6, 0], [8.8, 2.36, 0.6, 0],
    [3.78, 2.72, 0, 1.2], [5.78, 2.72, 0, 1.2], [8.05, 2.72, 0, 1.2], [10.25, 2.72, 0, 1.2],
  ];
  lines.forEach(([x, y, w, h]) => slide.addShape(pptx.ShapeType.line, { x, y, w, h, line: { color: C.teal, width: 1.4, beginArrowType: 'none', endArrowType: 'triangle' } }));
  sectionLabel(slide, 0.95, 6.1, 'Thiết kế hỗ trợ mở rộng: soft delete, snapshot giá, audit trail, privacy request và dữ liệu đa khu vực.');
}

// Slide 10
{
  const slide = pptx.addSlide();
  addBackground(slide);
  title(slide, 'CÔNG NGHỆ, BẢO MẬT & VẬN HÀNH');
  const cards = [
    ['Nền tảng', ['ASP.NET Core MVC', 'EF Core + SQL Server', 'Bootstrap, PWA, SignalR'], C.teal],
    ['Bảo mật', ['RBAC theo role/khu vực', 'PII encryption, audit log', 'Rate limit, MFA-ready'], C.navy2],
    ['Tích hợp', ['PayOS payment/payout', 'OCR/eKYC provider', 'Camera ANPR webhook'], C.orange],
    ['Vận hành', ['Background jobs', 'SLO metrics & runbooks', 'Staging/production checklist'], C.green],
  ];
  cards.forEach((c, i) => {
    const x = 0.9 + (i % 2) * 5.85;
    const y = 1.75 + Math.floor(i / 2) * 2.35;
    card(slide, x, y, 5.0, 1.72, { fill: C.white, line: c[2] });
    slide.addText(c[0], { x: x + 0.28, y: y + 0.22, w: 4.3, h: 0.26, fontSize: 16, bold: true, color: C.navy, margin: 0 });
    bulletList(slide, x + 0.35, y + 0.72, c[1], { w: 4.1, size: 10.6, gap: 0.33, dot: c[2] });
  });
}

// Slide 11
{
  const slide = pptx.addSlide();
  addBackground(slide, 'blue');
  slide.addText('KẾT LUẬN', { x: 0.75, y: 0.72, w: 11.8, h: 0.52, fontSize: 30, bold: true, color: C.navy, align: 'center', margin: 0 });
  slide.addText('SmartRental chuyển quy trình thuê xe thành một nền tảng số có kiểm soát từ đầu đến cuối.', {
    x: 1.45, y: 1.42, w: 10.5, h: 0.35, fontSize: 14, color: C.muted, align: 'center', margin: 0,
  });
  const summary = [
    ['Tập trung dữ liệu', 'Một nguồn dữ liệu cho xe, khách hàng, booking, hợp đồng và thanh toán.'],
    ['Giảm rủi ro', 'eKYC, RBAC, ledger và audit giúp kiểm soát giao dịch và danh tính.'],
    ['Tối ưu vận hành', 'Admin/Partner portal, tracking, báo cáo và background jobs hỗ trợ mở rộng.'],
  ];
  summary.forEach((s, i) => {
    const x = 1.05 + i * 3.85;
    card(slide, x, 2.38, 3.35, 2.0, { fill: C.white, line: i === 1 ? C.orange : C.teal });
    slide.addText(s[0], { x: x + 0.24, y: 2.82, w: 2.85, h: 0.3, fontSize: 15, bold: true, color: C.navy, align: 'center', margin: 0 });
    slide.addText(s[1], { x: x + 0.35, y: 3.32, w: 2.65, h: 0.58, fontSize: 10.5, color: C.muted, align: 'center', margin: 0, fit: 'shrink' });
  });
  slide.addShape(pptx.ShapeType.roundRect, { x: 4.22, y: 5.18, w: 4.9, h: 0.72, rectRadius: 0.12, fill: { color: C.navy }, line: { color: C.navy } });
  slide.addText('CẢM ƠN & HỎI ĐÁP', { x: 4.22, y: 5.38, w: 4.9, h: 0.25, fontSize: 17, bold: true, color: C.white, align: 'center', margin: 0 });
}

pptx.writeFile({ fileName: path.join(__dirname, '..', 'artifacts', 'SmartRental_CarRentalSystem_Presentation.pptx') });
