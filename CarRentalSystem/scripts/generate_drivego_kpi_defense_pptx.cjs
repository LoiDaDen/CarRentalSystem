'use strict';

const fs = require('fs');
const path = require('path');
const pptxgen = require('pptxgenjs');

const pptx = new pptxgen();
pptx.defineLayout({ name: 'DRIVEGO_WIDE', width: 13.333, height: 7.5 });
pptx.layout = 'DRIVEGO_WIDE';
pptx.author = 'DriveGo Team';
pptx.company = 'DriveGo';
pptx.subject = 'DriveGo startup marketplace and six-month KPI defense';
pptx.title = 'DriveGo - KPI 6 thang - Demo 10 phut';
pptx.lang = 'vi-VN';
pptx.theme = {
  headFontFace: 'Aptos Display',
  bodyFontFace: 'Aptos',
  lang: 'vi-VN',
};

const W = 13.333;
const H = 7.5;
const C = {
  navy: '0B1F3A',
  blue: '1D4ED8',
  cyan: '0891B2',
  green: '059669',
  orange: 'F59E0B',
  red: 'DC2626',
  ink: '111827',
  muted: '64748B',
  pale: 'F8FAFC',
  softBlue: 'EFF6FF',
  softGreen: 'ECFDF5',
  softOrange: 'FFFBEB',
  line: 'CBD5E1',
  white: 'FFFFFF',
};

const logo = path.join(__dirname, '..', 'wwwroot', 'images', 'car_rental_logo.png');
const output = path.join(__dirname, '..', 'artifacts', 'DriveGo_KPI_6_THANG_DEMO_10_PHUT.pptx');
const outputD = 'D:\\DriveGo_KPI_6_THANG_DEMO_10_PHUT.pptx';

function base(slide, section, appendix = false) {
  slide.background = { color: C.pale };
  slide.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: W, h: H, fill: { color: C.pale }, line: { color: C.pale } });
  slide.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: W, h: 0.12, fill: { color: appendix ? C.orange : C.blue }, line: { color: appendix ? C.orange : C.blue } });
  slide.addText(section, { x: 0.55, y: 7.12, w: 4.5, h: 0.18, fontSize: 7.5, bold: true, color: C.muted, margin: 0 });
  slide.addText(appendix ? 'PHỤ LỤC Q&A' : 'DRIVEGO • DEMO 10 PHÚT', { x: 9.25, y: 7.12, w: 3.5, h: 0.18, fontSize: 7.5, bold: true, color: appendix ? C.orange : C.blue, align: 'right', margin: 0 });
}

function heading(slide, title, subtitle) {
  slide.addText(title, { x: 0.62, y: 0.42, w: 11.9, h: 0.58, fontSize: 25, bold: true, color: C.navy, margin: 0, fit: 'shrink' });
  if (subtitle) slide.addText(subtitle, { x: 0.65, y: 1.04, w: 11.8, h: 0.32, fontSize: 10.8, color: C.muted, margin: 0, fit: 'shrink' });
}

function card(slide, x, y, w, h, fill = C.white, line = C.line) {
  slide.addShape(pptx.ShapeType.roundRect, {
    x, y, w, h, rectRadius: 0.08,
    fill: { color: fill },
    line: { color: line, width: 1 },
    shadow: { type: 'outer', color: '94A3B8', opacity: 0.12, blur: 1.5, angle: 45, distance: 1 },
  });
}

function label(slide, x, y, text, fill = C.softBlue, color = C.blue) {
  slide.addShape(pptx.ShapeType.roundRect, { x, y, w: Math.max(1.2, text.length * 0.075 + 0.35), h: 0.32, rectRadius: 0.08, fill: { color: fill }, line: { color: fill } });
  slide.addText(text, { x: x + 0.12, y: y + 0.07, w: Math.max(0.9, text.length * 0.075 + 0.1), h: 0.16, fontSize: 7.8, bold: true, color, margin: 0 });
}

