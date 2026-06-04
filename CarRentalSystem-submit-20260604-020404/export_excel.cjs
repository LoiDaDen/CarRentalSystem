'use strict';
const ExcelJS = require('exceljs');

async function buildExcel() {
    const wb = new ExcelJS.Workbook();
    wb.creator = 'Car Rental System - Nhóm Y5';
    wb.created = new Date();

    const C = {
        darkBg: '0d1117',
        white: 'ffffff',
        lightGray: 'e6edf3',
        muted: '8b949e',
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

    // ======================== SHEET 1: TỔNG QUAN ========================
    const ws1 = wb.addWorksheet('1. Tổng Quán', {
        properties: { tabColor: { argb: 'FF1a56db' } },
        views: [{ state: 'frozen', xSplit: 0, ySplit: 2 }]
    });

    ws1.columns = [
        { width: 6 },   // STT
        { width: 42 },  // Module
        { width: 10 },  // PO
        { width: 10 },  // SM
        { width: 10 },  // DT
        { width: 48 },  // Mô tả
    ];

    // Title
    ws1.mergeCells('A1:F1');
    const titleCell = ws1.getCell('A1');
    titleCell.value = 'SPRINT PLANNING — CAR RENTAL SYSTEM — PHÂN CÔNG VAI TRÒ';
    titleCell.font = { size: 15, bold: true, color: { argb: 'FFFFFFFF' } };
    titleCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF0d1117' } };
    titleCell.alignment = { horizontal: 'center', vertical: 'middle' };
    ws1.getRow(1).height = 38;

    // Header
    const headers1 = ['STT', 'Module', 'PO', 'SM', 'DT', 'Mô Tả'];
    headers1.forEach((h, i) => {
        const colors = [C.darkBg, C.darkBg, C.blue, C.green, C.purple, C.darkBg];
        const cell = ws1.getCell(2, i + 1);
        cell.value = h;
        cell.font = { size: 11, bold: true, color: { argb: 'FFFFFFFF' } };
        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: colors[i] } };
        cell.alignment = { horizontal: 'center', vertical: 'middle' };
        cell.border = { top: { style: 'thin', color: { argb: 'FF30363d' } }, left: { style: 'thin', color: { argb: 'FF30363d' } }, bottom: { style: 'thin', color: { argb: 'FF30363d' } }, right: { style: 'thin', color: { argb: 'FF30363d' } } };
    });
    ws1.getRow(2).height = 28;

    const overviewData = [
        ['1', 'Backlog & Planning', 'R/A', 'R', 'R/A', 'Sprint Planning, Grooming, theo dõi Metrics'],
        ['2', 'Quản Lý Xe (Car Management)', 'R/A', 'I', 'R/A', 'CRUD xe, tìm kiếm, pricing, phê duyệt P2P'],
        ['3', 'Hệ Thống Đặt Xe (Booking)', 'R/A', 'I', 'R/A', 'Tạo đơn, thanh toán, hợp đồng, handover'],
        ['4', 'Giỏ Hàng & Giao Dịch (Cart)', 'I', 'I', 'R/A', 'Giỏ hàng nhiều xe, BookingTransaction'],
        ['5', 'Thanh Toán & Ví (Payment)', 'R/A', 'I', 'R/A', 'payOS, ví điện tử, nạp tiền, hóa đơn'],
        ['6', 'Xác Thực & Người Dùng (Auth)', 'R/A', 'I', 'R/A', 'Đăng ký, đăng nhập, phân quyền, mã hóa PII'],
        ['7', 'Cổng Đối Tác P2P (Partner)', 'R/A', 'I', 'R/A', 'Đăng ký đối tác, quản lý xe, hợp đồng'],
        ['8', 'Dịch Vụ Tài Xế (Driver)', 'R/A', 'I', 'R/A', 'Đăng ký tài xế, gán vào booking'],
        ['9', 'Hợp Đồng Số (Digital Contract)', 'R/A', 'I', 'R/A', 'Tạo, gửi, ký hợp đồng điện tử'],
        ['10', 'Thuê Dài Hạn (Long-term)', 'R/A', 'I', 'R/A', 'Yêu cầu thuê, lịch thanh toán, kiểm tra xe'],
        ['11', 'Khuyến Mãi & Thành Viên', 'R/A', 'I', 'R/A', 'Mã KM, voucher, loyalty, nâng hạng tier'],
        ['12', 'Quản Lý Khu Vực (Area)', 'R/A', 'I', 'R/A', 'Khu vực, nhân viên, dashboard, thống kê'],
        ['13', 'Bảo Dưỡng & Theo Dõi', 'I', 'I', 'R/A', 'Bảo dưỡng, GPS, route tracking'],
        ['14', 'Đánh Giá (Reviews)', 'R/A', 'I', 'R/A', 'Thêm đánh giá xe, duyệt/ẩn đánh giá'],
        ['15', 'Vận Hành Hệ Thống (Admin)', 'R/A', 'I', 'R/A', 'Dashboard, reports, audit log, alerts'],
        ['16', 'Sprint Events', 'C', 'R', 'R/A', 'Planning, Daily Standup, Review, Retro'],
    ];

    overviewData.forEach((row, ri) => {
        const rowNum = ri + 3;
        const bgColor = ri % 2 === 0 ? 'FF161b22' : 'FF21262d';

        row.forEach((val, ci) => {
            const cell = ws1.getCell(rowNum, ci + 1);
            cell.value = val;
            cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: bgColor } };
            cell.alignment = { horizontal: ci === 0 ? 'center' : (ci >= 2 && ci <= 4 ? 'center' : 'left'), vertical: 'middle', wrapText: true };
            cell.border = { top: { style: 'thin', color: { argb: 'FF30363d' } }, left: { style: 'thin', color: { argb: 'FF30363d' } }, bottom: { style: 'thin', color: { argb: 'FF30363d' } }, right: { style: 'thin', color: { argb: 'FF30363d' } } };

            if (ci === 0) {
                cell.font = { size: 10, bold: true, color: { argb: 'FF8b949e' } };
            } else if (ci === 1) {
                cell.font = { size: 10, bold: true, color: { argb: 'FFE6edf3' } };
            } else if (ci >= 2 && ci <= 4) {
                if (val === 'R/A') {
                    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF16a34a' } };
                    cell.font = { size: 11, bold: true, color: { argb: 'FFFFFFFF' } };
                } else if (val === 'R') {
                    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF1a56db' } };
                    cell.font = { size: 11, bold: true, color: { argb: 'FFFFFFFF' } };
                } else if (val === 'C') {
                    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFf59e0b' } };
                    cell.font = { size: 11, bold: true, color: { argb: 'FF000000' } };
                } else if (val === 'I') {
                    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF30363d' } };
                    cell.font = { size: 11, color: { argb: 'FF8b949e' } };
                }
            } else {
                cell.font = { size: 9, color: { argb: 'FF8b949e' } };
            }
        });
        ws1.getRow(rowNum).height = 24;
    });

    // Legend
    const legendRow = overviewData.length + 4;
    ws1.getCell(legendRow, 1).value = 'GHI CHÚ:';
    ws1.getCell(legendRow, 1).font = { size: 10, bold: true, color: { argb: 'FFE6edf3' } };
    ws1.getCell(legendRow, 1).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF0d1117' } };
    ws1.mergeCells(legendRow, 1, legendRow, 6);

    const legends = [
        ['R/A', 'Responsible + Accountable — Người thực hiện & chịu trách nhiệm chính', 'FF16a34a'],
        ['R', 'Responsible — Người thực hiện chính', 'FF1a56db'],
        ['C', 'Consulted — Được tham vấn', 'FFf59e0b'],
        ['I', 'Informed — Được thông báo', 'FF8b949e'],
        ['PO', 'Product Owner — Quyết định sản phẩm, backlog, ưu tiên', 'FF1a56db'],
        ['SM', 'Scrum Master — Hướng dẫn Scrum, loại bỏ trở ngại', 'FF16a34a'],
        ['DT', 'Dev Team — Phát triển, code, test, tự tổ chức', 'FF7c3aed'],
    ];

    legends.forEach((leg, li) => {
        const r = legendRow + 1 + li;
        ws1.getCell(r, 1).value = leg[0];
        ws1.getCell(r, 1).font = { size: 9, bold: true, color: { argb: 'FF000000' } };
        ws1.getCell(r, 1).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: leg[2] } };
        ws1.getCell(r, 1).alignment = { horizontal: 'center' };
        ws1.getCell(r, 1).border = { top: { style: 'thin' }, left: { style: 'thin' }, bottom: { style: 'thin' }, right: { style: 'thin' } };

        ws1.getCell(r, 2).value = leg[1];
        ws1.getCell(r, 2).font = { size: 9, color: { argb: 'FFE6edf3' } };
        ws1.getCell(r, 2).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF21262d' } };
        ws1.getCell(r, 2).border = { top: { style: 'thin' }, left: { style: 'thin' }, bottom: { style: 'thin' }, right: { style: 'thin' } };
        ws1.mergeCells(r, 2, r, 6);
        ws1.getRow(r).height = 18;
    });

    // ======================== HELPER: create section sheet ========================
    function createSectionSheet(num, name, color, data) {
        const ws = wb.addWorksheet(`${num}. ${name}`, {
            properties: { tabColor: { argb: color } }
        });

        ws.columns = [
            { width: 6 },
            { width: 44 },
            { width: 12 },
            { width: 12 },
            { width: 12 },
            { width: 44 },
        ];

        // Title
        ws.mergeCells('A1:F1');
        const tc = ws.getCell('A1');
        tc.value = `${num}. ${name.toUpperCase()} — CAR RENTAL SYSTEM`;
        tc.font = { size: 13, bold: true, color: { argb: 'FFFFFFFF' } };
        tc.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: color } };
        tc.alignment = { horizontal: 'center', vertical: 'middle' };
        ws.getRow(1).height = 32;

        // Header
        ['STT', 'Công Việc', 'PO', 'SM', 'DT', 'Mô Tả'].forEach((h, i) => {
            const cell = ws.getCell(2, i + 1);
            cell.value = h;
            cell.font = { size: 11, bold: true, color: { argb: 'FFFFFFFF' } };
            cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF161b22' } };
            cell.alignment = { horizontal: 'center', vertical: 'middle' };
            cell.border = { top: { style: 'thin', color: { argb: 'FF30363d' } }, left: { style: 'thin', color: { argb: 'FF30363d' } }, bottom: { style: 'thin', color: { argb: 'FF30363d' } }, right: { style: 'thin', color: { argb: 'FF30363d' } } };
        });
        ws.getRow(2).height = 26;

        // Data rows
        data.forEach((row, ri) => {
            const rowNum = ri + 3;
            const bgColor = ri % 2 === 0 ? 'FF161b22' : 'FF21262d';

            row.forEach((val, ci) => {
                const cell = ws.getCell(rowNum, ci + 1);
                cell.value = val;
                cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: bgColor } };
                cell.alignment = {
                    horizontal: ci === 0 ? 'center' : (ci >= 2 && ci <= 4 ? 'center' : 'left'),
                    vertical: 'middle', wrapText: true
                };
                cell.border = { top: { style: 'thin', color: { argb: 'FF30363d' } }, left: { style: 'thin', color: { argb: 'FF30363d' } }, bottom: { style: 'thin', color: { argb: 'FF30363d' } }, right: { style: 'thin', color: { argb: 'FF30363d' } } };

                if (ci === 0) {
                    cell.font = { size: 10, bold: true, color: { argb: 'FF8b949e' } };
                } else if (ci === 1) {
                    cell.font = { size: 10, color: { argb: 'FFE6edf3' } };
                } else if (ci >= 2 && ci <= 4) {
                    if (val === 'R/A') {
                        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF16a34a' } };
                        cell.font = { size: 11, bold: true, color: { argb: 'FFFFFFFF' } };
                    } else if (val === 'R') {
                        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF1a56db' } };
                        cell.font = { size: 11, bold: true, color: { argb: 'FFFFFFFF' } };
                    } else if (val === 'C') {
                        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFf59e0b' } };
                        cell.font = { size: 11, bold: true, color: { argb: 'FF000000' } };
                    } else if (val === 'I') {
                        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF30363d' } };
                        cell.font = { size: 11, color: { argb: 'FF8b949e' } };
                    } else if (val === 'N/A') {
                        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF21262d' } };
                        cell.font = { size: 11, color: { argb: 'FF8b949e' } };
                    }
                } else {
                    cell.font = { size: 9, color: { argb: 'FF8b949e' } };
                }
            });
            ws.getRow(rowNum).height = 26;
        });
    }

    // ======================== ALL SECTIONS ========================
    const sections = [
        {
            num: 2, name: 'Backlog & Planning', color: '1a56db',
            data: [
                ['1', 'Xác định product vision', 'R/A', 'C', 'I', 'PO định nghĩa tầm nhìn sản phẩm tổng thể'],
                ['2', 'Tạo danh sách user stories (Product Backlog)', 'R/A', 'I', 'C', 'PO viết stories, DT góp ý về kỹ thuật'],
                ['3', 'Ưu tiên backlog (sắp xếp theo MoSCoW)', 'R/A', 'I', 'I', 'PO quyết định thứ tự ưu tiên'],
                ['4', 'Sprint Planning — Chọn stories cho Sprint', 'C', 'R', 'R/A', 'SM điều phối, DT ước tính & cam kết'],
                ['5', 'Xác định Sprint Goal', 'C', 'C', 'R/A', 'DT đặt goal dựa trên stories đã chọn'],
                ['6', 'Tạo Sprint Backlog (tasks, estimates)', 'I', 'C', 'R/A', 'DT chia nhỏ stories thành tasks'],
                ['7', 'Cập nhật burndown chart', 'I', 'R', 'I', 'SM theo dõi tiến độ hàng ngày'],
                ['8', 'Backlog Grooming/Refinement (giữa Sprint)', 'R/A', 'R', 'R', 'Cả team cùng làm rõ stories tiếp theo'],
            ]
        },
        {
            num: 3, name: 'Quản Lý Xe', color: '7c3aed',
            data: [
                ['1', 'Thiết kế data model Car', 'C', 'I', 'R/A', 'DT thiết kế entity Car, properties'],
                ['2', 'Tạo API / Controller xe', 'I', 'I', 'R/A', 'DT code CarController, CRUD operations'],
                ['3', 'Tạo View danh sách xe (catalog)', 'C', 'I', 'R/A', 'DT code view, PO góp ý UX'],
                ['4', 'Tạo trang chi tiết xe (Details)', 'I', 'I', 'R/A', 'DT code trang detail + hình ảnh'],
                ['5', 'Tính năng tìm kiếm theo bản đồ (Map Search)', 'I', 'I', 'R/A', 'DT tích hợp map API'],
                ['6', 'Tính năng tìm kiếm giao xe (Delivery)', 'R/A', 'I', 'R', 'PO định nghĩa logic, DT implement'],
                ['7', 'Tính giá động theo ngày/giờ', 'R/A', 'I', 'R', 'PO cấu hình pricing rules, DT code'],
                ['8', 'Phê duyệt xe đối tác P2P (Admin)', 'R/A', 'I', 'R', 'PO duyệt xe mới đăng ký'],
                ['9', 'Trang quản lý xe Admin (CRUD)', 'I', 'I', 'R/A', 'DT code Admin Car Management'],
                ['10', 'Tính năng thêm đánh giá xe (Review)', 'I', 'I', 'R/A', 'DT code chức năng review'],
                ['11', 'Cấu hình trạng thái xe', 'R/A', 'I', 'R', 'PO định nghĩa trạng thái, DT implement'],
            ]
        },
        {
            num: 4, name: 'Hệ Thống Đặt Xe', color: '0891b2',
            data: [
                ['1', 'Thiết kế data model Booking', 'C', 'I', 'R/A', 'DT thiết kế entity Booking, relationships'],
                ['2', 'Tạo Booking flow (form, validation)', 'R/A', 'I', 'R', 'PO định nghĩa flow, DT code'],
                ['3', 'Tính phí giao xe tự động', 'R/A', 'I', 'R', 'PO cấu hình pricing, DT code logic'],
                ['4', 'Tính tổng chi phí (subtotal, discount, total)', 'R/A', 'I', 'R', 'PO xác nhận pricing logic, DT implement'],
                ['5', 'Chọn dịch vụ bổ sung (Add-Ons: GPS, child seat)', 'R/A', 'I', 'R', 'PO định nghĩa add-ons, DT code'],
                ['6', 'Tạo hợp đồng số (Digital Contract)', 'R/A', 'I', 'R', 'PO cấu hình contract template, DT code'],
                ['7', 'Ký hợp đồng điện tử (E-signature)', 'I', 'I', 'R/A', 'DT tích hợp e-signature'],
                ['8', 'Trang My Bookings (danh sách booking)', 'I', 'I', 'R/A', 'DT code trang khách hàng'],
                ['9', 'Chức năng hủy booking (Cancel)', 'R/A', 'I', 'R', 'PO định nghĩa rules hủy, DT code'],
                ['10', 'Trang chi tiết booking', 'I', 'I', 'R/A', 'DT code trang detail'],
                ['11', 'Xác nhận nhận xe (Customer Pickup)', 'I', 'I', 'R/A', 'DT code chức năng confirm'],
                ['12', 'Xác nhận trả xe (Customer Return)', 'I', 'I', 'R/A', 'DT code chức năng confirm'],
                ['13', 'Trang Admin quản lý booking', 'I', 'I', 'R/A', 'DT code Admin Booking Management'],
                ['14', 'Cập nhật trạng thái booking', 'I', 'I', 'R/A', 'DT code chuyển trạng thái'],
                ['15', 'Bản ghi handover (pickup/return inspection)', 'I', 'I', 'R/A', 'DT code handover records'],
            ]
        },
        {
            num: 5, name: 'Giỏ Hàng & Giao Dịch', color: 'ea580c',
            data: [
                ['1', 'Tạo data model Cart', 'C', 'I', 'R/A', 'DT thiết kế Cart/CartItem'],
                ['2', 'Chức năng thêm xe vào giỏ', 'I', 'I', 'R/A', 'DT code add-to-cart'],
                ['3', 'Cập nhật / Xóa xe khỏi giỏ', 'I', 'I', 'R/A', 'DT code update/remove'],
                ['4', 'Tính tổng giá trị giỏ hàng', 'I', 'I', 'R/A', 'DT code pricing logic'],
                ['5', 'Tạo BookingTransaction cho multi-booking', 'R/A', 'I', 'R', 'PO định nghĩa transaction flow'],
            ]
        },
        {
            num: 6, name: 'Thanh Toán & Ví', color: '16a34a',
            data: [
                ['1', 'Tạo data model Wallet', 'C', 'I', 'R/A', 'DT thiết kế Wallet entity'],
                ['2', 'Tạo data model Payment', 'C', 'I', 'R/A', 'DT thiết kế Payment entity'],
                ['3', 'Tích hợp payOS (Payment Gateway)', 'R/A', 'I', 'R', 'PO chọn provider, DT tích hợp API'],
                ['4', 'Trang thanh toán thành công (Success)', 'I', 'I', 'R/A', 'DT code callback handler'],
                ['5', 'Trang hủy thanh toán (Cancel)', 'I', 'I', 'R/A', 'DT code cancel handler'],
                ['6', 'Chức năng nạp tiền ví', 'I', 'I', 'R/A', 'DT code wallet top-up'],
                ['7', 'Thanh toán bằng ví (Wallet)', 'I', 'I', 'R/A', 'DT code wallet payment'],
                ['8', 'Tạo hóa đơn (Invoice)', 'R/A', 'I', 'R', 'PO định nghĩa invoice format, DT code'],
                ['9', 'Trang Admin quản lý thanh toán', 'I', 'I', 'R/A', 'DT code Admin Payment Management'],
            ]
        },
        {
            num: 7, name: 'Xác Thực & Người Dùng', color: 'dc2626',
            data: [
                ['1', 'Thiết kế data model User', 'C', 'I', 'R/A', 'DT thiết kế User entity, roles'],
                ['2', 'Chức năng đăng ký tài khoản', 'I', 'I', 'R/A', 'DT code registration'],
                ['3', 'Chức năng đăng nhập / đăng xuất', 'I', 'I', 'R/A', 'DT code authentication (cookie-based)'],
                ['4', 'Xác thực email (Email verification)', 'I', 'I', 'R/A', 'DT code email verification flow'],
                ['5', 'Chức năng quên mật khẩu', 'I', 'I', 'R/A', 'DT code password reset'],
                ['6', 'Mã hóa PII (BCrypt)', 'I', 'I', 'R/A', 'DT implement PII encryption'],
                ['7', 'Trang cập nhật profile', 'I', 'I', 'R/A', 'DT code profile page'],
                ['8', 'Đặt / Đổi mã PIN bảo vệ PII', 'I', 'I', 'R/A', 'DT code PII PIN'],
                ['9', 'Trang Admin quản lý người dùng', 'I', 'I', 'R/A', 'DT code Admin User Management'],
                ['10', 'Phân quyền (Role-based access)', 'R/A', 'I', 'R', 'PO định nghĩa roles, DT implement'],
            ]
        },
        {
            num: 8, name: 'Cổng Đối Tác P2P', color: 'f59e0b',
            data: [
                ['1', 'Thiết kế Partner Portal layout', 'R/A', 'I', 'R', 'PO định nghĩa requirements, DT code layout'],
                ['2', 'Đăng ký trở thành đối tác', 'I', 'I', 'R/A', 'DT code partner registration'],
                ['3', 'Dashboard đối tác', 'I', 'I', 'R/A', 'DT code Partner Dashboard'],
                ['4', 'Chức năng thêm xe (Partner Add Car)', 'I', 'I', 'R/A', 'DT code partner car management'],
                ['5', 'Xem booking theo xe của đối tác', 'I', 'I', 'R/A', 'DT code partner booking view'],
                ['6', 'Hợp đồng đối tác (Partner Contract)', 'R/A', 'I', 'R', 'PO cấu hình contract, DT code'],
                ['7', 'Theo dõi doanh thu & hoa hồng', 'R/A', 'I', 'R', 'PO định nghĩa commission logic, DT code'],
            ]
        },
        {
            num: 9, name: 'Dịch Vụ Tài Xế', color: '7c3aed',
            data: [
                ['1', 'Thiết kế data model Driver', 'C', 'I', 'R/A', 'DT thiết kế Driver entity'],
                ['2', 'Đăng ký tài xế (Driver Registration)', 'I', 'I', 'R/A', 'DT code driver registration'],
                ['3', 'Dashboard tài xế (Driver Dashboard)', 'I', 'I', 'R/A', 'DT code Driver Dashboard'],
                ['4', 'Gán tài xế vào booking', 'R/A', 'I', 'R', 'PO/Admin gán driver, DT code'],
                ['5', 'Đánh giá tài xế', 'I', 'I', 'R/A', 'DT code driver review'],
                ['6', 'Quản lý hợp đồng tài xế', 'R/A', 'I', 'R', 'PO cấu hình, DT code'],
            ]
        },
        {
            num: 10, name: 'Hợp Đồng Số', color: '0891b2',
            data: [
                ['1', 'Thiết kế DigitalContract model', 'C', 'I', 'R/A', 'DT thiết kế contract entity'],
                ['2', 'Tạo template hợp đồng', 'R/A', 'I', 'R', 'PO định nghĩa template, DT code'],
                ['3', 'Gửi hợp đồng cho khách ký', 'I', 'I', 'R/A', 'DT code send contract flow'],
                ['4', 'Ký hợp đồng điện tử', 'I', 'I', 'R/A', 'DT code e-signature'],
                ['5', 'Lưu trữ hợp đồng đã ký', 'I', 'I', 'R/A', 'DT code contract storage'],
                ['6', 'Trang Admin quản lý hợp đồng', 'I', 'I', 'R/A', 'DT code Admin Digital Contract'],
            ]
        },
        {
            num: 11, name: 'Thuê Dài Hạn', color: '1a56db',
            data: [
                ['1', 'Thiết kế LongTermRental model', 'C', 'I', 'R/A', 'DT thiết kế entity'],
                ['2', 'Form yêu cầu thuê dài hạn', 'R/A', 'I', 'R', 'PO định nghĩa form, DT code'],
                ['3', 'Thiết lập lịch thanh toán (Payment Schedule)', 'R/A', 'I', 'R', 'PO cấu hình schedule, DT code'],
                ['4', 'Kiểm tra xe định kỳ (Vehicle Inspection)', 'I', 'I', 'R/A', 'DT code inspection records'],
                ['5', 'Xem / tải hợp đồng dài hạn', 'I', 'I', 'R/A', 'DT code contract preview'],
            ]
        },
        {
            num: 12, name: 'Khuyến Mãi & Thành Viên', color: 'dc2626',
            data: [
                ['1', 'Tạo PromoCode / Voucher', 'R/A', 'I', 'R', 'PO tạo promo, DT code'],
                ['2', 'Áp dụng mã khuyến mãi vào booking', 'R/A', 'I', 'R', 'PO định nghĩa logic, DT code'],
                ['3', 'Trang Admin quản lý khuyến mãi', 'I', 'I', 'R/A', 'DT code Admin Promotions'],
                ['4', 'Cấu hình Membership Tier', 'R/A', 'I', 'R', 'PO cấu hình tiers, DT code'],
                ['5', 'Tích điểm loyalty tự động', 'R/A', 'I', 'R', 'PO định nghĩa rules, DT code'],
                ['6', 'Nâng hạng thành viên tự động', 'R/A', 'I', 'R', 'PO cấu hình, DT code'],
                ['7', 'Trang Membership cho khách hàng', 'I', 'I', 'R/A', 'DT code Membership page'],
                ['8', 'Cấu hình giá holiday (Holiday Pricing)', 'R/A', 'I', 'R', 'PO cấu hình, DT code'],
            ]
        },
        {
            num: 13, name: 'Quản Lý Khu Vực', color: '16a34a',
            data: [
                ['1', 'Thiết kế Area model', 'C', 'I', 'R/A', 'DT thiết kế Area entity'],
                ['2', 'CRUD khu vực (Admin)', 'I', 'I', 'R/A', 'DT code Area Management'],
                ['3', 'Gán nhân viên vào khu vực', 'R/A', 'I', 'R', 'PO/Admin gán staff, DT code'],
                ['4', 'Dashboard theo khu vực', 'R/A', 'I', 'R', 'PO cấu hình metrics, DT code'],
                ['5', 'Thống kê theo khu vực', 'R/A', 'I', 'R', 'PO định nghĩa reports, DT code'],
                ['6', 'Phân quyền Area Manager', 'R/A', 'I', 'R', 'PO cấu hình, DT implement'],
            ]
        },
        {
            num: 14, name: 'Bảo Dưỡng & Theo Dõi', color: 'ea580c',
            data: [
                ['1', 'Thiết kế MaintenanceLog model', 'C', 'I', 'R/A', 'DT thiết kế entity'],
                ['2', 'Ghi nhận bảo dưỡng xe', 'I', 'I', 'R/A', 'DT code maintenance log'],
                ['3', 'Cảnh báo bảo dưỡng định kỳ', 'R/A', 'I', 'R', 'PO cấu hình rules, DT code'],
                ['4', 'Theo dõi GPS xe (Vehicle Tracking)', 'I', 'I', 'R/A', 'DT tích hợp tracking API'],
                ['5', 'Route tracking', 'I', 'I', 'R/A', 'DT code route tracking'],
                ['6', 'Bản ghi kiểm tra xe (Inspection)', 'I', 'I', 'R/A', 'DT code inspection records'],
            ]
        },
        {
            num: 15, name: 'Đánh Giá', color: 'f59e0b',
            data: [
                ['1', 'Thiết kế Review model', 'C', 'I', 'R/A', 'DT thiết kế entity'],
                ['2', 'Chức năng thêm đánh giá xe', 'I', 'I', 'R/A', 'DT code add review'],
                ['3', 'Hiển thị đánh giá trên trang xe', 'I', 'I', 'R/A', 'DT code review display'],
                ['4', 'Duyệt / Ẩn đánh giá (Admin)', 'R/A', 'I', 'R', 'PO/Admin duyệt review, DT code'],
            ]
        },
        {
            num: 16, name: 'Vận Hành Hệ Thống', color: 'dc2626',
            data: [
                ['1', 'Thiết kế Admin Dashboard', 'R/A', 'I', 'R', 'PO định nghĩa metrics, DT code'],
                ['2', 'Báo cáo thống kê (Reports)', 'R/A', 'I', 'R', 'PO cấu hình reports, DT code'],
                ['3', 'Audit Log (ghi log kiểm toán)', 'R/A', 'I', 'R', 'PO định nghĩa audit rules, DT code'],
                ['4', 'Snapshot database', 'I', 'I', 'R/A', 'DT code DB snapshot feature'],
                ['5', 'CRUD reference data (Lookups)', 'I', 'I', 'R/A', 'DT code CarBrand, CarType management'],
                ['6', 'Quản lý thông báo (Alert)', 'I', 'I', 'R/A', 'DT code alert system'],
                ['7', 'Quản lý tin nhắn liên hệ', 'I', 'I', 'R/A', 'DT code contact form'],
                ['8', 'Background jobs (Hangfire)', 'I', 'I', 'R/A', 'DT code async jobs'],
                ['9', 'Secure file access', 'I', 'I', 'R/A', 'DT code secure document access'],
                ['10', 'eKYC verification', 'I', 'I', 'R/A', 'DT code ID card upload & verification'],
            ]
        },
    ];

    sections.forEach(sec => {
        createSectionSheet(sec.num, sec.name, sec.color, sec.data);
    });

    // ======================== SHEET: TỔNG HỢP THEO VAI TRÒ ========================
    const wsSum = wb.addWorksheet('Tổng Hợp Theo Vai Trò', {
        properties: { tabColor: { argb: 'FF7c3aed' } },
        views: [{ state: 'frozen', xSplit: 0, ySplit: 2 }]
    });

    wsSum.columns = [
        { width: 6 },
        { width: 38 },
        { width: 22 },
        { width: 55 },
    ];

    wsSum.mergeCells('A1:D1');
    const sumTitle = wsSum.getCell('A1');
    sumTitle.value = 'TỔNG HỢP PHÂN CÔNG THEO VAI TRÒ — CAR RENTAL SYSTEM';
    sumTitle.font = { size: 14, bold: true, color: { argb: 'FFFFFFFF' } };
    sumTitle.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF0d1117' } };
    sumTitle.alignment = { horizontal: 'center', vertical: 'middle' };
    wsSum.getRow(1).height = 38;

    // PO Section
    wsSum.mergeCells('A2:D2');
    const poHdr = wsSum.getCell('A2');
    poHdr.value = 'PRODUCT OWNER (PO) — R/A — 13 công việc chính';
    poHdr.font = { size: 11, bold: true, color: { argb: 'FFFFFFFF' } };
    poHdr.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF1a56db' } };
    poHdr.alignment = { horizontal: 'center' };
    wsSum.getRow(2).height = 28;

    const poItems = [
        ['1', 'Định nghĩa product vision & roadmap', 'Planning', 'Quyết định tầm nhìn sản phẩm tổng thể, định hướng phát triển'],
        ['2', 'Tạo & ưu tiên Product Backlog', 'Planning', 'Viết user stories, sắp xếp thứ tự ưu tiên'],
        ['3', 'Quyết định scope & release plan', 'Planning', 'Xác định phạm vi dự án, lịch release'],
        ['4', 'Giao tiếp với stakeholder', 'Communication', 'Truyền đạt, thu thập feedback từ khách hàng'],
        ['5', 'Phê duyệt xe đối tác P2P', 'Approval', 'Duyệt xe mới đăng ký của đối tác'],
        ['6', 'Cấu hình pricing (xe, delivery, holiday)', 'Configuration', 'Đặt giá theo giờ/ngày, giá đặc biệt'],
        ['7', 'Định nghĩa contract template', 'Content', 'Tạo mẫu hợp đồng số'],
        ['8', 'Cấu hình Membership Tier & Loyalty', 'Configuration', 'Đặt quy tắc tích điểm, nâng hạng thành viên'],
        ['9', 'Cấu hình Promotion & PromoCode', 'Configuration', 'Tạo mã giảm giá, voucher'],
        ['10', 'Định nghĩa hợp đồng đối tác P2P', 'Content', 'Tạo nội dung hợp đồng đối tác'],
        ['11', 'Thiết kế Admin Dashboard metrics', 'Design', 'Xác định các chỉ số hiển thị'],
        ['12', 'Định nghĩa audit rules', 'Security', 'Quy tắc ghi log kiểm toán'],
        ['13', 'Phân quyền người dùng', 'Security', 'Định nghĩa roles: Customer/Admin/Staff/Partner'],
    ];

    poItems.forEach((item, ri) => {
        const r = ri + 3;
        item.forEach((val, ci) => {
            const cell = wsSum.getCell(r, ci + 1);
            cell.value = val;
            const bg = ri % 2 === 0 ? 'FF161b22' : 'FF21262d';
            cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: bg } };
            cell.font = { size: 10, color: ci === 0 ? 'FF8b949e' : (ci === 2 ? 'FF1a56db' : 'FFE6edf3'), bold: ci === 0 };
            cell.alignment = { horizontal: ci === 0 ? 'center' : 'left', vertical: 'middle', wrapText: true };
            cell.border = { top: { style: 'thin', color: { argb: 'FF30363d' } }, left: { style: 'thin', color: { argb: 'FF30363d' } }, bottom: { style: 'thin', color: { argb: 'FF30363d' } }, right: { style: 'thin', color: { argb: 'FF30363d' } } };
        });
        wsSum.getRow(r).height = 24;
    });

    // SM Section
    const smStartRow = poItems.length + 4;
    wsSum.mergeCells(`A${smStartRow}:D${smStartRow}`);
    const smHdr = wsSum.getCell(`A${smStartRow}`);
    smHdr.value = 'SCRUM MASTER (SM) — R — 10 công việc chính';
    smHdr.font = { size: 11, bold: true, color: { argb: 'FFFFFFFF' } };
    smHdr.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF16a34a' } };
    smHdr.alignment = { horizontal: 'center' };
    wsSum.getRow(smStartRow).height = 28;

    const smItems = [
        ['1', 'Điều phối Sprint Planning', 'Facilitation', 'Hướng dẫn team lập kế hoạch Sprint'],
        ['2', 'Điều phối Daily Standup', 'Facilitation', 'Điều phối họp hàng ngày 15 phút'],
        ['3', 'Điều phối Sprint Review', 'Facilitation', 'Hướng dẫn buổi demo cuối Sprint'],
        ['4', 'Điều phối Retrospective', 'Facilitation', 'Hướng dẫn buổi phân công, cải tiến'],
        ['5', 'Theo dõi Sprint metrics', 'Tracking', 'Velocity, burndown chart, compliance'],
        ['6', 'Loại bỏ trở ngại cho team', 'Support', 'Giải quyết các vấn đề làm chậm tiến độ'],
        ['7', 'Coach team về Scrum', 'Coaching', 'Hướng dẫn Agile practices, loại bỏ hiểu nhầm'],
        ['8', 'Bảo vệ team khỏi gián đoạn', 'Protection', 'Chặn các yêu cầu không ưu tiên từ bên ngoài'],
        ['9', 'Cải tiến quy trình liên tục', 'Improvement', 'Tìm cách làm việc hiệu quả hơn mỗi Sprint'],
        ['10', 'Backlog Grooming/Refinement', 'Facilitation', 'Điều phối buổi làm rõ backlog giữa Sprint'],
    ];

    smItems.forEach((item, ri) => {
        const r = smStartRow + 1 + ri;
        item.forEach((val, ci) => {
            const cell = wsSum.getCell(r, ci + 1);
            cell.value = val;
            const bg = ri % 2 === 0 ? 'FF161b22' : 'FF21262d';
            cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: bg } };
            cell.font = { size: 10, color: ci === 0 ? 'FF8b949e' : (ci === 2 ? 'FF16a34a' : 'FFE6edf3'), bold: ci === 0 };
            cell.alignment = { horizontal: ci === 0 ? 'center' : 'left', vertical: 'middle', wrapText: true };
            cell.border = { top: { style: 'thin', color: { argb: 'FF30363d' } }, left: { style: 'thin', color: { argb: 'FF30363d' } }, bottom: { style: 'thin', color: { argb: 'FF30363d' } }, right: { style: 'thin', color: { argb: 'FF30363d' } } };
        });
        wsSum.getRow(r).height = 24;
    });

    // DT Section
    const dtStartRow = smStartRow + smItems.length + 2;
    wsSum.mergeCells(`A${dtStartRow}:D${dtStartRow}`);
    const dtHdr = wsSum.getCell(`A${dtStartRow}`);
    dtHdr.value = 'DEV TEAM (DT) — R/A — 16+ công việc chính (~80% khối lượng)';
    dtHdr.font = { size: 11, bold: true, color: { argb: 'FFFFFFFF' } };
    dtHdr.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF7c3aed' } };
    dtHdr.alignment = { horizontal: 'center' };
    wsSum.getRow(dtStartRow).height = 28;

    const dtItems = [
        ['1', 'Thiết kế data models (Car, Booking, User, Wallet...)', 'Database', 'Tạo entity, relationships, migration'],
        ['2', 'Tạo Controllers & API endpoints', 'Backend', 'Code Business Logic cho từng chức năng'],
        ['3', 'Tạo Views (Razor Pages)', 'Frontend', 'Giao diện người dùng cho từng trang'],
        ['4', 'Tích hợp Payment Gateway (payOS)', 'Integration', 'Kết nối cổng thanh toán online'],
        ['5', 'Tích hợp Map API', 'Integration', 'Tìm kiếm xe theo bản đồ'],
        ['6', 'Tích hợp E-signature', 'Integration', 'Ký hợp đồng điện tử'],
        ['7', 'Tích hợp GPS / Tracking API', 'Integration', 'Theo dõi vị trí xe'],
        ['8', 'Viết Unit Tests', 'Testing', 'Đảm bảo chất lượng code'],
        ['9', 'Code Review (Pull Requests)', 'Quality', 'Kiểm tra code của nhau'],
        ['10', 'Tích hợp Background Jobs (Hangfire)', 'Backend', 'Xử lý async tasks'],
        ['11', 'Implement Authentication & Authorization', 'Security', 'Cookie-based auth, role-based access'],
        ['12', 'Implement PII Encryption', 'Security', 'Mã hóa dữ liệu nhạy cảm'],
        ['13', 'Tạo Admin Dashboard', 'Frontend', 'Trang quản trị hệ thống'],
        ['14', 'Tạo Partner Portal', 'Frontend', 'Trang đối tác P2P'],
        ['15', 'Tạo Driver Portal', 'Frontend', 'Trang tài xế'],
        ['16', 'Code Review lẫn nhau', 'Quality', 'Đảm bảo chất lượng, chia sẻ kiến thức'],
    ];

    dtItems.forEach((item, ri) => {
        const r = dtStartRow + 1 + ri;
        item.forEach((val, ci) => {
            const cell = wsSum.getCell(r, ci + 1);
            cell.value = val;
            const bg = ri % 2 === 0 ? 'FF161b22' : 'FF21262d';
            cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: bg } };
            cell.font = { size: 10, color: ci === 0 ? 'FF8b949e' : (ci === 2 ? 'FF7c3aed' : 'FFE6edf3'), bold: ci === 0 };
            cell.alignment = { horizontal: ci === 0 ? 'center' : 'left', vertical: 'middle', wrapText: true };
            cell.border = { top: { style: 'thin', color: { argb: 'FF30363d' } }, left: { style: 'thin', color: { argb: 'FF30363d' } }, bottom: { style: 'thin', color: { argb: 'FF30363d' } }, right: { style: 'thin', color: { argb: 'FF30363d' } } };
        });
        wsSum.getRow(r).height = 24;
    });

    // Save
    await wb.xlsx.writeFile('SPRINT_PHAN_CONG_Y5_VietNam.xlsx');
    console.log('Done!');
}

buildExcel().catch(console.error);
