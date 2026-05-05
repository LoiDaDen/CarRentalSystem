'use strict';
const ExcelJS = require('exceljs');

async function buildSprintBacklogExcel() {
    const wb = new ExcelJS.Workbook();
    wb.creator = 'Car Rental System - Nhóm Y5';
    wb.created = new Date();

    const C = {
        darkBg: '0d1117',
        white: 'ffffff',
        lightGray: 'e6edf3',
        border: '30363d',
        rowAlt: '21262d',
        rowMain: '161b22',
        green: '16a34a',
        blue: '1a56db',
        purple: '7c3aed',
        orange: 'ea580c',
        red: 'dc2626',
        cyan: '0891b2',
        yellow: 'f59e0b',
    };

    function setTitleCell(cell, text) {
        cell.value = text;
        cell.font = { size: 15, bold: true, color: { argb: 'FFFFFFFF' } };
        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF0d1117' } };
        cell.alignment = { horizontal: 'center', vertical: 'middle' };
    }

    function setHeaderRow(ws, rowNum, headers, fills) {
        const row = ws.getRow(rowNum);
        row.height = 30;
        headers.forEach((h, i) => {
            const cell = row.getCell(i + 1);
            cell.value = h;
            cell.font = { size: 11, bold: true, color: { argb: 'FFFFFFFF' } };
            cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF' + fills[i] } };
            cell.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
            cell.border = {
                top: { style: 'thin', color: { argb: 'FF30363d' } },
                left: { style: 'thin', color: { argb: 'FF30363d' } },
                bottom: { style: 'thin', color: { argb: 'FF30363d' } },
                right: { style: 'thin', color: { argb: 'FF30363d' } },
            };
        });
    }

    function setDataCell(cell, value, fillColor, textColor, bold, align) {
        if (textColor === undefined) textColor = 'FFFFFFFF';
        if (bold === undefined) bold = false;
        if (align === undefined) align = 'center';
        cell.value = value;
        cell.font = { size: 10, bold: bold, color: { argb: 'FF' + textColor } };
        if (fillColor) {
            cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF' + fillColor } };
        }
        cell.alignment = { horizontal: align, vertical: 'middle', wrapText: true };
        cell.border = {
            top: { style: 'thin', color: { argb: 'FF30363d' } },
            left: { style: 'thin', color: { argb: 'FF30363d' } },
            bottom: { style: 'thin', color: { argb: 'FF30363d' } },
            right: { style: 'thin', color: { argb: 'FF30363d' } },
        };
    }

    function addTitleRow(ws, text, colCount) {
        ws.mergeCells('A1:' + String.fromCharCode(64 + colCount) + '1');
        const titleCell = ws.getCell('A1');
        setTitleCell(titleCell, text);
        ws.getRow(1).height = 38;
    }

    const priorityColors = { 'P0-Critical': 'dc2626', 'P1-High': 'ea580c', 'P2-Medium': 'f59e0b', 'P3-Low': '16a34a' };
    const roleColors = { 'R/A': '16a34a', 'R': '0891b2', 'C': '7c3aed', 'I': '8b949e' };

    // ======================== SHEET 1: TỔNG QUAN ========================
    const ws1 = wb.addWorksheet('1. Tổng Quản', {
        properties: { tabColor: { argb: 'FF1a56db' } },
        views: [{ state: 'frozen', xSplit: 0, ySplit: 3 }]
    });
    ws1.columns = [
        { width: 10 }, { width: 28 }, { width: 16 }, { width: 16 }, { width: 10 }, { width: 14 }, { width: 55 },
    ];

    addTitleRow(ws1, 'SPRINT OVERVIEW — CAR RENTAL SYSTEM (10/03/2026 — 24/04/2026)', 7);
    setHeaderRow(ws1, 2, ['Sprint', 'Thời gian', 'Ngày bắt đầu', 'Ngày kết thúc', 'Số ngày', 'Tổng giờ', 'Mục tiêu Sprint'], [C.blue, C.blue, C.blue, C.blue, C.blue, C.blue, C.blue]);

    var sprints = [
        { sprint: 'Sprint 1', time: '11 ngày', start: '10/03/2026', end: '20/03/2026', days: '11', hours: '102', goal: 'Nền tảng hệ thống: Database, Authentication, Authorization, PII, Audit Log, Project Setup, Reference Data, Hangfire, Admin Layout, Documentation' },
        { sprint: 'Sprint 2', time: '13 ngày', start: '21/03/2026', end: '04/04/2026', days: '13', hours: '238', goal: 'Tính năng cốt lõi: Car Management, Booking System, Cart, Payment (payOS), Wallet, Add-ons, Handover, Admin Booking/Car/Payment, Unit Tests, API Docs' },
        { sprint: 'Sprint 3', time: '11 ngày', start: '05/04/2026', end: '15/04/2026', days: '11', hours: '232', goal: 'Tính năng mở rộng: Partner Portal, Driver Service, Digital Contract, Long-term Rental, PromoCode, Membership, Area Management, Unit Tests, User Manual' },
        { sprint: 'Sprint 4', time: '9 ngày', start: '16/04/2026', end: '24/04/2026', days: '9', hours: '200', goal: 'Hoàn thiện & Vận hành: Admin Dashboard, Reviews, Maintenance, GPS Tracking, eKYC, Alert System, Integration Test, UAT, Bug Fixing, Final Docs, Demo' },
        { sprint: 'TỔNG', time: '46 ngày', start: '10/03/2026', end: '24/04/2026', days: '44', hours: '772', goal: 'Hoàn thành toàn bộ hệ thống Car Rental System MVP' },
    ];

    var sprintFillColors = [C.blue, C.purple, C.cyan, C.orange, C.green];
    sprints.forEach(function(s, i) {
        var rowNum = i + 3;
        var row = ws1.getRow(rowNum);
        row.height = 55;
        var isTotal = s.sprint === 'TỔNG';
        var fillColor = isTotal ? C.green : (sprintFillColors[i] || C.blue);
        setDataCell(row.getCell(1), s.sprint, fillColor, C.white, isTotal);
        setDataCell(row.getCell(2), s.time, fillColor, C.white, isTotal);
        setDataCell(row.getCell(3), s.start, fillColor, C.white, isTotal);
        setDataCell(row.getCell(4), s.end, fillColor, C.white, isTotal);
        setDataCell(row.getCell(5), s.days, fillColor, C.white, isTotal);
        setDataCell(row.getCell(6), s.hours, fillColor, C.white, isTotal);
        setDataCell(row.getCell(7), s.goal, fillColor, C.white, isTotal, 'left');
    });

    // ======================== SHEET 2: TEAM ========================
    var ws2 = wb.addWorksheet('2. Team & Roles', {
        properties: { tabColor: { argb: 'FF16a34a' } },
        views: [{ state: 'frozen', xSplit: 0, ySplit: 3 }]
    });
    ws2.columns = [
        { width: 20 }, { width: 18 }, { width: 10 }, { width: 10 }, { width: 10 }, { width: 10 }, { width: 65 },
    ];

    addTitleRow(ws2, 'TEAM ROLES & RESPONSIBILITIES — CAR RENTAL SYSTEM', 7);
    setHeaderRow(ws2, 2, ['Họ tên', 'Vai trò', 'Sprint 1', 'Sprint 2', 'Sprint 3', 'Sprint 4', 'Trách nhiệm chính'], [C.green, C.green, C.blue, C.purple, C.cyan, C.orange, C.green]);

    var team = [
        { name: 'Lợi', role: 'PO-Dev', s1: '38h', s2: '90h', s3: '100h', s4: '70h', resp: 'Product Owner + Developer chính. Thiết kế database, authentication, pricing engine, booking flow, payment payOS, digital contract, eKYC, security, demo. Đồng thời quản lý backlog, ưu tiên tính năng, quyết định product direction.' },
        { name: 'Tuấn', role: 'Dev', s1: '36h', s2: '80h', s3: '100h', s4: '65h', resp: 'Developer. Xây dựng Car CRUD/Catalog/Search, Map search, Cart, Admin pages, Partner portal, Driver portal, Reports, Dashboard. Hỗ trợ tích hợp payOS, Hangfire, performance test, bug fixing, demo.' },
        { name: 'Huy', role: 'Tester', s1: '10h', s2: '30h', s3: '26h', s4: '48h', resp: 'Quality Assurance. Viết unit test (Auth, Car, Booking, Payment, Partner, Promo), viết test case chi tiết cho tất cả module, integration test, E2E test, performance test, security test, bug reporting & tracking.' },
        { name: 'Vũ', role: 'Tài liệu', s1: '12h', s2: '12h', s3: '8h', s4: '20h', resp: 'Documentation. Lập hồ sơ dự án (SRS), hướng dẫn cài đặt, viết API documentation, database schema docs, user manual, final documentation, project presentation docs, deployment guide.' },
        { name: 'TỔNG', role: '', s1: '96h', s2: '212h', s3: '234h', s4: '203h', resp: 'Tổng: 745 giờ làm việc' },
    ];

    var teamColors = [C.blue, C.purple, C.cyan, C.orange, C.green];
    team.forEach(function(t, i) {
        var rowNum = i + 3;
        var row = ws2.getRow(rowNum);
        row.height = 65;
        var isTotal = t.name === 'TỔNG';
        var fillColor = isTotal ? C.green : (teamColors[i] || C.blue);
        setDataCell(row.getCell(1), t.name, fillColor, C.white, true);
        setDataCell(row.getCell(2), t.role, fillColor, C.white, true);
        setDataCell(row.getCell(3), t.s1, fillColor, C.white, isTotal);
        setDataCell(row.getCell(4), t.s2, fillColor, C.white, isTotal);
        setDataCell(row.getCell(5), t.s3, fillColor, C.white, isTotal);
        setDataCell(row.getCell(6), t.s4, fillColor, C.white, isTotal);
        setDataCell(row.getCell(7), t.resp, fillColor, C.white, isTotal, 'left');
    });

    // ======================== SPRINT SHEET BUILDER ========================
    function buildSprintSheet(wb, sheetName, title, tabColor, headerFill, tasks) {
        var ws = wb.addWorksheet(sheetName, {
            properties: { tabColor: { argb: 'FF' + tabColor } },
            views: [{ state: 'frozen', xSplit: 0, ySplit: 3 }]
        });
        ws.columns = [
            { width: 10 }, { width: 36 }, { width: 55 }, { width: 14 },
            { width: 10 }, { width: 8 }, { width: 8 }, { width: 8 }, { width: 8 }, { width: 14 },
        ];

        addTitleRow(ws, title, 10);
        setHeaderRow(ws, 2, ['Task ID', 'Task', 'Mô tả', 'Priority', 'Estimate', 'Lợi', 'Tuấn', 'Huy', 'Vũ', 'Ghi chú'],
            [C.darkBg, headerFill, headerFill, headerFill, headerFill, headerFill, headerFill, headerFill, headerFill, headerFill]);

        tasks.forEach(function(t, i) {
            var rowNum = i + 3;
            var row = ws.getRow(rowNum);
            row.height = 50;
            var fillColor = i % 2 === 0 ? C.rowMain : C.rowAlt;

            setDataCell(row.getCell(1), t.id, fillColor, C.white, false);
            setDataCell(row.getCell(2), t.task, fillColor, C.white, true);
            setDataCell(row.getCell(3), t.desc, fillColor, C.lightGray, false, 'left');
            setDataCell(row.getCell(4), t.priority, priorityColors[t.priority] || fillColor, C.white);
            setDataCell(row.getCell(5), t.est, fillColor, C.white);
            setDataCell(row.getCell(6), t.loi, roleColors[t.loi] || fillColor, C.white);
            setDataCell(row.getCell(7), t.tuan, roleColors[t.tuan] || fillColor, C.white);
            setDataCell(row.getCell(8), t.huy, roleColors[t.huy] || fillColor, C.white);
            setDataCell(row.getCell(9), t.vu, roleColors[t.vu] || fillColor, C.white);
            setDataCell(row.getCell(10), t.note || '', fillColor, C.lightGray, false, 'left');
        });
    }

    // ======================== SPRINT 1 ========================
    var s1Tasks = [
        { id: 'S1-01', task: 'Thiết kế database schema', desc: 'Thiết kế toàn bộ bảng (Users, Roles, Cars, Bookings, Payments, Wallets, Reviews, Partners, Drivers, Areas, Contracts, MaintLog, Promos, Memberships), relationship, index, constraints, ERD', priority: 'P0-Critical', est: '8h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: 'Lợi thiết kế, Tuấn review' },
        { id: 'S1-02', task: 'Tạo project structure', desc: 'Setup ASP.NET Core MVC, folder structure (Controllers, Models, Views, Services, Repositories, Data, Middleware), NuGet packages', priority: 'P0-Critical', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S1-03', task: 'Thiết lập Entity Framework Core', desc: 'Migrations, DbContext, seed data cho lookup tables (CarBrand, CarType, FuelType, Transmission, Area)', priority: 'P0-Critical', est: '6h', loi: 'R/A', tuan: 'R', huy: 'I', vu: 'I', note: 'Tuấn làm EF, Lợi review' },
        { id: 'S1-04', task: 'Implement Authentication', desc: 'Đăng ký, đăng nhập, đăng xuất, cookie-based session, external login', priority: 'P0-Critical', est: '10h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S1-05', task: 'Email verification', desc: 'Gửi mail xác thực, verify token, resend verification', priority: 'P0-Critical', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S1-06', task: 'Forgot/Reset password', desc: 'Quên mật khẩu, reset token via email, đổi mật khẩu', priority: 'P0-Critical', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S1-07', task: 'User profile management', desc: 'CRUD profile, đổi avatar, địa chỉ, đổi PIN bảo vệ PII', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S1-08', task: 'PII Encryption (BCrypt)', desc: 'Mã hóa dữ liệu nhạy cảm (email, phone, address) trong DB bằng BCrypt', priority: 'P1-High', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S1-09', task: 'Role-based authorization', desc: 'Phân quyền: Admin, Customer, Partner, Driver. Policy-based authorization trong ASP.NET Core', priority: 'P0-Critical', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S1-10', task: 'Admin User Management', desc: 'CRUD người dùng, phân quyền, khóa/mở tài khoản, xem log hoạt động', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'R', huy: 'I', vu: 'I', note: 'Tuấn code, Lợi review' },
        { id: 'S1-11', task: 'Setup Hangfire', desc: 'Cấu hình background jobs, dashboard, retry policy', priority: 'P1-High', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S1-12', task: 'Setup PagedList helper', desc: 'Phân trang cho danh sách (Cars, Bookings, Users, Partners)', priority: 'P2-Medium', est: '2h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S1-13', task: 'Error handling middleware', desc: 'Xử lý exception toàn cục, custom error pages, logging', priority: 'P1-High', est: '3h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S1-14', task: 'Ghi audit log', desc: 'Ghi log hành động người dùng (CRUD, login, payment) vào AuditLog table', priority: 'P1-High', est: '4h', loi: 'R/A', tuan: 'R', huy: 'I', vu: 'I', note: '' },
        { id: 'S1-15', task: 'Admin Layout & Navigation', desc: 'Layout admin, sidebar, header, menu điều hướng admin', priority: 'P1-High', est: '4h', loi: 'R/A', tuan: 'R', huy: 'I', vu: 'I', note: '' },
        { id: 'S1-16', task: 'Reference data CRUD', desc: 'CarBrand, CarType, FuelType, Transmission, Area CRUD cho Admin', priority: 'P2-Medium', est: '6h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S1-17', task: 'Navigation & Footer', desc: 'Header (menu, user dropdown), footer (links, contact), layout chính cho customer', priority: 'P2-Medium', est: '3h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S1-18', task: 'Unit test — Auth', desc: 'Viết unit test cho authentication (register, login, forgot password, authorization)', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'R', huy: 'R/A', vu: 'I', note: 'Huy viết, Tuấn review' },
        { id: 'S1-19', task: 'Test case — Auth & User', desc: 'Viết test case chi tiết cho module Auth, User profile, phân quyền', priority: 'P1-High', est: '4h', loi: 'I', tuan: 'C', huy: 'R/A', vu: 'I', note: '' },
        { id: 'S1-20', task: 'Lập hồ sơ dự án', desc: 'Viết SRS (Software Requirements Specification), tổng quan dự án, mô tả hệ thống, use cases', priority: 'P1-High', est: '8h', loi: 'C', tuan: 'I', huy: 'I', vu: 'R/A', note: '' },
        { id: 'S1-21', task: 'Viết hướng dẫn cài đặt', desc: 'Setup guide, environment config, database setup, dependencies installation', priority: 'P1-High', est: '4h', loi: 'C', tuan: 'C', huy: 'I', vu: 'R/A', note: '' },
    ];
    buildSprintSheet(wb, 'Sprint 1 - Nền tảng', 'SPRINT 1 (10/03 — 20/03/2026) — Nền tảng hệ thống — Tổng: 102 giờ', '1a56db', '1a56db', s1Tasks);

    // ======================== SPRINT 2 ========================
    var s2Tasks = [
        { id: 'S2-01', task: 'Car CRUD Admin', desc: 'Thêm, sửa, xóa, duyệt xe từ Admin panel. Upload hình ảnh, trạng thái xe (Available, Rented, Maintenance)', priority: 'P0-Critical', est: '8h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-02', task: 'Car catalog (public)', desc: 'Danh sách xe công khai, phân trang, lọc cơ bản (loại xe, giá thuê)', priority: 'P0-Critical', est: '6h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-03', task: 'Car detail page', desc: 'Chi tiết xe: hình ảnh, thông số kỹ thuật, đánh giá, xe cùng loại', priority: 'P0-Critical', est: '6h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-04', task: 'Car search & filter', desc: 'Tìm kiếm theo location, ngày thuê, loại xe, giá, số chỗ, nhiên liệu, hộp số', priority: 'P0-Critical', est: '8h', loi: 'R/A', tuan: 'R', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-05', task: 'Map search (OpenStreetMap)', desc: 'Tìm kiếm xe theo bản đồ tương tác, hiển thị markers, cluster markers, click to select', priority: 'P1-High', est: '10h', loi: 'R/A', tuan: 'R', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-06', task: 'Dynamic pricing engine', desc: 'Tính giá theo ngày (ngày thường, cuối tuần), giờ thuê, mùa, holiday pricing', priority: 'P0-Critical', est: '8h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-07', task: 'Delivery fee calculator', desc: 'Tính phí giao xe tự động theo khoảng cách từ depot đến địa điểm khách', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-08', task: 'Booking flow (multi-step)', desc: 'Form đặt xe 3 bước: (1) Chọn xe & ngày, (2) Dịch vụ bổ sung, (3) Xác nhận & thanh toán', priority: 'P0-Critical', est: '12h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-09', task: 'Booking service logic', desc: 'Tính tổng chi phí: subtotal, delivery fee, add-ons, discount, grand total', priority: 'P0-Critical', est: '8h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-10', task: 'Add-ons services', desc: 'Các dịch vụ bổ sung: bảo hiểm, GPS, tài xế, ghế trẻ em, giao xe tận nơi', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-11', task: 'Booking confirmation & email', desc: 'Xác nhận đặt xe qua email, SMS notification', priority: 'P0-Critical', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-12', task: 'My Bookings page', desc: 'Danh sách booking của khách hàng, filter theo trạng thái, phân trang', priority: 'P0-Critical', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-13', task: 'Booking detail page', desc: 'Chi tiết booking: thông tin xe, ngày, chi phí, trạng thái, hành động (hủy, xác nhận)', priority: 'P0-Critical', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-14', task: 'Cancel booking', desc: 'Chức năng hủy booking, áp dụng cancellation policy, refund calculation', priority: 'P0-Critical', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-15', task: 'Booking status machine', desc: 'Cập nhật trạng thái: Pending → Confirmed → OnTheWay → OnTheRoad → Completed / Cancelled', priority: 'P0-Critical', est: '8h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-16', task: 'Pickup confirmation', desc: 'Xác nhận nhận xe: partner/driver xác nhận xe đã giao, khách xác nhận đã nhận', priority: 'P0-Critical', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-17', task: 'Return confirmation', desc: 'Xác nhận trả xe: khách xác nhận trả, partner/driver xác nhận đã nhận', priority: 'P0-Critical', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-18', task: 'Handover inspection record', desc: 'Bản ghi kiểm tra xe khi nhận và trả: hình ảnh, mô tả, chữ ký', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-19', task: 'Cart management', desc: 'Thêm, cập nhật số lượng, xóa xe khỏi giỏ. Persist cart trong DB', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-20', task: 'Cart pricing', desc: 'Tính tổng giá trị giỏ hàng, preview trước khi checkout', priority: 'P1-High', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-21', task: 'BookingTransaction', desc: 'Xử lý multi-booking transaction (một giỏ có thể chứa nhiều xe)', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-22', task: 'Wallet data model', desc: 'Thiết kế Wallet entity: balance, transactions, frozen amount', priority: 'P0-Critical', est: '3h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-23', task: 'Wallet top-up', desc: 'Nạp tiền vào ví qua payOS, cập nhật balance', priority: 'P0-Critical', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-24', task: 'Wallet payment', desc: 'Thanh toán bằng ví (trừ từ wallet balance)', priority: 'P0-Critical', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-25', task: 'Payment Gateway payOS', desc: 'Tích hợp payOS API: create payment, get QR code, webhook callback', priority: 'P0-Critical', est: '12h', loi: 'R/A', tuan: 'R', huy: 'I', vu: 'I', note: 'Lợi design + integrate, Tuấn backend support' },
        { id: 'S2-26', task: 'Payment callback', desc: 'Xử lý callback success/cancel từ payOS, update booking status, gửi email xác nhận', priority: 'P0-Critical', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-27', task: 'Invoice generation', desc: 'Tạo hóa đơn PDF cho booking: thông tin khách, xe, chi phí chi tiết', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-28', task: 'Admin Payment Management', desc: 'Dashboard quản lý thanh toán: danh sách giao dịch, trạng thái, filter, export', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-29', task: 'Admin Booking Management', desc: 'Dashboard quản lý booking: danh sách, cập nhật trạng thái, gán driver', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-30', task: 'Admin Car Approval', desc: 'Phê duyệt xe mới từ đối tác P2P trước khi hiển thị công khai', priority: 'P1-High', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S2-31', task: 'Unit test — Car', desc: 'Viết unit test cho CarService (search, pricing, CRUD)', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'R', huy: 'R/A', vu: 'I', note: '' },
        { id: 'S2-32', task: 'Unit test — Booking', desc: 'Viết unit test cho BookingService (create, cancel, status, pricing)', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'R', huy: 'R/A', vu: 'I', note: '' },
        { id: 'S2-33', task: 'Unit test — Payment', desc: 'Viết unit test cho PaymentService, WalletService', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'R', huy: 'R/A', vu: 'I', note: '' },
        { id: 'S2-34', task: 'Test case — Car & Booking', desc: 'Viết test case chi tiết: use case, steps, expected result, test data cho Car, Booking', priority: 'P1-High', est: '8h', loi: 'I', tuan: 'C', huy: 'R/A', vu: 'I', note: '' },
        { id: 'S2-35', task: 'Test case — Payment', desc: 'Viết test case cho module Payment & Wallet (payOS, wallet top-up, wallet pay, refund)', priority: 'P1-High', est: '6h', loi: 'I', tuan: 'C', huy: 'R/A', vu: 'I', note: '' },
        { id: 'S2-36', task: 'API documentation', desc: 'Viết tài liệu API (Swagger annotations, Postman collection)', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'C', huy: 'I', vu: 'R/A', note: '' },
        { id: 'S2-37', task: 'Database schema docs', desc: 'Tài liệu ERD, mô tả bảng, relationship, constraints', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'C', huy: 'I', vu: 'R/A', note: '' },
    ];
    buildSprintSheet(wb, 'Sprint 2 - Cốt lõi', 'SPRINT 2 (21/03 — 04/04/2026) — Tính năng cốt lõi — Tổng: 238 giờ', '7c3aed', '7c3aed', s2Tasks);

    // ======================== SPRINT 3 ========================
    var s3Tasks = [
        { id: 'S3-01', task: 'Partner registration', desc: 'Đăng ký trở thành đối tác P2P, upload giấy phép kinh doanh, CCCD', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-02', task: 'Partner layout', desc: 'Layout riêng cho partner portal: sidebar, header, navigation', priority: 'P1-High', est: '3h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-03', task: 'Partner dashboard', desc: 'Dashboard: tổng quan xe, booking, doanh thu, thu nhập, đánh giá', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-04', task: 'Partner add car', desc: 'Partner đăng xe mới: thông tin, hình ảnh, giá thuê. Sau đó chờ Admin duyệt', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-05', task: 'Partner view bookings', desc: 'Xem booking theo xe của mình, xác nhận nhận/trả xe', priority: 'P1-High', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-06', task: 'Partner contract', desc: 'Hợp đồng đối tác: template, ký điện tử, lưu trữ', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-07', task: 'Partner commission tracking', desc: 'Theo dõi doanh thu & hoa hồng (commission %), lịch sử thanh toán cho partner', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-08', task: 'Driver registration', desc: 'Đăng ký tài xế: thông tin cá nhân, bằng lái, kinh nghiệm', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-09', task: 'Driver layout', desc: 'Layout riêng cho driver portal: sidebar, header, navigation', priority: 'P1-High', est: '3h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-10', task: 'Driver dashboard', desc: 'Dashboard tài xế: chuyến đang chờ, đang chạy, hoàn thành, thu nhập', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-11', task: 'Assign driver to booking', desc: 'Admin gán tài xế vào booking, notification cho driver', priority: 'P1-High', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-12', task: 'Driver review', desc: 'Khách đánh giá tài xế sau chuyến (1-5 sao + comment)', priority: 'P2-Medium', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-13', task: 'Driver contract management', desc: 'Quản lý hợp đồng tài xế: tạo, ký, lưu trữ', priority: 'P2-Medium', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-14', task: 'DigitalContract model', desc: 'Thiết kế entity hợp đồng số: contract number, parties, terms, signatures', priority: 'P1-High', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-15', task: 'Contract template & generation', desc: 'Tạo template hợp đồng PDF: hợp đồng thuê xe, hợp đồng đối tác', priority: 'P1-High', est: '8h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-16', task: 'Send contract for signing', desc: 'Gửi hợp đồng qua email cho khách ký, notification nhắc nhở', priority: 'P1-High', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-17', task: 'E-signature integration', desc: 'Ký hợp đồng điện tử (DocuSign-style): vẽ chữ ký, upload chữ ký', priority: 'P1-High', est: '8h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-18', task: 'Signed contract storage', desc: 'Lưu trữ hợp đồng đã ký: secure file storage, versioning', priority: 'P1-High', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-19', task: 'Admin Digital Contract', desc: 'Quản lý hợp đồng số (Admin): danh sách, trạng thái, tải về', priority: 'P1-High', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-20', task: 'LongTermRental model', desc: 'Thiết kế entity thuê dài hạn: duration, mileage limit, payment plan', priority: 'P2-Medium', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-21', task: 'LongTerm rental form', desc: 'Form yêu cầu thuê dài hạn: chọn xe, thời hạn, thông tin liên hệ', priority: 'P2-Medium', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-22', task: 'Payment schedule', desc: 'Thiết lập lịch thanh toán định kỳ (hàng tháng), auto-reminder', priority: 'P2-Medium', est: '8h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-23', task: 'LongTerm contract preview', desc: 'Xem/tải hợp đồng dài hạn (PDF), lịch sử thanh toán', priority: 'P2-Medium', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-24', task: 'PromoCode model & CRUD', desc: 'Tạo mã khuyến mãi: mã, giảm %, giảm số tiền, ngày hết hạn, usage limit (Admin)', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-25', task: 'Apply promo to booking', desc: 'Áp dụng mã khuyến mãi vào booking: validate, calculate discount', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-26', task: 'Admin Promotions page', desc: 'Trang quản lý khuyến mãi: CRUD promo, thống kê usage', priority: 'P1-High', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-27', task: 'Membership tiers', desc: 'Cấu hình hạng thành viên: Bronze, Silver, Gold, Platinum (thresholds, benefits)', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-28', task: 'Loyalty points auto', desc: 'Tích điểm tự động sau booking: points = total * ratio, expiration', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-29', task: 'Membership auto-upgrade', desc: 'Nâng hạng thành viên tự động dựa trên tổng points hoặc total spent', priority: 'P1-High', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-30', task: 'Membership page', desc: 'Trang thành viên cho khách hàng: xem điểm, hạng, quyền lợi, lịch sử điểm', priority: 'P2-Medium', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-31', task: 'Holiday pricing', desc: 'Cấu hình giá theo ngày lễ (Tết, Giỗ Tổ, Quốc khánh), multiplier', priority: 'P2-Medium', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-32', task: 'Area CRUD', desc: 'CRUD khu vực (Admin): thêm, sửa, xóa, gán thành viên', priority: 'P2-Medium', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-33', task: 'Assign staff to area', desc: 'Gán nhân viên (driver, staff) vào khu vực hoạt động', priority: 'P2-Medium', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-34', task: 'Area dashboard', desc: 'Dashboard theo khu vực: số xe, booking, doanh thu theo khu vực', priority: 'P2-Medium', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-35', task: 'Area reports', desc: 'Thống kê báo cáo theo khu vực: biểu đồ, export CSV', priority: 'P2-Medium', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-36', task: 'Area Manager authorization', desc: 'Phân quyền Area Manager: chỉ xem/sửa dữ liệu trong khu vực được gán', priority: 'P2-Medium', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S3-37', task: 'Unit test — Partner/Driver', desc: 'Viết unit test cho PartnerService, DriverService', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'R', huy: 'R/A', vu: 'I', note: '' },
        { id: 'S3-38', task: 'Unit test — Promo/Membership', desc: 'Viết unit test cho PromoService, MembershipService', priority: 'P1-High', est: '6h', loi: 'C', tuan: 'R', huy: 'R/A', vu: 'I', note: '' },
        { id: 'S3-39', task: 'Test case — Partner & Driver', desc: 'Viết test case chi tiết cho Partner Portal, Driver Service', priority: 'P1-High', est: '6h', loi: 'I', tuan: 'C', huy: 'R/A', vu: 'I', note: '' },
        { id: 'S3-40', task: 'Test case — Promo & Membership', desc: 'Viết test case cho PromoCode, Loyalty, Membership tiers', priority: 'P1-High', est: '4h', loi: 'I', tuan: 'C', huy: 'R/A', vu: 'I', note: '' },
        { id: 'S3-41', task: 'Test case — Digital Contract', desc: 'Viết test case cho Digital Contract: tạo, gửi, ký, lưu trữ', priority: 'P1-High', est: '4h', loi: 'I', tuan: 'C', huy: 'R/A', vu: 'I', note: '' },
        { id: 'S3-42', task: 'User manual (v1)', desc: 'Viết user manual phiên bản 1: hướng dẫn sử dụng cho Customer, Partner, Driver, Admin', priority: 'P1-High', est: '8h', loi: 'I', tuan: 'I', huy: 'I', vu: 'R/A', note: '' },
    ];
    buildSprintSheet(wb, 'Sprint 3 - Mở rộng', 'SPRINT 3 (05/04 — 15/04/2026) — Tính năng mở rộng — Tổng: 232 giờ', '0891b2', '0891b2', s3Tasks);

    // ======================== SPRINT 4 ========================
    var s4Tasks = [
        { id: 'S4-01', task: 'Admin Dashboard', desc: 'Tổng quan hệ thống: tổng xe, booking, doanh thu, user stats, biểu đồ chart.js', priority: 'P0-Critical', est: '8h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-02', task: 'Reports & Analytics', desc: 'Báo cáo doanh thu, booking, xe, top xe thuê, tỷ lệ hủy, NPS', priority: 'P1-High', est: '8h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-03', task: 'DB Snapshot feature', desc: 'Tính năng snapshot database: export/import data for backup', priority: 'P2-Medium', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-04', task: 'Review model & CRUD', desc: 'Thiết kế Review entity, CRUD operations', priority: 'P1-High', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-05', task: 'Add car review', desc: 'Chức năng thêm đánh giá xe sau khi hoàn thành booking (1-5 sao + comment)', priority: 'P1-High', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-06', task: 'Display reviews', desc: 'Hiển thị đánh giá trên trang xe: average rating, tổng số đánh giá, pagination', priority: 'P1-High', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-07', task: 'Admin review moderation', desc: 'Duyệt/ẩn đánh giá vi phạm (Admin)', priority: 'P1-High', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-08', task: 'MaintenanceLog model', desc: 'Thiết kế entity bảo dưỡng: loại bảo dưỡng, chi phí, ngày, ghi chú', priority: 'P1-High', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-09', task: 'Maintenance log entry', desc: 'Ghi nhận bảo dưỡng xe: thêm, xem lịch sử bảo dưỡng', priority: 'P1-High', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-10', task: 'Maintenance alert', desc: 'Cảnh báo bảo dưỡng định kỳ qua Hangfire job: notification khi đến hạn', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-11', task: 'Vehicle inspection records', desc: 'Bản ghi kiểm tra xe định kỳ (hàng tháng/quý)', priority: 'P1-High', est: '4h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-12', task: 'GPS/Vehicle tracking', desc: 'Tích hợp GPS tracking (OpenStreetMap): hiển thị vị trí xe realtime', priority: 'P2-Medium', est: '8h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-13', task: 'Route tracking', desc: 'Theo dõi lộ trình xe: điểm đi, điểm đến, quãng đường, thời gian', priority: 'P2-Medium', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-14', task: 'Alert system', desc: 'Quản lý thông báo hệ thống: notification bell, in-app notifications', priority: 'P2-Medium', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-15', task: 'Contact form & messages', desc: 'Form liên hệ, quản lý tin nhắn (Admin)', priority: 'P2-Medium', est: '4h', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-16', task: 'Secure file access', desc: 'Truy cập file bảo mật (contract, ID scan): authorize, stream file', priority: 'P1-High', est: '6h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-17', task: 'eKYC upload & verification', desc: 'Upload CCCD mặt trước/sau, xác thực eKYC: OCR (mock), face match (mock)', priority: 'P1-High', est: '10h', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', note: '' },
        { id: 'S4-18', task: 'Integration testing', desc: 'Kiểm thử tích hợp toàn bộ hệ thống: API integration, data flow, edge cases', priority: 'P0-Critical', est: '12h', loi: 'C', tuan: 'C', huy: 'R/A', vu: 'I', note: '' },
        { id: 'S4-19', task: 'System test scenarios', desc: 'Viết kịch bản test toàn diện: smoke test, E2E test, boundary test', priority: 'P0-Critical', est: '8h', loi: 'I', tuan: 'C', huy: 'R/A', vu: 'I', note: '' },
        { id: 'S4-20', task: 'Bug fixing — Sprint 1-3', desc: 'Sửa lỗi phát hiện trong Sprint 1-3: bug reports từ Huy', priority: 'P0-Critical', est: '16h', loi: 'R/A', tuan: 'R', huy: 'R', vu: 'I', note: 'Lỗi nghiêm trọng: Lợi ưu tiên; Medium/Low: cả team' },
        { id: 'S4-21', task: 'UAT preparation', desc: 'Chuẩn bị môi trường UAT, test data, hướng dẫn UAT', priority: 'P0-Critical', est: '6h', loi: 'R/A', tuan: 'R', huy: 'C', vu: 'I', note: '' },
        { id: 'S4-22', task: 'Full system test', desc: 'Test toàn bộ hệ thống end-to-end: user flows, payment flows, notification flows', priority: 'P0-Critical', est: '12h', loi: 'C', tuan: 'C', huy: 'R/A', vu: 'I', note: '' },
        { id: 'S4-23', task: 'Performance test', desc: 'Kiểm thử hiệu năng: load test với concurrent users, response time', priority: 'P1-High', est: '8h', loi: 'C', tuan: 'R/A', huy: 'R', vu: 'I', note: '' },
        { id: 'S4-24', task: 'Security test', desc: 'Kiểm thử bảo mật: auth bypass, SQL injection, XSS, PII exposure, rate limiting', priority: 'P1-High', est: '8h', loi: 'R/A', tuan: 'C', huy: 'R', vu: 'I', note: '' },
        { id: 'S4-25', task: 'Final documentation', desc: 'Hoàn thiện toàn bộ tài liệu: update tất cả docs sau khi code xong', priority: 'P1-High', est: '8h', loi: 'C', tuan: 'C', huy: 'I', vu: 'R/A', note: '' },
        { id: 'S4-26', task: 'Project presentation docs', desc: 'Chuẩn bị tài liệu thuyết trình: slide demo, speaker script, demo script', priority: 'P1-High', est: '8h', loi: 'C', tuan: 'C', huy: 'I', vu: 'R/A', note: '' },
        { id: 'S4-27', task: 'Deployment guide', desc: 'Hướng dẫn triển khai production: server requirements, steps, docker, nginx config', priority: 'P1-High', est: '4h', loi: 'C', tuan: 'C', huy: 'I', vu: 'R/A', note: '' },
        { id: 'S4-28', task: 'Demo preparation', desc: 'Chuẩn bị demo: checklist tính năng, demo script, backup plan', priority: 'P0-Critical', est: '6h', loi: 'R/A', tuan: 'R', huy: 'C', vu: 'C', note: '' },
    ];
    buildSprintSheet(wb, 'Sprint 4 - Hoàn thiện', 'SPRINT 4 (16/04 — 24/04/2026) — Hoàn thiện & Vận hành — Tổng: 200 giờ', 'ea580c', 'ea580c', s4Tasks);

    // ======================== SHEET 7: SUMMARY ========================
    var wsSum = wb.addWorksheet('7. Tổng Hợp', {
        properties: { tabColor: { argb: 'FF16a34a' } },
        views: [{ state: 'frozen', xSplit: 0, ySplit: 3 }]
    });
    wsSum.columns = [
        { width: 20 }, { width: 14 }, { width: 14 }, { width: 14 }, { width: 14 }, { width: 14 }, { width: 12 }, { width: 22 },
    ];

    addTitleRow(wsSum, 'TỔNG HỢP PHÂN CÔNG THEO NGƯỜI & THEO SPRINT', 8);
    setHeaderRow(wsSum, 2, ['Người / Module', 'Sprint 1', 'Sprint 2', 'Sprint 3', 'Sprint 4', 'Tổng theo người', 'Tỷ lệ', 'Vai trò'], [C.darkBg, C.blue, C.purple, C.cyan, C.orange, C.green, C.green, C.green]);

    var summaryRows = [
        { name: 'Lợi (PO-Dev)', s1: '38h', s2: '90h', s3: '100h', s4: '70h', total: '298h', ratio: '40%', role: 'PO + Developer chính' },
        { name: 'Tuấn (Dev)', s1: '36h', s2: '80h', s3: '100h', s4: '65h', total: '281h', ratio: '38%', role: 'Developer' },
        { name: 'Huy (Tester)', s1: '10h', s2: '30h', s3: '26h', s4: '48h', total: '114h', ratio: '15%', role: 'QA / Tester' },
        { name: 'Vũ (Tài liệu)', s1: '12h', s2: '12h', s3: '8h', s4: '20h', total: '52h', ratio: '7%', role: 'Documentation' },
        { name: 'Tổng/Sprint', s1: '96h', s2: '212h', s3: '234h', s4: '203h', total: '745h', ratio: '100%', role: '' },
    ];

    var sumColors = [C.blue, C.purple, C.cyan, C.orange, C.green];
    summaryRows.forEach(function(r, i) {
        var rowNum = i + 3;
        var row = wsSum.getRow(rowNum);
        row.height = 32;
        var isTotal = r.name === 'Tổng/Sprint';
        var fillColor = isTotal ? C.green : (sumColors[i] || C.blue);
        setDataCell(row.getCell(1), r.name, fillColor, C.white, true, 'left');
        setDataCell(row.getCell(2), r.s1, fillColor, C.white, isTotal);
        setDataCell(row.getCell(3), r.s2, fillColor, C.white, isTotal);
        setDataCell(row.getCell(4), r.s3, fillColor, C.white, isTotal);
        setDataCell(row.getCell(5), r.s4, fillColor, C.white, isTotal);
        setDataCell(row.getCell(6), r.total, fillColor, C.white, true);
        setDataCell(row.getCell(7), r.ratio, fillColor, C.white, isTotal);
        setDataCell(row.getCell(8), r.role, fillColor, C.white, isTotal, 'left');
    });

    // ======================== SHEET 8: RACI MATRIX ========================
    var wsRaci = wb.addWorksheet('8. RACI Matrix', {
        properties: { tabColor: { argb: 'FFdb2777' } },
        views: [{ state: 'frozen', xSplit: 0, ySplit: 2 }]
    });
    wsRaci.columns = [
        { width: 32 }, { width: 10 }, { width: 10 }, { width: 10 }, { width: 10 }, { width: 55 },
    ];

    addTitleRow(wsRaci, 'RACI MATRIX — AI LÀM GÌ? (R/A, R, C, I)', 6);
    setHeaderRow(wsRaci, 2, ['Module / Công việc', 'Lợi', 'Tuấn', 'Huy', 'Vũ', 'Mô tả'], [C.darkBg, C.blue, C.purple, C.cyan, C.orange, C.darkBg]);

    var raciData = [
        { module: 'Sprint Planning & Backlog', loi: 'R/A', tuan: 'R', huy: 'C', vu: 'C', desc: 'Lợi làm PO quyết định backlog, Tuấn ước tính effort' },
        { module: 'Database Design', loi: 'R/A', tuan: 'R', huy: 'I', vu: 'I', desc: 'Lợi thiết kế, Tuấn review kỹ thuật' },
        { module: 'Authentication & PII', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', desc: '' },
        { module: 'Car Management (CRUD, Search)', loi: 'R/A', tuan: 'R/A', huy: 'I', vu: 'I', desc: 'Tuấn code, Lợi review design' },
        { module: 'Booking Flow & Pricing', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', desc: 'Lợi design + code core logic' },
        { module: 'Payment & Wallet (payOS)', loi: 'R/A', tuan: 'R', huy: 'I', vu: 'I', desc: 'Lợi tích hợp API, Tuấn backend support' },
        { module: 'Partner Portal', loi: 'R/A', tuan: 'R/A', huy: 'I', vu: 'I', desc: 'Tuấn code, Lợi review + contract/commission' },
        { module: 'Driver Portal', loi: 'R/A', tuan: 'R/A', huy: 'I', vu: 'I', desc: 'Tuấn code, Lợi review + assignment' },
        { module: 'Digital Contract', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', desc: '' },
        { module: 'Promo & Membership', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', desc: '' },
        { module: 'Area Management', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', desc: '' },
        { module: 'Admin Dashboard', loi: 'C', tuan: 'R/A', huy: 'I', vu: 'I', desc: '' },
        { module: 'Reviews & Maintenance', loi: 'R/A', tuan: 'R/A', huy: 'I', vu: 'I', desc: '' },
        { module: 'eKYC & Security', loi: 'R/A', tuan: 'C', huy: 'I', vu: 'I', desc: '' },
        { module: 'Unit Testing', loi: 'C', tuan: 'R', huy: 'R/A', vu: 'I', desc: 'Huy viết test, Tuấn review' },
        { module: 'Integration/E2E Testing', loi: 'C', tuan: 'C', huy: 'R/A', vu: 'I', desc: '' },
        { module: 'Bug Fixing', loi: 'R/A', tuan: 'R', huy: 'R', vu: 'I', desc: 'Lỗi nghiêm trọng: Lợi; Medium: cả team' },
        { module: 'API Documentation', loi: 'C', tuan: 'C', huy: 'I', vu: 'R/A', desc: '' },
        { module: 'Database Docs', loi: 'C', tuan: 'C', huy: 'I', vu: 'R/A', desc: '' },
        { module: 'User Manual', loi: 'I', tuan: 'I', huy: 'I', vu: 'R/A', desc: '' },
        { module: 'Final Documentation', loi: 'C', tuan: 'C', huy: 'I', vu: 'R/A', desc: '' },
        { module: 'Demo Preparation', loi: 'R/A', tuan: 'R', huy: 'C', vu: 'C', desc: '' },
    ];

    raciData.forEach(function(r, i) {
        var rowNum = i + 3;
        var row = wsRaci.getRow(rowNum);
        row.height = 30;
        var fillColor = i % 2 === 0 ? C.rowMain : C.rowAlt;
        setDataCell(row.getCell(1), r.module, fillColor, C.white, false, 'left');
        setDataCell(row.getCell(2), r.loi, roleColors[r.loi] || fillColor, C.white);
        setDataCell(row.getCell(3), r.tuan, roleColors[r.tuan] || fillColor, C.white);
        setDataCell(row.getCell(4), r.huy, roleColors[r.huy] || fillColor, C.white);
        setDataCell(row.getCell(5), r.vu, roleColors[r.vu] || fillColor, C.white);
        setDataCell(row.getCell(6), r.desc || '', fillColor, C.lightGray, false, 'left');
    });

    // Save
    var outputPath = 'e:\\CarRentalSystem\\CarRentalSystem\\CarRentalSystem\\CarRentalSystem\\SPRINT_BACKLOG_FULL.xlsx';
    await wb.xlsx.writeFile(outputPath);
    console.log('Excel created successfully:', outputPath);
}

buildSprintBacklogExcel().catch(console.error);