function bullet(slide, x, y, text, w = 4.7, color = C.ink, size = 11) {
  slide.addShape(pptx.ShapeType.ellipse, { x, y: y + 0.08, w: 0.09, h: 0.09, fill: { color: C.blue }, line: { color: C.blue } });
  slide.addText(text, { x: x + 0.2, y, w, h: 0.35, fontSize: size, color, margin: 0, fit: 'shrink' });
}

function metric(slide, x, y, w, value, title, note, color = C.blue) {
  card(slide, x, y, w, 1.18);
  slide.addShape(pptx.ShapeType.rect, { x, y, w: 0.08, h: 1.18, fill: { color }, line: { color } });
  slide.addText(value, { x: x + 0.22, y: y + 0.16, w: w - 0.35, h: 0.35, fontSize: 20, bold: true, color, margin: 0, fit: 'shrink' });
  slide.addText(title, { x: x + 0.22, y: y + 0.58, w: w - 0.35, h: 0.2, fontSize: 9.2, bold: true, color: C.ink, margin: 0, fit: 'shrink' });
  slide.addText(note, { x: x + 0.22, y: y + 0.84, w: w - 0.35, h: 0.18, fontSize: 7.5, color: C.muted, margin: 0, fit: 'shrink' });
}

function arrow(slide, x, y, w = 0.55, color = C.blue) {
  slide.addShape(pptx.ShapeType.rightArrow, { x, y, w, h: 0.32, fill: { color }, line: { color } });
}

function processBox(slide, x, y, w, title, note, color = C.blue) {
  card(slide, x, y, w, 1.12, C.white, color);
  slide.addText(title, { x: x + 0.14, y: y + 0.18, w: w - 0.28, h: 0.25, fontSize: 11, bold: true, color, align: 'center', margin: 0, fit: 'shrink' });
  slide.addText(note, { x: x + 0.14, y: y + 0.55, w: w - 0.28, h: 0.35, fontSize: 7.8, color: C.muted, align: 'center', margin: 0, fit: 'shrink' });
}

// Slide 1
{
  const slide = pptx.addSlide();
  base(slide, '01 • Bài toán và giải pháp');
  if (fs.existsSync(logo)) slide.addImage({ path: logo, x: 0.62, y: 0.48, w: 0.72, h: 0.72, transparency: 0 });
  slide.addText('DRIVEGO', { x: 1.5, y: 0.46, w: 4, h: 0.5, fontSize: 27, bold: true, color: C.navy, margin: 0 });
  slide.addText('Marketplace thuê xe tự lái có kiểm soát giao dịch', { x: 1.52, y: 1.02, w: 7.4, h: 0.3, fontSize: 12.5, color: C.muted, margin: 0 });
  card(slide, 0.72, 1.75, 3.35, 3.8, C.softOrange, C.orange);
  card(slide, 9.25, 1.75, 3.35, 3.8, C.softBlue, C.blue);
  card(slide, 4.62, 2.15, 4.1, 3.0, C.navy, C.navy);
  slide.addText('CHỦ XE', { x: 1.05, y: 2.05, w: 2.7, h: 0.34, fontSize: 18, bold: true, color: C.orange, align: 'center', margin: 0 });
  bullet(slide, 1.1, 2.72, 'Xe nhàn rỗi nhưng khó tìm khách đáng tin cậy', 2.45, C.ink, 10);
  bullet(slide, 1.1, 3.48, 'Thiếu quy trình duyệt xe, hợp đồng và đối soát', 2.45, C.ink, 10);
  slide.addText('KHÁCH THUÊ', { x: 9.58, y: 2.05, w: 2.7, h: 0.34, fontSize: 18, bold: true, color: C.blue, align: 'center', margin: 0 });
  bullet(slide, 9.63, 2.72, 'Khó kiểm tra xe, lịch trống và chất lượng', 2.45, C.ink, 10);
  bullet(slide, 9.63, 3.48, 'Thanh toán, bàn giao và tranh chấp phân tán', 2.45, C.ink, 10);
  slide.addText('DRIVEGO', { x: 5.25, y: 2.65, w: 2.85, h: 0.4, fontSize: 23, bold: true, color: C.white, align: 'center', margin: 0 });
  slide.addText('Kết nối nguồn cung và nhu cầu\nKiểm soát booking từ đầu đến cuối\nThu hoa hồng khi giao dịch hoàn thành', { x: 5.18, y: 3.35, w: 3.0, h: 1.05, fontSize: 11.5, color: C.white, align: 'center', margin: 0, breakLine: false, fit: 'shrink' });
  arrow(slide, 4.05, 3.38, 0.55, C.orange);
  arrow(slide, 8.7, 3.38, 0.55, C.blue);
  label(slide, 0.75, 6.2, 'THÔNG ĐIỆP: OWNER → DRIVEGO → CUSTOMER', C.softGreen, C.green);
}

// Slide 2
{
  const slide = pptx.addSlide();
  base(slide, '02 • Mô hình doanh thu');
  heading(slide, '50 triệu đồng là doanh thu hoa hồng, không phải GMV', 'Mỗi chỉ số tài chính có một ý nghĩa riêng và phải được đối soát độc lập.');
  card(slide, 0.75, 1.72, 11.85, 1.35, C.navy, C.navy);
  slide.addText('250 TRIỆU GMV', { x: 1.15, y: 2.08, w: 2.8, h: 0.38, fontSize: 22, bold: true, color: C.white, align: 'center', margin: 0 });
  slide.addText('×', { x: 4.25, y: 2.05, w: 0.55, h: 0.4, fontSize: 24, bold: true, color: C.orange, align: 'center', margin: 0 });
  slide.addText('20% HOA HỒNG', { x: 4.95, y: 2.08, w: 2.8, h: 0.38, fontSize: 22, bold: true, color: C.orange, align: 'center', margin: 0 });
  slide.addText('=', { x: 8.0, y: 2.05, w: 0.55, h: 0.4, fontSize: 24, bold: true, color: C.green, align: 'center', margin: 0 });
  slide.addText('50 TRIỆU DOANH THU', { x: 8.7, y: 2.08, w: 3.2, h: 0.38, fontSize: 22, bold: true, color: '6EE7B7', align: 'center', margin: 0 });
  metric(slide, 0.8, 3.55, 3.65, 'GMV Completed', 'Tổng TotalPrice của booking Completed', 'Không phải doanh thu DriveGo', C.blue);
  metric(slide, 4.85, 3.55, 3.65, 'Hoa hồng DriveGo', 'TotalPrice × CommissionRate', 'Nguồn doanh thu chính của nền tảng', C.green);
  metric(slide, 8.9, 3.55, 3.65, 'Ledger đã hạch toán', 'Dòng tiền đã ghi sổ', 'Không được gọi nhầm là GMV', C.orange);
  label(slide, 0.8, 5.35, 'KẾ HOẠCH: 80 BOOKING × 70% = 56 COMPLETED', C.softBlue, C.blue);
  slide.addText('Giá trị trung bình cần đạt: khoảng 4,46 triệu đồng / booking Completed', { x: 0.83, y: 5.92, w: 8.3, h: 0.3, fontSize: 13, bold: true, color: C.navy, margin: 0 });
  slide.addText('Ưu tiên chuyến thuê nhiều ngày', { x: 9.25, y: 5.72, w: 3.1, h: 0.62, fontSize: 13, bold: true, color: C.white, align: 'center', valign: 'mid', margin: 0, fill: { color: C.green }, shape: pptx.ShapeType.roundRect });
}

// Slide 3
{
  const slide = pptx.addSlide();
  base(slide, '03 • KPI sáu tháng');
  heading(slide, 'Funnel KPI có định nghĩa đo lường rõ ràng', 'MỤC TIÊU / DỮ LIỆU MÔ PHỎNG — không phải kết quả kinh doanh thực tế.');
  const values = [
    ['100', 'Người dùng', C.blue],
    ['30', 'Xe được duyệt', C.cyan],
    ['80', 'Booking hợp lệ', C.orange],
    ['56', 'Booking Completed', C.green],
    ['250tr', 'GMV Completed', C.blue],
    ['50tr', 'Hoa hồng DriveGo', C.green],
  ];
  values.forEach((item, i) => {
    const x = 0.65 + i * 2.08;
    metric(slide, x, 1.75, 1.75, item[0], item[1], i === 3 ? '70% kế hoạch' : 'Mục tiêu tháng 1-6', item[2]);
    if (i < values.length - 1) arrow(slide, x + 1.78, 2.15, 0.28, C.line);
  });
  card(slide, 0.8, 3.45, 5.75, 2.25, C.white, C.green);
  slide.addText('TỶ LỆ GIAO DỊCH THÀNH CÔNG', { x: 1.15, y: 3.82, w: 5.0, h: 0.3, fontSize: 15, bold: true, color: C.green, align: 'center', margin: 0 });
  slide.addText('Completed', { x: 2.65, y: 4.35, w: 2.0, h: 0.32, fontSize: 18, bold: true, color: C.navy, align: 'center', margin: 0 });
  slide.addShape(pptx.ShapeType.line, { x: 2.25, y: 4.76, w: 2.8, h: 0, line: { color: C.navy, width: 1.5 } });
  slide.addText('Completed + Cancelled + Rejected + Expired', { x: 1.25, y: 4.9, w: 4.8, h: 0.3, fontSize: 10.3, color: C.navy, align: 'center', margin: 0, fit: 'shrink' });
  card(slide, 6.8, 3.45, 5.75, 2.25, C.white, C.orange);
  slide.addText('MỨC ĐỘ HÀI LÒNG', { x: 7.15, y: 3.82, w: 5.0, h: 0.3, fontSize: 15, bold: true, color: C.orange, align: 'center', margin: 0 });
  slide.addText('Review đã duyệt 4-5 sao', { x: 8.05, y: 4.35, w: 3.2, h: 0.32, fontSize: 16, bold: true, color: C.navy, align: 'center', margin: 0 });
  slide.addShape(pptx.ShapeType.line, { x: 8.0, y: 4.76, w: 3.3, h: 0, line: { color: C.navy, width: 1.5 } });
  slide.addText('Tổng review đã duyệt', { x: 8.25, y: 4.9, w: 2.8, h: 0.3, fontSize: 11, color: C.navy, align: 'center', margin: 0 });
  label(slide, 0.8, 6.13, 'Không tính Confirmed/Active là thành công; không tính review chưa duyệt', C.softOrange, C.red);
}

// Slide 4
{
  const slide = pptx.addSlide();
  base(slide, '04 • Luồng tạo doanh thu');
  heading(slide, 'Một booking xuyên suốt bốn điểm demo', 'Chỉ mở các màn hình trực tiếp phục vụ nguồn cung, giao dịch, hoàn thành và đo KPI.');
  const boxes = [
    ['Owner đăng xe', '/OwnerCars/Create', C.orange],
    ['Customer đặt xe', '/Booking/MyBookings', C.blue],
    ['Staff bàn giao', '/Staff/Handover', C.cyan],
    ['Booking Completed', 'Có kết quả cuối cùng', C.green],
    ['KPI & hoa hồng', '/Admin/StartupKpi', C.green],
  ];
  boxes.forEach((b, i) => {
    const x = 0.55 + i * 2.58;
    processBox(slide, x, 2.05, 2.15, b[0], b[1], b[2]);
    if (i < boxes.length - 1) arrow(slide, x + 2.2, 2.44, 0.3, C.line);
  });
  card(slide, 0.75, 3.85, 3.7, 1.55, C.softOrange, C.orange);
  slide.addText('NGUỒN CUNG', { x: 1.05, y: 4.18, w: 3.05, h: 0.26, fontSize: 15, bold: true, color: C.orange, align: 'center', margin: 0 });
  slide.addText('Owner onboarding + Admin duyệt xe\n→ KPI 30 xe', { x: 1.08, y: 4.62, w: 3.0, h: 0.45, fontSize: 10, color: C.ink, align: 'center', margin: 0, fit: 'shrink' });
  card(slide, 4.8, 3.85, 3.7, 1.55, C.softBlue, C.blue);
  slide.addText('CHUYỂN ĐỔI', { x: 5.1, y: 4.18, w: 3.05, h: 0.26, fontSize: 15, bold: true, color: C.blue, align: 'center', margin: 0 });
  slide.addText('Search + Booking Hold + checkout\n→ KPI booking hợp lệ', { x: 5.13, y: 4.62, w: 3.0, h: 0.45, fontSize: 10, color: C.ink, align: 'center', margin: 0, fit: 'shrink' });
  card(slide, 8.85, 3.85, 3.7, 1.55, C.softGreen, C.green);
  slide.addText('CHẤT LƯỢNG', { x: 9.15, y: 4.18, w: 3.05, h: 0.26, fontSize: 15, bold: true, color: C.green, align: 'center', margin: 0 });
  slide.addText('Handover + Return + review\n→ 70% thành công, >80% hài lòng', { x: 9.18, y: 4.62, w: 3.0, h: 0.45, fontSize: 10, color: C.ink, align: 'center', margin: 0, fit: 'shrink' });
  label(slide, 0.75, 6.1, 'Không mở menu kỹ thuật không liên kết trực tiếp với KPI trong 10 phút', C.softOrange, C.red);
}

// Slide 5
{
  const slide = pptx.addSlide();
  base(slide, '05 • Dashboard KPI và bằng chứng');
  heading(slide, 'Dashboard mới chứng minh cách đo, không giả vờ đã đạt KPI', 'Route Admin: /Admin/StartupKpi • số liệu được tính trực tiếp từ database trong kỳ đo.');
  metric(slide, 0.7, 1.65, 2.85, 'Completed / outcome', 'Tỷ lệ thành công', 'Không tính trạng thái đang xử lý', C.green);
  metric(slide, 3.72, 1.65, 2.85, 'Σ TotalPrice', 'GMV Completed', 'Tách khỏi doanh thu nền tảng', C.blue);
  metric(slide, 6.74, 1.65, 2.85, 'Σ Price × Rate', 'Hoa hồng DriveGo', 'Mặc định 20% theo từng xe', C.green);
  metric(slide, 9.76, 1.65, 2.85, '4-5★ / approved', 'Mức độ hài lòng', 'Chỉ review đã duyệt', C.orange);
  card(slide, 0.72, 3.28, 7.2, 2.45, C.white, C.blue);
  slide.addText('BA LỚP TÀI CHÍNH TRÊN DASHBOARD', { x: 1.05, y: 3.65, w: 6.5, h: 0.28, fontSize: 15, bold: true, color: C.navy, margin: 0 });
  bullet(slide, 1.05, 4.15, 'GMV Completed: giá trị thuê đã hoàn thành', 6.1, C.ink, 11);
  bullet(slide, 1.05, 4.65, 'Hoa hồng DriveGo: doanh thu theo CommissionRate', 6.1, C.ink, 11);
  bullet(slide, 1.05, 5.15, 'Ledger đã hạch toán: dòng tiền posted, trình bày riêng', 6.1, C.ink, 11);
  card(slide, 8.25, 3.28, 4.35, 2.45, C.navy, C.navy);
  slide.addText('BẰNG CHỨNG KIỂM THỬ', { x: 8.62, y: 3.65, w: 3.6, h: 0.28, fontSize: 15, bold: true, color: C.white, align: 'center', margin: 0 });
  slide.addText('✓ 375/375 .NET pass · 15/06/2026\n✓ Trạng thái bị loại\n✓ Mẫu số bằng 0\n✓ GMV và commission\n✓ Satisfaction\n✓ Route chỉ dành cho Admin', { x: 8.87, y: 4.08, w: 3.2, h: 1.35, fontSize: 10.2, color: C.white, margin: 0, breakLine: false, fit: 'shrink' });
  label(slide, 0.72, 6.13, 'Dữ liệu local = bằng chứng công thức và luồng; không phải kết quả kinh doanh thật', C.softOrange, C.red);
}

// Slide 6
{
  const slide = pptx.addSlide();
  base(slide, '06 • Kế hoạch tăng trưởng và pilot');
  heading(slide, 'Kế hoạch sáu tháng tăng dần, pilot một tuần kiểm chứng giả định', 'Tổng kế hoạch: 100 người dùng • 30 xe • 80 booking • 56 Completed • 250 triệu GMV • 50 triệu hoa hồng.');
  const rows = [
    ['T1', '15', '5', '5', '3', '10tr', '2tr'],
    ['T2', '15', '5', '8', '6', '20tr', '4tr'],
    ['T3', '15', '5', '10', '7', '30tr', '6tr'],
    ['T4', '20', '5', '14', '10', '45tr', '9tr'],
    ['T5', '17', '5', '18', '12', '60tr', '12tr'],
    ['T6', '18', '5', '25', '18', '85tr', '17tr'],
    ['TỔNG', '100', '30', '80', '56', '250tr', '50tr'],
  ];
  slide.addTable([
    [{ text: 'Tháng', options: { bold: true } }, { text: 'User', options: { bold: true } }, { text: 'Xe', options: { bold: true } }, { text: 'Booking', options: { bold: true } }, { text: 'Completed', options: { bold: true } }, { text: 'GMV', options: { bold: true } }, { text: 'Hoa hồng', options: { bold: true } }],
    ...rows,
  ], {
    x: 0.7, y: 1.65, w: 7.25, h: 4.55,
    border: { type: 'solid', color: C.line, width: 0.7 },
    fill: C.white, color: C.ink, fontSize: 9.5,
    rowH: 0.48, margin: 0.05,
    colW: [0.8, 0.75, 0.65, 1.05, 1.15, 1.05, 1.25],
    bold: false,
  });
  card(slide, 8.35, 1.65, 4.25, 4.55, C.navy, C.navy);
  slide.addText('PILOT MỘT TUẦN', { x: 8.75, y: 2.05, w: 3.45, h: 0.34, fontSize: 18, bold: true, color: C.white, align: 'center', margin: 0 });
  const pilot = [
    ['5', 'chủ xe quan tâm'],
    ['8', 'xe trong pipeline'],
    ['20', 'phản hồi khách thuê'],
    ['10', 'đăng ký thử nghiệm'],
    ['3', 'booking test đầu-cuối'],
  ];
  pilot.forEach((item, i) => {
    slide.addText(item[0], { x: 8.8, y: 2.72 + i * 0.58, w: 0.55, h: 0.28, fontSize: 15, bold: true, color: C.orange, align: 'right', margin: 0 });
    slide.addText(item[1], { x: 9.55, y: 2.74 + i * 0.58, w: 2.35, h: 0.25, fontSize: 10.5, color: C.white, margin: 0 });
  });
  label(slide, 0.72, 6.38, 'Pilot xác thực giả định; không được gọi là đã đạt KPI sáu tháng', C.softOrange, C.red);
}

// Slide 7
{
  const slide = pptx.addSlide();
  base(slide, '07 • Kết luận');
  heading(slide, 'DriveGo chứng minh năng lực tạo, kiểm soát và đo giao dịch', 'Kết luận trung thực: hệ thống đã có nền tảng demo; KPI kinh doanh cần được xác thực bằng pilot và vận hành thật.');
  const summaries = [
    ['KẾT NỐI', 'Owner đăng xe\nCustomer đặt thuê', C.orange],
    ['KIỂM SOÁT', 'Hold • Payment\nHandover • Return', C.blue],
    ['TẠO DOANH THU', 'Completed booking\n× 20% hoa hồng', C.green],
    ['ĐO LƯỜNG', 'KPI dashboard\nPilot có bằng chứng', C.cyan],
  ];
  summaries.forEach((item, i) => {
    const x = 0.65 + i * 3.16;
    card(slide, x, 1.72, 2.75, 2.25, C.white, item[2]);
    slide.addText(item[0], { x: x + 0.3, y: 2.1, w: 2.15, h: 0.3, fontSize: 15, bold: true, color: item[2], align: 'center', margin: 0 });
    slide.addText(item[1], { x: x + 0.36, y: 2.75, w: 2.03, h: 0.68, fontSize: 11, color: C.ink, align: 'center', margin: 0, breakLine: false, fit: 'shrink' });
  });
  card(slide, 0.75, 4.45, 7.55, 1.3, C.softOrange, C.orange);
  slide.addText('GIỚI HẠN ĐƯỢC CÔNG BỐ RÕ', { x: 1.05, y: 4.78, w: 6.9, h: 0.28, fontSize: 14, bold: true, color: C.red, margin: 0 });
  slide.addText('Chưa có doanh thu thật • Chưa production-ready • Vendor/thiết bị thật cần kiểm chứng • Pilot không thay thế KPI sáu tháng', { x: 1.05, y: 5.2, w: 6.9, h: 0.3, fontSize: 10, color: C.ink, margin: 0, fit: 'shrink' });
  slide.addShape(pptx.ShapeType.roundRect, { x: 8.75, y: 4.45, w: 3.85, h: 1.3, rectRadius: 0.1, fill: { color: C.navy }, line: { color: C.navy } });
  slide.addText('250 TRIỆU GMV\n→ 50 TRIỆU HOA HỒNG', { x: 9.05, y: 4.78, w: 3.25, h: 0.62, fontSize: 16, bold: true, color: C.white, align: 'center', margin: 0, breakLine: false, fit: 'shrink' });
  slide.addText('CẢM ƠN & Q&A', { x: 4.2, y: 6.25, w: 4.9, h: 0.34, fontSize: 18, bold: true, color: C.navy, align: 'center', margin: 0 });
}

// Appendix 1
{
  const slide = pptx.addSlide();
  base(slide, 'A1 • Kiến trúc hỗ trợ KPI', true);
  heading(slide, 'Kiến trúc hỗ trợ vòng đời giao dịch và đo KPI', 'Dùng khi hội đồng hỏi sâu; không trình bày trong 10 phút chính.');
  const layers = [
    ['GIAO DIỆN', 'Customer • Owner • Staff • Admin', C.blue],
    ['NGHIỆP VỤ', 'Hold • Booking • Handover • Review', C.cyan],
    ['ĐO LƯỜNG', 'StartupKpiService • RevenueMetrics', C.green],
    ['DỮ LIỆU', 'EF Core • SQL Server • Ledger', C.orange],
  ];
  layers.forEach((l, i) => {
    card(slide, 1.15 + i * 3.0, 2.05, 2.55, 2.4, C.white, l[2]);
    slide.addText(l[0], { x: 1.4 + i * 3.0, y: 2.47, w: 2.05, h: 0.3, fontSize: 15, bold: true, color: l[2], align: 'center', margin: 0 });
    slide.addText(l[1], { x: 1.45 + i * 3.0, y: 3.1, w: 1.95, h: 0.65, fontSize: 10.5, color: C.ink, align: 'center', margin: 0, fit: 'shrink' });
    if (i < layers.length - 1) arrow(slide, 3.74 + i * 3.0, 3.05, 0.35, C.line);
  });
  label(slide, 1.15, 5.25, 'Dashboard KPI đọc dữ liệu nghiệp vụ; không suy diễn từ screenshot hoặc số liệu slide', C.softGreen, C.green);
}

// Appendix 2
{
  const slide = pptx.addSlide();
  base(slide, 'A2 • Kiểm thử và giới hạn', true);
  heading(slide, 'Kiểm thử công thức KPI và phạm vi trung thực', 'Đã chạy lại ngày 15/06/2026: 375/375 .NET test pass; không công bố Playwright vì chưa có log mới.');
  const tests = [
    ['Công thức thành công', 'Completed / kết quả cuối cùng'],
    ['Trạng thái bị loại', 'Draft, Unpaid và trạng thái đang xử lý'],
    ['Tài chính', 'GMV, commission và ledger tách riêng'],
    ['Hài lòng', 'Review duyệt 4-5 sao / review duyệt'],
    ['Phân quyền', 'Route KPI yêu cầu Admin policy'],
  ];
  tests.forEach((t, i) => {
    card(slide, 0.8 + (i % 3) * 4.1, 1.65 + Math.floor(i / 3) * 1.75, 3.55, 1.32, C.white, i === 4 ? C.orange : C.green);
    slide.addText(t[0], { x: 1.05 + (i % 3) * 4.1, y: 1.95 + Math.floor(i / 3) * 1.75, w: 3.05, h: 0.24, fontSize: 12.5, bold: true, color: i === 4 ? C.orange : C.green, margin: 0 });
    slide.addText(t[1], { x: 1.05 + (i % 3) * 4.1, y: 2.35 + Math.floor(i / 3) * 1.75, w: 3.05, h: 0.3, fontSize: 9, color: C.ink, margin: 0, fit: 'shrink' });
  });
  label(slide, 0.8, 5.5, '375/375 .NET pass; không tuyên bố production-ready, doanh thu thật, Playwright pass hoặc vendor live', C.softOrange, C.red);
}

// Appendix 3
{
  const slide = pptx.addSlide();
  base(slide, 'A3 • Q&A nhanh', true);
  heading(slide, 'Câu trả lời ngắn cần thống nhất trong nhóm', 'Mỗi câu trả lời tối đa 20 giây.');
  const qas = [
    ['50 triệu là gì?', 'Doanh thu hoa hồng DriveGo; cần 250 triệu GMV ở mức 20%.'],
    ['Vì sao chỉ tính Completed?', 'Vì Confirmed/Active vẫn có thể hủy hoặc phát sinh sự cố.'],
    ['Dashboard có phải số thật?', 'Công thức tính thật từ database local; dữ liệu local là dữ liệu demo.'],
    ['Làm sao chứng minh khả thi?', 'Unit economics + kế hoạch theo tháng + pilot nhỏ có bằng chứng.'],
  ];
  qas.forEach((qa, i) => {
    const x = i % 2 === 0 ? 0.85 : 6.85;
    const y = i < 2 ? 1.65 : 4.05;
    card(slide, x, y, 5.6, 1.65, C.white, i === 0 ? C.green : C.blue);
    slide.addText(qa[0], { x: x + 0.3, y: y + 0.3, w: 5.0, h: 0.28, fontSize: 14, bold: true, color: C.navy, margin: 0 });
    slide.addText(qa[1], { x: x + 0.3, y: y + 0.82, w: 5.0, h: 0.38, fontSize: 10, color: C.ink, margin: 0, fit: 'shrink' });
  });
}

async function main() {
  fs.mkdirSync(path.dirname(output), { recursive: true });
  await pptx.writeFile({ fileName: output });
  fs.copyFileSync(output, outputD);
  console.log(JSON.stringify({ output, outputD, slides: pptx._slides.length }, null, 2));
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
