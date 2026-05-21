const ExcelJS = require('exceljs');
const fs = require('fs');
const path = require('path');

const args = process.argv.slice(2);
const getArg = (name, fallback) => {
  const flag = `--${name}=`;
  const found = args.find((arg) => arg.startsWith(flag));
  return found ? found.slice(flag.length) : fallback;
};

const templatePath = getArg('template', 'D:\\TestCase_CarRentalSystem_Full (1).xlsx');
const outputPath = getArg('output', 'D:\\TestCase_CarRentalSystem_Full_Detailed_2026-05-20.xlsx');

const colors = {
  blue: 'FF2563EB',
  teal: 'FF0891B2',
  green: 'FF16A34A',
  purple: 'FF7C3AED',
  orange: 'FFF97316',
  red: 'FFDC2626',
  dark: 'FF111827',
  header: 'FF305496',
  border: 'FFBFBFBF',
  white: 'FFFFFFFF',
  alt: 'FFF8FAFC',
  pending: 'FFFFF2CC',
};

const fill = (argb) => ({ type: 'pattern', pattern: 'solid', fgColor: { argb } });
const borderAll = () => ({
  top: { style: 'thin', color: { argb: colors.border } },
  left: { style: 'thin', color: { argb: colors.border } },
  bottom: { style: 'thin', color: { argb: colors.border } },
  right: { style: 'thin', color: { argb: colors.border } },
});
const steps = (items) => items.map((item, idx) => `${idx + 1}: ${item}`).join('\n');
const expects = (items) => items.map((item) => `- ${item}`).join('\n');
const sheetName = (code, title) => `${code} - ${title}`.slice(0, 31);

function loginStep(role) {
  if (role === 'Guest') return 'Mở trình duyệt ẩn danh, không đăng nhập';
  if (role === 'System') return 'Chuẩn bị dữ liệu hệ thống, provider mock hoặc payload API hợp lệ';
  if (role.includes('/')) return `Đăng nhập lần lượt bằng các vai trò: ${role}`;
  return `Đăng nhập bằng tài khoản ${role}`;
}

function note(role, priority, source, route) {
  return `Role: ${role} | Priority: ${priority} | Source: ${source} | Route/API: ${route}`;
}

function feature(name, route, role, source, action, expected, negative) {
  return { name, route, role, source, action, expected, negative };
}

function expandFeature(moduleTitle, item) {
  const highRisk = /auth|login|register|booking|checkout|cart|payment|payos|webhook|refund|contract|handover|ekyc|pii|privacy|rbac|csrf|rate|security|legal|consent|admin|area|user|snapshot|audit/i.test(
    `${moduleTitle} ${item.name}`,
  );
  const priority = highRisk ? 'P0' : 'P1';
  const negative = item.negative || 'bỏ trống dữ liệu bắt buộc, nhập sai định dạng, dùng ID không tồn tại, dùng role không đủ quyền và submit nhanh hai lần';
  return [
    {
      desc: `${item.name} - luồng chính`,
      steps: steps([
        loginStep(item.role),
        `Truy cập ${item.route}`,
        `Thực hiện thao tác chính: ${item.action}`,
        'Reload trang hoặc mở màn hình liên quan để xác nhận dữ liệu sau thao tác',
        'Kiểm tra Network/Console để bảo đảm không có lỗi 500 hoặc lỗi JavaScript nghiêm trọng',
      ]),
      expected: expects([
        item.expected,
        'HTTP status nhỏ hơn 500; không hiển thị stack trace, SQL exception, NullReferenceException hoặc trang Error ngoài ý muốn',
        'Dữ liệu hiển thị/tạo/sửa/xóa đúng user, role, trạng thái và khu vực liên quan',
        'Thông báo thành công/lỗi rõ ràng bằng tiếng Việt có dấu; không mojibake, không placeholder hoặc link chết',
        'Audit/log/evidence được ghi với dữ liệu tối thiểu cần thiết nếu thao tác thuộc nhóm nhạy cảm',
      ]),
      notes: note(item.role, priority, item.source, item.route),
    },
    {
      desc: `${item.name} - validation, phân quyền và edge cases`,
      steps: steps([
        loginStep(item.role),
        `Truy cập ${item.route} hoặc gọi trực tiếp action/API liên quan`,
        `Thử các trường hợp âm tính: ${negative}`,
        'Thử reload giữa chừng, back-forward trình duyệt hoặc request trực tiếp bỏ qua UI nếu áp dụng',
        'Kiểm tra dữ liệu trong màn hình danh sách/detail sau mỗi lần thử',
      ]),
      expected: expects([
        'Hệ thống chặn dữ liệu sai ở server-side, kể cả khi bypass client validation',
        'Role/user/khu vực không đủ quyền nhận 401/403/AccessDenied hoặc redirect đúng, không lộ dữ liệu tồn tại',
        'Không tạo bản ghi rác, không double-submit, không thay đổi trạng thái trái workflow',
        'Thông báo lỗi gắn đúng field/action, không lộ secret, stack trace, connection string hoặc PII plaintext',
        'Các trường nhập HTML/script/SQL-like được encode/sanitize, không gây XSS hoặc SQL error',
      ]),
      notes: note(item.role, priority, item.source, item.route),
    },
    {
      desc: `${item.name} - layout, responsive, accessibility và console`,
      steps: steps([
        loginStep(item.role),
        `Mở ${item.route} trên desktop 1366x768, tablet và mobile`,
        'Cuộn toàn trang; mở dropdown, modal, date picker, map/chart/offcanvas/toast nếu có',
        'Điều hướng bằng keyboard Tab/Shift+Tab/Enter/Escape qua các control chính',
        'Quan sát Console và Network sau khi thao tác chính',
      ]),
      expected: expects([
        'Không có horizontal overflow bất thường; text trong button, badge, table, card, modal không bị cắt hoặc chồng lên nhau',
        'Control có label/aria-label phù hợp, focus visible, tab order hợp lý và không có keyboard trap',
        'Ảnh/icon/chart/map có fallback hoặc empty state rõ ràng khi dữ liệu/tài nguyên thiếu',
        'Toast/modal/dropdown không che mất CTA chính và đóng/mở đúng trên mobile',
        'Console không có ReferenceError, TypeError, Cannot read properties, invalid object/column name hoặc lỗi script nghiêm trọng',
      ]),
      notes: note(item.role, 'P1', item.source, item.route),
    },
  ];
}

const modules = [
  {
    code: 'MD01', title: 'Layout Public', req: 'LAY01', color: colors.teal,
    features: [
      feature('Navbar public brand/menu/active state', '/', 'Guest/Customer', 'Views/Shared/_Layout.cshtml', 'mở Trang chủ, Xe, Bản đồ, Blog, Thuê dài hạn, Membership và kiểm tra active state', 'Navbar hiển thị đúng brand, menu, route active và trạng thái guest/customer'),
      feature('Auth menu, avatar và cart count', '/', 'Customer', '_Layout.cshtml; CartController.GetCartCount', 'mở user menu, profile, cart và logout', 'User menu, avatar/initials, cart count và logout hoạt động đúng'),
      feature('Region selector và cookie khu vực', '/', 'Guest/Customer', 'HomeController.SetRegion; AreaSelectorViewComponent', 'chọn khu vực, reload /Car và /Car/MapSearch', 'Khu vực được lưu, hiển thị và lọc dữ liệu đúng; không tràn navbar'),
      feature('Mobile navbar collapse/offcanvas', '/', 'Guest/Customer', '_Layout.cshtml; site.css', 'mở hamburger, cuộn menu, chọn link, back-forward', 'Mobile menu cuộn được, không che nội dung và không kẹt scroll'),
      feature('Footer và link pháp lý', '/Home/Privacy, /Home/Terms', 'Guest', '_Layout.cshtml; HomeController', 'kiểm tra Privacy, Terms, Blog, Contact/Support link', 'Footer dùng link thật, không href="#" ở link pháp lý quan trọng'),
      feature('Toast/TempData public chống XSS', '/', 'Guest/Customer', '_Layout.cshtml', 'kích hoạt message chứa ký tự HTML/script giả', 'Toast render message như text an toàn, không chạy script'),
      feature('PWA manifest/offline/service worker', '/manifest.webmanifest, /offline.html, /sw.js', 'Guest', 'wwwroot manifest/sw/offline', 'mở manifest, offline page, sw.js và kiểm tra icon', 'PWA assets hợp lệ, offline page không stack trace và không cache sai route nhạy cảm'),
    ],
  },
  {
    code: 'MD02', title: 'Auth Account Session', req: 'AUTH01', color: colors.blue,
    features: [
      feature('Đăng ký Customer với consent', '/Auth/Register', 'Guest', 'AuthController.Register; RegisterViewModel', 'nhập thông tin hợp lệ, tick Terms/Privacy/consent và submit', 'Tạo tài khoản đúng role, lưu consent version và không lưu plaintext PII'),
      feature('Đăng ký validation tuổi/email/phone/CCCD/GPLX/password', '/Auth/Register', 'Guest', 'AuthController.Register', 'thử email sai/trùng, tuổi dưới 18, CCCD/GPLX sai, password yếu', 'Form bị chặn đúng trường và không tạo user rác'),
      feature('Xác thực email OTP', '/Auth/VerifyEmailStep1, /Auth/VerifyEmailStep2', 'Guest', 'AuthController.VerifyEmailStep1/VerifyEmailStep2/ResendOtp', 'nhập email, OTP đúng/sai/hết hạn và resend', 'OTP đúng verify email; OTP sai/hết hạn không verify và không lộ OTP'),
      feature('Đăng nhập Customer và cookie auth', '/Auth/Login', 'Customer', 'AuthController.Login; cypress/e2e/01-auth', 'login customer@cypress.local / Cypress@123', 'Đăng nhập thành công, cookie an toàn, user menu xuất hiện'),
      feature('Đăng nhập sai và rate limit', '/Auth/Login', 'Guest', 'AuthController.Login; RateLimitingMiddleware', 'nhập sai password nhiều lần với email tồn tại/không tồn tại', 'Không account enumeration; sau ngưỡng bị throttle/rate limit'),
      feature('MFA cho Admin/Manager/Staff', '/Auth/VerifyMfa', 'Admin/Manager/Staff', 'AuthController.VerifyMfa/ResendMfa', 'login privileged user, nhập OTP đúng/sai/hết hạn, resend', 'Privileged user không vào dashboard khi chưa qua MFA nếu policy bật'),
      feature('Forgot/reset password', '/Auth/ForgotPassword, /Auth/ResetPassword', 'Guest', 'AuthController.ForgotPassword/ResetPassword', 'gửi email reset, dùng token đúng/sai/hết hạn, đặt password mới', 'Token một lần, hết hạn đúng và không tiết lộ email tồn tại'),
    ],
  },
  {
    code: 'MD03', title: 'Profile eKYC PII', req: 'EKYC01', color: colors.purple,
    features: [
      feature('Profile hiển thị và mask PII', '/Auth/Profile', 'Customer', 'AuthController.Profile', 'mở profile, kiểm tra CCCD/GPLX/phone/avatar/status eKYC', 'PII nhạy cảm được mask, không lộ plaintext trong DOM'),
      feature('Cập nhật hồ sơ cá nhân', '/Auth/Profile', 'Customer', 'AuthController.Profile', 'sửa tên, phone, địa chỉ/ngày sinh nếu UI cho phép', 'Chỉ trường được phép được cập nhật, role/verified flags không bị user đổi'),
      feature('PII PIN set/verify', '/Auth/SetPiiPin, /Auth/VerifyPiiPin', 'Customer', 'AuthController.SetPiiPin/VerifyPiiPin/HasPiiPin', 'thiết lập PIN 6 số, verify đúng/sai/nhiều lần', 'PIN được hash, verify sai không mở PII và có guard/rate limit'),
      feature('Upload/scan CCCD và GPLX', '/Ekyc', 'Customer', 'EkycController.ScanIdCard/ScanLicense; EkycService', 'upload ảnh CCCD/GPLX hợp lệ, chạy scan id card/license', 'File hợp lệ được xử lý secure, OCR/vendor lỗi có fallback rõ'),
      feature('eKYC reject file giả mạo/quá dung lượng', '/Ekyc', 'Customer', 'FileUploadValidationService; EkycController', 'upload exe đổi đuôi jpg, file rỗng, MIME sai, quá dung lượng', 'File sai bị chặn bằng server-side validation và không lưu file độc hại'),
      feature('Face verification', '/Ekyc', 'Customer', 'EkycController.VerifyFace; EkycService', 'upload ảnh mặt hợp lệ, ảnh không mặt, ảnh nhiều mặt, vendor timeout', 'Face verify cập nhật đúng trạng thái, log không chứa raw PII'),
      feature('SecureFile ownership', '/SecureFile/GetSecureImage', 'Customer/Admin', 'SecureFileController.GetSecureImage/DeleteSecureFile', 'Customer A mở ảnh của mình, Customer B thử mở ảnh của A, Admin mở/xóa', 'Chỉ chủ sở hữu/Admin đúng quyền xem/xóa secure file; không path traversal'),
    ],
  },
  {
    code: 'MD04', title: 'Home Catalog Search', req: 'CAT01', color: colors.teal,
    features: [
      feature('Trang chủ xe nổi bật và CTA', '/', 'Guest/Customer', 'HomeController.Index', 'quan sát hero/search/filter/featured cars và nhấn CTA', 'Chỉ xe approved/available hiển thị; CTA dẫn đúng catalog/detail'),
      feature('Danh sách xe filter nhiều tiêu chí', '/Car', 'Guest/Customer', 'CarController.Index', 'lọc brand, type, seat, price, location, service type', 'Kết quả đúng filter, không hiện xe deleted/pending/unavailable'),
      feature('Search text chống SQL/XSS và bỏ dấu', '/Car', 'Guest/Customer', 'CarController.Index; RemoveDiacritics', 'tìm xe bằng tiếng Việt và payload script/SQL giả', 'Search an toàn, không SQL error, payload được encode'),
      feature('Load more/pagination catalog', '/Car/LoadMoreCars', 'Guest/Customer', 'CarController.LoadMoreCars; CatalogCacheService', 'load thêm xe sau khi áp filter và khi hết dữ liệu', 'Không duplicate/mất item, nút disable đúng khi hết dữ liệu'),
      feature('Map search marker và khoảng cách', '/Car/MapSearch', 'Guest/Customer', 'CarController.MapSearch/GetAllCarsWithDistance', 'cho phép/từ chối geolocation, chọn marker, mở detail', 'Map fallback tốt, marker đúng và xe thiếu tọa độ không crash'),
      feature('Delivery search phạm vi giao xe', '/Car/DeliverySearch', 'Guest/Customer', 'CarController.DeliverySearch/CalculateDelivery', 'nhập địa chỉ trong/ngoài phạm vi, kiểm tra phí', 'Xe/phí delivery được tính đúng, địa chỉ sai có message rõ'),
      feature('Catalog models API cho dropdown', '/CarCatalog/Models', 'Guest/Customer', 'CarCatalogController.Models', 'gọi API với brandId hợp lệ/sai/âm/chữ', 'JSON schema sạch, input sai không 500 và UI reset dropdown đúng'),
    ],
  },
  {
    code: 'MD05', title: 'Car Detail Review', req: 'CAR01', color: colors.teal,
    features: [
      feature('Chi tiết xe và gallery/fallback ảnh', '/Car/Details/{id}', 'Guest/Customer', 'CarController.Details', 'mở detail xe có ảnh và xe thiếu ảnh', 'Thông tin xe/giá/ảnh/gallery đúng, không lộ dữ liệu chủ xe quá mức'),
      feature('Busy dates calendar', '/Car/BusyDates', 'Customer', 'CarController.BusyDates; BookingStatusConstants', 'chọn khoảng ngày trùng booking/hold và khoảng ngày rảnh', 'Ngày bận bị disable/cảnh báo, không lệch timezone'),
      feature('Delivery fee ở detail', '/Car/Details/{id}', 'Customer', 'CarController.CalculateDelivery; BookingQuoteService', 'nhập địa chỉ giao xe, tính phí, so sánh checkout', 'Phí detail và checkout nhất quán'),
      feature('Review xe sau booking completed', '/Car/AddReview, /Review/Create', 'Customer', 'CarController.AddReview; ReviewController.Create', 'gửi rating/comment với booking completed và không completed', 'Chỉ customer đủ điều kiện được review; comment được encode/sanitize'),
      feature('Đặt xe từ detail theo auth/eKYC', '/Car/Details/{id}', 'Guest/Customer', 'BookingController.Book', 'guest, customer unverified, customer verified nhấn book', 'Redirect/chặn/cho phép đúng theo auth và eKYC'),
      feature('Thêm giỏ từ detail theo auth/eKYC', '/Car/Details/{id}', 'Guest/Customer', 'CartController.AddToCart', 'guest, customer unverified, customer verified nhấn add cart', 'Redirect/chặn/cho phép đúng theo auth, eKYC và availability'),
      feature('Admin-only action không lộ public', '/Car/Create, /Car/Manage', 'Guest/Customer/Admin', 'CarController; Areas/Admin/CarController', 'truy cập route admin-like bằng guest/customer và admin', 'Guest/customer bị chặn, admin dùng đúng admin area'),
    ],
  },
  {
    code: 'MD06', title: 'Booking Single Flow', req: 'BOOK01', color: colors.blue,
    features: [
      feature('Tạo booking một xe từ detail', '/Booking/Book, /Booking/Checkout', 'Customer', 'BookingController.Book/Checkout; ReservationService', 'chọn xe available, ngày giờ hợp lệ, add-on/delivery/insurance rồi checkout', 'Tạo booking/hold đúng, quote đầy đủ và không duplicate double-click'),
      feature('Reject ngày nhận/trả không hợp lệ', '/Booking/Book, /Booking/Checkout', 'Customer', 'BookingController; BookingBusinessRuleService', 'ngày quá khứ, trả trước nhận, duration vượt rule', 'Server-side reject, không tạo hold/booking rác'),
      feature('Concurrency hai user cùng xe', '/Booking/Checkout', 'Customer', 'ReservationConcurrencyTests; ReservationService', 'hai customer đặt cùng xe cùng khoảng ngày gần đồng thời', 'Chỉ một booking/hold thắng, không overlap availability-blocking'),
      feature('Quote nhất quán GET/POST/payment', '/Booking/Checkout/{id}', 'Customer', 'BookingController.Checkout; BookingQuoteService', 'so sánh giá ở checkout, deposit/payment và transaction', 'Tổng tiền/discount/add-on/delivery/deposit không đổi sai'),
      feature('Checkout bắt buộc Terms/Privacy/self-drive consent', '/Booking/Checkout/{id}', 'Customer', 'BookingController.Checkout; LegalComplianceService', 'submit thiếu consent rồi tick đầy đủ', 'Thiếu consent bị chặn; version/IP/user-agent được lưu khi đồng ý'),
      feature('Promotion/voucher trong booking', '/api/Voucher/Check, /Booking/Checkout', 'Customer', 'VoucherController.Check; Promotion; BookingQuoteService', 'nhập mã đúng/sai/hết hạn/hết lượt/chưa đủ điều kiện', 'Discount áp dụng đúng, không vượt tổng và không abuse per-user/global'),
      feature('MyBookings, detail, cancel, pickup/return', '/Booking/MyBookings, /Booking/Details/{id}', 'Customer', 'BookingController; BookingTransitionService; RefundService', 'xem danh sách/detail, hủy booking, xác nhận nhận/trả xe', 'Ownership, transition, refund/hold release và trạng thái xe đồng bộ'),
    ],
  },
  {
    code: 'MD07', title: 'Cart Multi Checkout', req: 'CART01', color: colors.blue,
    features: [
      feature('Thêm xe vào giỏ và cart count', '/Cart/AddToCart, /Cart', 'Customer', 'CartController.AddToCart/GetCartCount', 'thêm xe từ detail, mở giỏ, kiểm tra navbar count', 'Item đúng xe/ngày/giá và cart count tăng đúng'),
      feature('Giỏ nhiều xe tính tổng', '/Cart', 'Customer', 'CartController.Index; BillingService', 'thêm nhiều xe, add-on, delivery rồi kiểm tra từng dòng và tổng', 'Tổng tiền = tổng dòng đúng rounding; xe unavailable được cảnh báo'),
      feature('Cập nhật ngày trong giỏ', '/Cart/UpdateCartItemDates', 'Customer', 'CartController.UpdateCartItemDates; ReservationService', 'đổi ngày hợp lệ và ngày trùng booking/quá khứ', 'Ngày hợp lệ cập nhật quote; ngày sai bị reject'),
      feature('Toggle add-on/package/driver', '/Cart/ToggleAddon, /Cart/UpdateCartItemPackage, /Cart/UpdateCartItemDriver', 'Customer', 'CartController', 'bật/tắt add-on, đổi package, chọn/bỏ driver nếu có', 'Quote cập nhật đúng, add-on inactive/driver trái policy bị chặn'),
      feature('Xóa item và empty state', '/Cart/RemoveFromCart', 'Customer', 'CartController.RemoveFromCart/GetCartCount', 'xóa một item, reload, xóa item cuối cùng', 'Chỉ xóa item của user hiện tại, cart count không âm, empty state rõ'),
      feature('CheckoutAll multi-car transaction', '/Cart/CheckoutAll', 'Customer', 'CartController.CheckoutAll; PayOSPaymentService', 'checkout nhiều item hợp lệ với consent', 'Tạo transaction/snapshot đầy đủ, atomic theo policy và consent version được lưu'),
      feature('Cart CSRF và IDOR', '/Cart/* POST', 'Customer', 'CartController POST actions; global antiforgery', 'POST thiếu token, token sai, id item của user khác', 'CSRF/IDOR bị chặn; không sửa/xóa dữ liệu user khác'),
    ],
  },
  {
    code: 'MD08', title: 'Payment Voucher Refund', req: 'PAY01', color: colors.orange,
    features: [
      feature('Tạo PayOS payment cho deposit/hold', '/Payment/CreatePayment, /Payment/Hold', 'Customer', 'PaymentController; PayOSPaymentService', 'tạo payment từ booking/cart, kiểm tra payment URL/orderCode/amount', 'Payment/transaction tạo đúng amount, orderCode unique, không double-click duplicate'),
      feature('PayOS webhook paid idempotent', '/Payment/Webhook', 'System', 'PaymentController.Webhook; PayOSPaymentService; PayOSPaymentWebhookTests', 'gửi webhook signature đúng, gửi lại, gửi amount mismatch/signature sai', 'Webhook đúng cập nhật payment/booking; webhook lặp không duplicate; sai bị reject'),
      feature('Success/cancel/pending routes', '/Payment/Success, /Payment/Cancel, /Payment/Pending', 'Customer', 'PaymentController.Success/Cancel/Pending/SuccessTransaction', 'mở return/cancel/pending với params đúng/sai/user khác', 'Chỉ báo thành công khi backend paid; không hủy/sửa transaction user khác'),
      feature('Manual payment confirmation', '/Booking/ConfirmManualPayment, /Admin/Payment/ConfirmPayment', 'Customer/Admin', 'BookingController; Admin/PaymentController', 'customer thử confirm, admin confirm booking/payment đúng/sai', 'Customer không tự paid; admin confirm idempotent và audit đầy đủ'),
      feature('Refund request/approve/reject', '/Admin/Payment/RequestRefund, /Admin/Payment/ApproveRefund', 'Customer/Admin', 'RefundService; Admin/PaymentController', 'tạo refund eligible, approve/reject, kiểm tra ledger/payout', 'Refund không vượt paid amount, không double payout, trạng thái đồng bộ'),
      feature('Voucher API brute force/input', '/api/Voucher/Check', 'Customer', 'VoucherController.Check; Promotion', 'gửi mã đúng/sai/dài/ký tự đặc biệt/nhiều request', 'API trả schema an toàn, mã sai không lộ danh sách và không 500'),
      feature('Admin Payment index/filter', '/Admin/Payment', 'Admin', 'Areas/Admin/PaymentController; Views/Admin/Payment', 'lọc giao dịch theo status/ngày/user/booking', 'Dữ liệu tài chính định dạng VND, không lộ PayOS secret'),
    ],
  },
  {
    code: 'MD09', title: 'Contract Handover', req: 'CON01', color: colors.purple,
    features: [
      feature('Physical contract upload/classify/confirm', '/Admin/PhysicalContracts', 'Admin/Manager/Staff', 'Admin/PhysicalContractsController', 'upload hợp đồng cho booking, classify, confirm type', 'File validate, contract gắn đúng booking, audit đầy đủ'),
      feature('Customer xem hợp đồng đúng quyền', '/PhysicalContracts, /Booking/Contract', 'Customer/Admin/Staff', 'BookingController.Contract; PhysicalContractsController', 'customer chủ booking xem, customer khác thử mở, admin/staff mở', 'Ownership/RBAC đúng, không lộ PII/path quá mức'),
      feature('Digital contract legacy routes', '/Booking/Contract, /Booking/TransactionContract, /Contract/Create', 'Customer/Admin', 'BookingController; ContractController.Create; Program.cs redirects', 'mở route legacy và POST nếu còn form', 'Redirect/AccessDenied an toàn, không tạo contract thiếu dữ liệu'),
      feature('Ký hợp đồng/evidence', '/Booking/SignContract', 'Customer', 'BookingController.SignContract', 'ký hợp đồng, thiếu consent, ký lặp, user khác ký', 'Chỉ chủ booking ký, lưu signature/IP/user-agent/version và idempotent'),
      feature('Handover CheckIn', '/Handover/Capture?type=CheckIn', 'Customer/Staff/Admin', 'HandoverController.Capture/Submit', 'nhập ODO, nhiên liệu, tình trạng, upload ảnh, ký tên', 'Tạo đúng một CheckIn, file/signature validate, trạng thái booking/xe đúng'),
      feature('Handover CheckOut', '/Handover/Capture?type=CheckOut', 'Customer/Staff/Admin', 'HandoverController.Submit; BookingTransitionService', 'nhập ODO cuối, ảnh, phí phát sinh, submit nhanh hai lần', 'Một CheckOut hợp lệ, ODO cuối >= đầu, double-submit bị chặn'),
      feature('Handover reject trạng thái/file/signature sai', '/Handover/Capture', 'Customer/Staff/Admin', 'HandoverController; FileUploadValidationService', 'booking sai trạng thái, thiếu chữ ký, base64 hỏng, ảnh sai, ODO âm', 'Không tạo record, không crash Convert.FromBase64String'),
    ],
  },
  {
    code: 'MD10', title: 'LongTerm Rentals', req: 'LONG01', color: colors.green,
    features: [
      feature('Trang thuê dài hạn và gói', '/LongTerm', 'Guest/Customer', 'LongTermController.Index; ContractType', 'mở trang, kiểm tra gói Theo ngày/tuần/tháng/năm, CTA', 'Gói active hiển thị đúng discount/free maintenance và không vỡ card'),
      feature('RequestForm thuê dài hạn', '/LongTerm/RequestForm', 'Customer', 'LongTermController.RequestForm/SubmitRequest', 'chọn gói, xe, ngày, địa điểm, ghi chú rồi submit', 'Request hợp lệ tạo đúng trạng thái; dữ liệu sai bị reject'),
      feature('CalculatePrice/GetAvailableCars', '/LongTerm/CalculatePrice, /LongTerm/GetAvailableCars', 'Customer', 'LongTermController', 'gọi API với gói/ngày/xe hợp lệ/sai', 'Giá discount đúng, xe bận không được trả về, input sai không 500'),
      feature('ContractPreview thuê dài hạn', '/LongTerm/ContractPreview/{id}', 'Customer', 'LongTermController.ContractPreview', 'mở preview, kiểm tra thông tin xe/thời hạn/giá/lịch thanh toán', 'Preview đúng owner, user khác bị chặn, link Terms/Privacy thật'),
      feature('ConfirmAndDeposit thuê dài hạn', '/LongTerm/ConfirmAndDeposit', 'Customer', 'LongTermController.ConfirmAndDeposit; PaymentController', 'confirm request, tạo payment deposit/kỳ đầu', 'Trạng thái request/payment đúng, không confirm lại request đã paid'),
      feature('VehicleInspection định kỳ', '/LongTerm/VehicleInspection', 'Customer/Staff/Admin', 'LongTermController.VehicleInspection; InspectionRecord', 'nhập ODO/tình trạng/ảnh/chi phí inspection', 'Inspection chỉ tạo cho rental hợp lệ, ODO/file validate'),
      feature('Long-term maintenance/payment schedule', '/LongTerm/ContractPreview/{id}', 'Customer/Admin', 'LongTermRentalService; PaymentSchedule; MaintenanceLog', 'kiểm tra lịch thanh toán/bảo dưỡng theo gói dài hạn', 'Schedule đúng kỳ hạn, không trùng kỳ và không thiếu free maintenance'),
    ],
  },
  {
    code: 'MD11', title: 'Owner Partner Driver', req: 'P2P01', color: colors.green,
    features: [
      feature('OwnerCars dashboard', '/OwnerCars', 'Owner/Partner', 'OwnerCarsController.Index', 'mở danh sách xe của tôi, trạng thái duyệt, booking/revenue nếu có', 'Chỉ hiện xe của owner hiện tại; không lộ xe người khác'),
      feature('OwnerCars create P2P car', '/OwnerCars/Create', 'Owner/Partner', 'OwnerCarsController.Create; Admin/CarApprovalController', 'nhập thông tin xe, upload ảnh, submit', 'Xe tạo với owner đúng, pending approval, chưa hiện public'),
      feature('OwnerCars validation biển số/VIN/giá/ảnh', '/OwnerCars/Create', 'Owner/Partner', 'OwnerCarsController.Create; Models/Car', 'nhập biển số/VIN trùng/sai, giá âm, thiếu field, file sai', 'Validation chặn và không lộ DbUpdateException'),
      feature('Partner chưa duyệt bị chặn đăng xe', '/OwnerCars/Create', 'Customer/Admin', 'OwnerCarsController; Admin/UserController.ApprovePartner', 'customer chưa approved mở create, admin approve rồi thử lại', 'Chưa duyệt bị chặn server-side; approve mở quyền đúng'),
      feature('Admin duyệt/từ chối xe owner', '/Admin/CarApproval', 'Admin/Manager/Staff', 'Admin/CarApprovalController.Approve/Reject', 'approve/reject xe pending, kiểm tra catalog và owner dashboard', 'Approve/reject cập nhật đúng trạng thái và lý do'),
      feature('Driver/with-driver surface đóng an toàn', '/Driver, /Admin/Driver, /Cart/UpdateCartItemDriver', 'Guest/Customer/Admin', 'Program.cs redirects; ServiceTypeConstants; legal compliance', 'mở route driver legacy và thử chọn driver khi feature public disabled', 'Route legacy an toàn; không bán dịch vụ tài xế khi chưa bật/policy chưa đủ'),
      feature('Partner revenue/booking visibility', '/OwnerCars', 'Owner/Partner', 'OwnerCarsController.Index; Booking/Payment models', 'kiểm tra doanh thu, booking active/completed theo xe owner', 'Owner chỉ thấy booking/doanh thu xe của mình, số tiền khớp payment/ledger'),
    ],
  },
  {
    code: 'MD12', title: 'Membership SelfService', req: 'MEM01', color: colors.green,
    features: [
      feature('Membership public tiers', '/Membership', 'Guest/Customer', 'MembershipController.Index; MembershipTier', 'mở trang, kiểm tra hạng/quyền lợi/progress customer', 'Tier active hiển thị đúng, không mâu thuẫn tên hạng'),
      feature('Loyalty points/tier upgrade', '/Membership', 'Customer/System', 'MembershipTier; Booking/Payment completion services', 'hoàn tất booking, kiểm tra điểm/tier sau completion/refund', 'Điểm cộng đúng một lần, refund/cancel xử lý đúng policy'),
      feature('Admin Membership update', '/Admin/Membership', 'Admin', 'Admin/MembershipController', 'sửa tier/contract, toggle active, kiểm tra public', 'Admin update hợp lệ, dữ liệu âm/trùng bị reject'),
      feature('SelfService Summary', '/SelfService/Summary', 'Customer', 'SelfServiceController.Summary', 'mở summary booking/payment/privacy/membership', 'Chỉ dữ liệu user hiện tại, PII mask, không lỗi khi rỗng'),
      feature('Membership contract toggle', '/Admin/Membership', 'Admin', 'Admin/MembershipController.ToggleContract', 'bật/tắt hợp đồng/quyền lợi membership và reload public page', 'Toggle phản ánh đúng, không làm mất quyền lợi user hiện tại trái policy'),
      feature('Customer tier display on profile/checkout', '/Auth/Profile, /Booking/Checkout', 'Customer', 'MembershipTier; CheckoutQuoteViewModel', 'kiểm tra hạng thành viên và ưu đãi trong profile/checkout', 'Tier và discount hiển thị nhất quán, không double discount'),
      feature('SelfService empty state', '/SelfService/Summary', 'Customer', 'SelfServiceController.Summary; _EmptyState', 'mở bằng user chưa có booking/payment/support/privacy', 'Không 500, hiển thị empty state và CTA hợp lý'),
    ],
  },
  {
    code: 'MD13', title: 'Blog Content API', req: 'BLOG01', color: colors.purple,
    features: [
      feature('Blog public list/search/category/tag', '/blog', 'Guest', 'BlogController.Index/Category/Tag', 'mở list, search, filter category/tag, pagination', 'Chỉ bài published hiển thị, search/filter an toàn'),
      feature('Blog detail sanitizer', '/blog/{slug}', 'Guest/Admin', 'BlogController.Detail; BlogService', 'mở bài có HTML và payload script/onerror giả', 'Content render đúng nhưng script/event handler bị sanitize'),
      feature('Blog public API', '/api/blogs', 'Guest', 'BlogApiController', 'gọi list/latest/featured/search/detail với input đúng/sai', 'JSON schema sạch, không trả draft/private fields'),
      feature('Admin Blog create/edit', '/Admin/Blog/Create, /Admin/Blog/Edit', 'Admin', 'Admin/BlogController', 'nhập title/slug/content/category/image/status', 'Slug unique, content sanitize, ảnh validate'),
      feature('Admin Blog toggle/delete', '/Admin/Blog', 'Admin', 'Admin/BlogController.ToggleStatus/Delete', 'toggle published/draft, delete bài, kiểm tra public/API', 'Visibility cập nhật đúng; role khác bị chặn'),
      feature('Blog API admin mutate RBAC', '/api/blogs POST/PUT/DELETE', 'Admin/Guest', 'BlogApiController.Create/Update/Delete', 'guest/customer/admin gọi create/update/delete', 'Chỉ Admin mutate được, payload sai không 500'),
      feature('Blog related/sidebar/featured widgets', '/blog/{slug}', 'Guest', 'Views/Blog partials', 'mở featured, related, sidebar, category filter, search partials', 'Widget không duplicate bài, empty state tốt và không vỡ layout'),
    ],
  },
  {
    code: 'MD14', title: 'Support Privacy Contact', req: 'SUP01', color: colors.orange,
    features: [
      feature('Support ticket customer create/mine', '/Support/Create, /Support/Mine', 'Customer', 'SupportController; SupportTicketService', 'tạo ticket, mở Mine, kiểm tra status/message', 'Ticket thuộc user hiện tại, content encode và validation đúng'),
      feature('Admin Support update status', '/Admin/Support', 'Admin/Manager/Staff', 'Admin/SupportController', 'lọc ticket, update Open/InProgress/Resolved/Closed', 'Status transition đúng và customer thấy cập nhật'),
      feature('Contact messages admin respond/delete', '/Admin/Contacts', 'Guest/Admin', 'Admin/ContactsController', 'tạo contact, xem detail, respond, delete', 'Respond/delete an toàn, content encode, SMTP lỗi không crash UI'),
      feature('Privacy request export/delete', '/Privacy/RequestExport, /Privacy/RequestDelete', 'Customer/Admin', 'PrivacyController; PrivacyRequestService', 'customer tạo request export/delete, admin xem', 'Request có due date/SLA/scope/status Pending'),
      feature('Admin privacy approve/reject/complete/retention', '/Admin/Privacy', 'Admin/Manager', 'Admin/PrivacyController', 'approve, reject, complete request và mở RetentionPreview', 'Status/evidence/manifest đúng; preview không mutate dữ liệu'),
      feature('Privacy request duplicate/spam guard', '/Privacy/RequestExport, /Privacy/RequestDelete', 'Customer', 'PrivacyRequestService', 'gửi nhiều request cùng loại liên tiếp và request khi đang pending', 'Không tạo duplicate spam trái policy, message hướng dẫn rõ'),
      feature('Support/admin privacy area/RBAC', '/Admin/Support, /Admin/Privacy', 'Admin/Manager/Staff/Customer', 'RoleAuthorize; Admin controllers', 'truy cập bằng role khác nhau và kiểm tra list/detail', 'Role ngoài quyền bị chặn, Staff/Manager bị giới hạn dữ liệu theo policy'),
    ],
  },
  {
    code: 'MD15', title: 'Tracking IoT Maps', req: 'TRK01', color: colors.teal,
    features: [
      feature('Tracking Map và marker xe', '/Tracking/Map, /Admin/Tracking/Map', 'Admin/Customer', 'TrackingController; Admin/TrackingController; TrackingService', 'mở map, chọn xe, xem marker/timestamp/route', 'Marker đúng tọa độ, role không phép bị chặn'),
      feature('IoT ReceiveData HMAC/nonce', '/Tracking/ReceiveData', 'System', 'TrackingController.ReceiveData; VehicleTracking; TrackingIngestNonce', 'gửi payload hợp lệ, replay nonce, signature sai, timestamp cũ', 'Payload đúng lưu tracking; replay/signature sai bị reject'),
      feature('Admin live location APIs', '/Admin/Tracking/GetLiveLocations, /Admin/Tracking/GetCarLocation', 'Admin/Manager/Staff', 'Admin/TrackingController', 'gọi APIs với carId hợp lệ/sai và auto refresh map', 'JSON đúng schema, xe ngoài quyền không trả về'),
      feature('Tracking retention/legal notice', '/Home/Privacy, /Admin/Privacy', 'Customer/Admin', 'legal compliance audit; PrivacyRequestService', 'kiểm tra notice GPS, privacy export/delete scope và retention preview', 'Tracking data có notice/scope/retention rõ hoặc ghi risk'),
      feature('Tracking connect page', '/Tracking/Connect', 'Admin', 'TrackingController.Connect', 'mở hướng dẫn kết nối IoT và kiểm tra token/QR nếu có', 'Không lộ secret dài hạn; hướng dẫn rõ và layout không vỡ'),
      feature('Vehicle tracking stale/out-of-range data', '/Admin/Tracking/Map', 'Admin', 'TrackingService; VehicleTracking', 'tạo dữ liệu GPS cũ, lat/lng sai phạm vi, tốc độ âm/quá cao', 'Dữ liệu sai bị reject hoặc đánh dấu bất thường, không crash map'),
      feature('Tracking route geofence/area isolation', '/Admin/Tracking', 'Admin/Manager/Staff', 'AreaAuthorizationHandler; TrackingService', 'Manager/Staff xem tracking xe trong và ngoài area', 'Chỉ xe thuộc area được quyền hiển thị, không IDOR qua carId'),
    ],
  },
  {
    code: 'MD16', title: 'AI Public APIs', req: 'API01', color: colors.purple,
    features: [
      feature('AI chat safe answer and PII redaction', '/api/ai/chat', 'Guest/Customer', 'AiController.Chat; AiService', 'hỏi giá thuê/chính sách và nhập CCCD/GPLX/booking dispute nhạy cảm', 'AI không gửi/lộ PII trái policy, vendor lỗi có fallback'),
      feature('Chat API payload/rate limit', '/api/Chat/Send, /api/ai/chat', 'Guest/Customer', 'ChatController.Send; RateLimitingMiddleware', 'gửi message hợp lệ, rỗng, quá dài, JSON sai, nhiều request', 'Validation/rate limit đúng, không 500 và không session chéo user'),
      feature('Health/live/ready', '/live, /ready, /health', 'Guest/System', 'Program.cs health endpoints', 'gọi endpoints trong trạng thái config đủ/thiếu', 'JSON checks ổn định, không lộ connection string/secret'),
      feature('Guide page', '/Guide', 'Guest', 'GuideController.Index', 'mở hướng dẫn theo role và kiểm tra link/nội dung', 'Guide không XSS, link đúng và không mojibake'),
      feature('Alert page/user alerts', '/Alert', 'Admin/Customer', 'AlertController.Index; Admin/AlertController.Resolve', 'mở alert và resolve alert nếu có quyền', 'Alert hiển thị/resolve đúng và empty state rõ'),
      feature('AI tool executor boundaries', '/api/ai/chat', 'Customer/Admin', 'Services/Ai/AiToolExecutor', 'hỏi AI thực hiện thao tác nhạy cảm như hủy booking/thanh toán/xem PII', 'AI không tự mutate dữ liệu nhạy cảm nếu không có xác nhận/quyền'),
      feature('AI provider fallback Groq/Gemini', '/api/ai/chat', 'System', 'GroqChatService; GeminiChatService; ConfigurationGuard', 'mô phỏng provider timeout/missing key/rate limit', 'Fallback/error message rõ, không lộ key/model secret'),
    ],
  },
  {
    code: 'MD17', title: 'Admin Dashboard SRE', req: 'ADM01', color: colors.dark,
    features: [
      feature('Admin dashboard KPI/chart/notification', '/Admin/Dashboard', 'Admin', 'Admin/DashboardController', 'xem KPI, chart, recent activity, notification bell', 'KPI đúng dữ liệu, chart có empty state, notification JSON ổn định'),
      feature('Admin sidebar/offcanvas role menus', '/Admin/Dashboard', 'Admin/Manager/Staff', '_AdminLayout.cshtml; RoleAuthorize', 'đăng nhập từng role, mở nhóm menu, kiểm tra active state', 'Menu theo role đúng, mobile offcanvas không vỡ'),
      feature('Enterprise Center', '/Admin/Enterprise', 'Admin/Manager/Staff', 'Admin/EnterpriseController; EnterpriseOperationsService', 'mở command center, readiness, snapshot actions nếu có', 'Trang tải ổn, role/action đúng quyền'),
      feature('SRE SLO page', '/Admin/Sre/Slo', 'Admin/Manager', 'Admin/SreController.Slo; SloMetricsService', 'mở SLO/error budget, thử role Staff/Customer', 'Admin/Manager xem được; role khác bị chặn'),
      feature('Admin theme dark/light', '/Admin/Dashboard', 'Admin', '_AdminLayout; admin-theme.css', 'toggle dark/light và mở nhiều admin pages', 'Theme lưu localStorage, contrast và dropdown/modal đúng'),
      feature('Admin badge counts', '/Admin/Dashboard', 'Admin/Manager/Staff', 'AdminBadgeCountsViewComponent', 'tạo booking/car/user pending rồi reload sidebar', 'Badge count đúng dữ liệu và theo quyền area'),
      feature('Admin global search/topbar/profile/logout', '/Admin/Dashboard', 'Admin/Manager/Staff', '_AdminLayout.cshtml', 'dùng search/topbar/profile dropdown/logout', 'Topbar responsive tốt, logout xóa session và không kẹt offcanvas'),
    ],
  },
  {
    code: 'MD18', title: 'Admin Area RBAC', req: 'AREA01', color: colors.dark,
    features: [
      feature('Area CRUD', '/Admin/Area', 'Admin', 'Admin/AreaController', 'tạo/sửa/show/delete/restore/permanent delete area', 'CRUD đúng, validate tên/toạ độ/trạng thái, audit đầy đủ'),
      feature('Manager/Staff area isolation', '/Admin/Area, /Admin/Car/Manage, /Admin/Booking', 'Manager/Staff/Viewer', 'AreaAuthorizationHandler; AdminAreaControllerBase', 'truy cập Area A được gán và Area B bằng id trực tiếp', 'Không xem/sửa dữ liệu ngoài area'),
      feature('Area location add/remove', '/Admin/AreaLocation/Add, /Admin/AreaLocation/Remove', 'Admin/Manager', 'Admin/AreaLocationController; AreaApiController.GetLocations', 'gán/remove location, add duplicate/id sai', 'Location mapping đúng, duplicate/id sai bị chặn'),
      feature('Area staff add/update/remove', '/Admin/AreaStaff', 'Admin/Manager', 'Admin/AreaStaffController; AreaStaffRoleConstants', 'add staff, update role, remove, thử role sai case', 'Role normalized, Manager không tự nâng quyền vượt policy'),
      feature('Area API GetUsers/GetLocations', '/admin/api', 'Admin/Manager', 'Admin/AreaApiController.GetUsers/GetLocations', 'gọi API bằng Admin/Manager/Staff/Customer với query hợp lệ/sai', 'Schema không chứa PII/password hash, role sai bị 401/403'),
      feature('Area manager layout', '/Admin/AreaDashboard', 'Manager/Staff', '_AreaManagerLayout.cshtml; AreaSelectorViewComponent', 'mở area selector/sidebar trên desktop/mobile', 'Selector chỉ hiện area được quyền, sidebar theo role đúng'),
      feature('Area stats and all areas dashboard', '/Admin/AreaStats/Stats, /Admin/AreaManager/AllAreas', 'Admin/Manager', 'AreaStatsController; AreaManagerController', 'mở thống kê/all areas và thử areaId ngoài quyền', 'Stats đúng quyền, không lộ dữ liệu ngoài area'),
    ],
  },
  {
    code: 'MD19', title: 'Admin Booking Ops', req: 'ADBK01', color: colors.dark,
    features: [
      feature('Admin Booking index/filter/pagination', '/Admin/Booking', 'Admin/Manager/Staff', 'Admin/BookingController.Index; BookingService', 'lọc booking theo status/date/area/customer/car, mở detail', 'Danh sách phân trang, không load toàn bộ, area scope đúng'),
      feature('Admin booking confirm/cancel/update status', '/Admin/Booking', 'Admin/Manager/Staff', 'Admin/BookingController; BookingTransitionService', 'confirm/cancel/update trạng thái hợp lệ và không hợp lệ', 'State machine chặn transition sai và audit đúng'),
      feature('Admin start/end trip/pickup/return', '/Admin/Booking', 'Admin/Manager/Staff', 'Admin/BookingController.ConfirmPickup/StartTrip/EndTrip/CompleteReturn', 'chạy đúng thứ tự và sai thứ tự/double-submit', 'Trạng thái booking/xe/ngày giờ thực tế đồng bộ và idempotent'),
      feature('Recent notifications', '/Admin/Booking/GetRecentNotifications', 'Admin/Manager/Staff', 'Admin/BookingController.GetRecentNotifications; _AdminLayout', 'tạo booking mới, mở notification bell', 'Badge/message/time/icon đúng, dữ liệu theo area scope'),
      feature('Settlement review', '/Admin/Settlement', 'Admin/Manager', 'Admin/SettlementController; SettlementReviewService', 'create/approve/reject settlement', 'Rowversion/concurrency đúng, ledger/payout nhất quán'),
      feature('Dispute export evidence', '/Admin/Dispute/Export', 'Admin/Manager', 'Admin/DisputeController.Export; DisputeExportService', 'export evidence package cho booking tranh chấp', 'Package đúng quyền, không lộ PII vượt phạm vi, không mutate booking'),
      feature('Physical contracts admin index', '/Admin/PhysicalContracts', 'Admin/Manager/Staff', 'Admin/PhysicalContractsController.Index', 'lọc/xem contracts theo booking/status/type', 'Danh sách đúng quyền, file/link tải an toàn'),
    ],
  },
  {
    code: 'MD20', title: 'Admin Users Roles', req: 'ADUS01', color: colors.dark,
    features: [
      feature('Admin User index/filter', '/Admin/User', 'Admin', 'Admin/UserController.Index', 'lọc user theo email/name/role/verified/locked/blacklisted', 'Không lộ password hash/PII plaintext, filter an toàn'),
      feature('SetRole governance', '/Admin/User/SetRole', 'Admin', 'Admin/UserController.SetRole; RoleAuthorizeAttribute', 'đổi role user test và thử tự hạ/admin cuối cùng', 'Role cập nhật đúng, guard thao tác nguy hiểm, audit old/new role'),
      feature('Toggle verify/lock/blacklist', '/Admin/User', 'Admin', 'Admin/UserController.ToggleVerify/ToggleLock/ToggleBlacklist; AccountLockMiddleware', 'toggle flags và user refresh/login', 'Middleware chặn/mở đúng, không redirect loop'),
      feature('Partner requests approve/reject', '/Admin/User/PartnerRequests', 'Admin/Partner', 'Admin/UserController.PartnerRequests/ApprovePartner/RejectPartner', 'approve/reject partner pending với lý do', 'Partner approval cập nhật OwnerCars permission đúng'),
      feature('Admin PIN verify', '/Admin/User/VerifyAdminPin', 'Admin', 'Admin/UserController.VerifyAdminPin', 'nhập PIN đúng/sai/nhiều lần và thử bypass action nhạy cảm', 'PIN bảo vệ thao tác, không lưu plaintext và có rate limit'),
      feature('User search XSS/PII masking', '/Admin/User', 'Admin', 'Admin/UserController.Index; Areas/Admin/Views/User/Index.cshtml', 'search payload HTML/SQL-like và kiểm tra columns PII', 'Search không XSS/SQL; PII nhạy cảm được mask hoặc giới hạn'),
      feature('Privileged account SSO/MFA policy', '/Admin/User', 'Admin', 'AuthController; UserController', 'đổi user thành privileged rồi login bằng local/SSO', 'Privileged users phải đáp ứng verified/MFA/approval theo policy'),
    ],
  },
  {
    code: 'MD21', title: 'Admin Fleet Lookups', req: 'ADFL01', color: colors.dark,
    features: [
      feature('Admin Car Manage CRUD/filter/trash', '/Admin/Car/Manage', 'Admin/Manager/Staff', 'Admin/CarController', 'CRUD xe, filter, trash/restore/permanent delete', 'CRUD đúng quyền/area, ảnh validate, audit đầy đủ'),
      feature('Admin Car Create/Edit validation', '/Admin/Car/Create, /Admin/Car/Edit', 'Admin/Manager', 'Admin/CarController; Models/Car.cs', 'nhập VIN/biển số/giá/toạ độ/status/service type hợp lệ/sai', 'Validation chặn unique/format/range, không DbUpdateException lộ ra'),
      feature('Car Approval', '/Admin/CarApproval', 'Admin/Manager/Staff', 'Admin/CarApprovalController.Approve/Reject', 'approve/reject xe pending với reason', 'Approved xe hiện public; rejected không hiện và owner thấy reason'),
      feature('Maintenance logs', '/Admin/Maintenance', 'Admin/Manager/Staff', 'Admin/MaintenanceController; MaintenanceAlertJob', 'tạo/xóa log bảo trì, kiểm tra cảnh báo', 'ODO/chi phí validate, job không duplicate alert'),
      feature('Holiday pricing', '/Admin/HolidayPricing', 'Admin', 'Admin/HolidayPricingController; HolidayPricingService', 'CRUD/toggle ngày lễ, gọi CalculatePrice/GetYearHolidays', 'Multiplier áp dụng quote và API JSON đúng'),
      feature('Lookups CRUD', '/Admin/Lookups', 'Admin', 'Admin/LookupsController', 'thêm/sửa/xóa brand/type/status/location/booking status', 'Lookup đang tham chiếu không bị xóa hỏng dữ liệu'),
      feature('FleetOps readiness', '/Admin/FleetOps/Readiness', 'Admin/Manager', 'Admin/FleetOpsController.Readiness', 'mở readiness report, kiểm tra risk xe thiếu giấy tờ/bảo trì/tracking', 'Report đúng quyền, link về xe/detail hợp lệ'),
    ],
  },
  {
    code: 'MD22', title: 'CRM Audit Snapshot APIKeys', req: 'ADCRM01', color: colors.dark,
    features: [
      feature('Promotions CRUD/trash/restore', '/Admin/Promotions', 'Admin/Customer', 'Admin/PromotionsController; VoucherController.Check', 'tạo/sửa/toggle/delete/restore promotion và áp ở checkout', 'Promotion active/inactive/deleted áp dụng đúng policy'),
      feature('Review moderation', '/Admin/Reviews', 'Admin', 'Admin/ReviewsController.ToggleApproval/Delete', 'approve/unapprove/delete review', 'Review public visibility đúng và nội dung encode'),
      feature('Reports index/export Excel', '/Admin/Reports', 'Admin', 'Admin/ReportsController.Index/ExportExcel', 'lọc report và tải Excel', 'File tải không corrupt, dữ liệu/format VND/ngày đúng, không lộ PII quá mức'),
      feature('AuditLog verify chain', '/Admin/AuditLog', 'Admin', 'Admin/AuditLogController.Index/Details/VerifyChain; AuditTrailService', 'lọc log, mở detail, verify chain', 'Không log secret/raw PII; verify báo pass/fail rõ'),
      feature('Alerts resolve', '/Admin/Alert, /Alert', 'Admin/Customer', 'Admin/AlertController.Resolve; AlertController.Index', 'xem alert và resolve alert', 'Resolve idempotent, empty state rõ'),
      feature('API Keys create/revoke', '/Admin/ApiKeys', 'Admin', 'Admin/ApiKeysController; ApiKeyService', 'create key với scope/expiry, reload, revoke', 'Raw key chỉ hiện một lần, hash/scope/revoke đúng'),
      feature('Snapshot lifecycle', '/Admin/Snapshot', 'Admin', 'Admin/SnapshotController; SnapshotService', 'create/details/download/restore/delete snapshot', 'Snapshot không path traversal, restore có confirm/không chạy nhầm production'),
    ],
  },
  {
    code: 'MD23', title: 'Security RBAC Audit', req: 'SEC01', color: colors.red,
    features: [
      feature('RBAC route matrix', 'public/customer/admin/area routes', 'Guest/Customer/Owner/Manager/Staff/Admin', 'RoleAuthorizeAttribute; Authorize attributes; cypress/e2e/02-rbac', 'quét route bằng các role chính', 'Ma trận quyền đúng, route private không bị lộ'),
      feature('CSRF toàn bộ POST/PUT/DELETE MVC', 'MVC forms/actions', 'All roles', 'Global antiforgery; Controllers POST actions', 'gửi request thiếu/sai token và token user khác', 'Request thiếu/sai CSRF bị chặn, webhook/API có auth riêng'),
      feature('Rate limit và spoof X-Forwarded-For', '/Auth/Login, /api/ai/chat, /Tracking/ReceiveData, /Auth/VerifyPiiPin', 'Guest/All', 'RateLimitingMiddleware; Program ForwardedHeaders', 'spam request và đổi X-Forwarded-For giả', 'Header giả không bypass rate limit nếu proxy không trusted'),
      feature('Security headers/cookie flags', '/, /Auth/Login, /Admin/Dashboard', 'All', 'Program.cs security headers; Cookie auth config', 'kiểm tra CSP/HSTS/X-Frame-Options/cookie HttpOnly/SameSite/Secure', 'Headers/cookie flags phù hợp môi trường'),
      feature('IDOR ownership resources', 'booking/cart/secure file/support/privacy ids', 'Customer/Manager/Staff', 'BookingController; CartController; SecureFileController; PrivacyController', 'Customer B mở/sửa/xóa resource của A; Staff ngoài area thử truy cập', '403/404 đúng, không lộ resource tồn tại'),
      feature('Audit log request success/fail', '/Admin/AuditLog', 'Admin/System', 'AuditLogMiddleware; AuditLogController', 'thực hiện login/booking/admin update/request validation fail', 'Audit ghi request quan trọng và không ghi password/PIN/token/raw PII'),
      feature('Secrets/config exposure', '/ready, rendered HTML, static JS', 'All/System', '.env.example; appsettings*.json; ConfigurationGuard', 'tìm PayOS/FPT/Groq/Gemini secret trong UI/JSON/client JS', 'Không lộ secret thật, ready chỉ báo trạng thái cấu hình'),
    ],
  },
  {
    code: 'MD24', title: 'A11y Responsive NFR', req: 'NFR01', color: colors.red,
    features: [
      feature('Responsive public key pages', '/, /Car, /Car/Details, /Cart, /Booking/Checkout, /Home/Privacy, /Home/Terms', 'Guest/Customer', 'cypress/e2e/10-nonfunctional; site.css', 'mở mobile/tablet/desktop, cuộn và mở modal/calendar/dropdown', 'Không horizontal overflow, không che CTA, text không cắt'),
      feature('Responsive admin key pages', '/Admin/Dashboard, /Admin/Booking, /Admin/Car/Manage, /Admin/User, /Admin/Reports', 'Admin', '_AdminLayout; admin-theme.css', 'mở desktop/mobile, sidebar/offcanvas/table/action menus', 'Sidebar/table/action buttons không vỡ layout'),
      feature('Keyboard-only critical flows', 'login/checkout/eKYC/admin booking', 'All roles', 'docs/accessibility-wcag-aa-checklist.md', 'hoàn thành flow bằng Tab/Shift+Tab/Enter/Escape', 'Không keyboard trap, focus order hợp lý, focus visible'),
      feature('Labels/error association forms', 'Register/Login/eKYC/Checkout/Admin forms', 'All roles', 'Razor views; _ValidationScriptsPartial', 'submit form trống/sai và inspect label/aria-describedby', 'Error gắn đúng field, không chỉ dùng màu'),
      feature('Contrast AA public/admin themes', 'public/admin dark-light pages', 'All roles', 'site.css; admin-theme.css', 'đo contrast text/link/button/badge/table ở hover/focus/disabled', 'Đạt ngưỡng WCAG AA hoặc bug được ghi rõ'),
      feature('Console/network smoke internal links', '/ and /Admin/Dashboard links', 'Guest/Admin', 'cypress/e2e/10-nonfunctional/nonfunctional.cy.js', 'quét link nội bộ bounded và kiểm tra console error', 'Không link 500, không ReferenceError/TypeError/invalid object name'),
      feature('Background jobs behavior', 'Hangfire recurring jobs', 'System/Admin', 'Program.cs Hangfire; BackgroundJobs', 'kiểm tra Testing/Cypress không chạy jobs và production-like đăng ký jobs', 'Jobs idempotent, cron đúng, lỗi được log và không crash app'),
    ],
  },
  {
    code: 'MD25', title: 'Legal Compliance VN', req: 'LAW01', color: colors.red,
    features: [
      feature('Privacy/Terms nội dung thật', '/Home/Privacy, /Home/Terms', 'Guest/Customer', 'Views/Home/Privacy.cshtml; Views/Home/Terms.cshtml; LegalDocumentVersion', 'tìm placeholder, href="#", mojibake, version/date và link trong flows', 'Không placeholder pháp lý, link thật, version phù hợp'),
      feature('Consent version register/eKYC/booking/cart', 'Auth/Ekyc/Booking/Cart flows', 'Customer/System', 'LegalComplianceService; ConsentRecord', 'thực hiện bốn flow consent và kiểm tra records/evidence', 'Mỗi flow lưu đúng document code/version/IP/user-agent khi cần'),
      feature('Wallet stored value disabled', 'Wallet/UI/payment surfaces', 'Admin/Customer', 'docs/legal-compliance-vn-2026-audit.md; Wallet; ConfigurationGuard', 'kiểm tra config/UI nạp-rút ví và action trực tiếp', 'Không public stored-value wallet khi chưa có giấy phép; ledger không claim quá mức'),
      feature('With-driver public booking disabled', 'with-driver routes/options', 'Customer/Admin', 'legal compliance transport; CartController.UpdateCartItemDriver; ServiceTypeConstants', 'tìm lựa chọn thuê có tài xế và thử request trực tiếp', 'Không bán dịch vụ tài xế public nếu chưa đủ điều kiện/flag'),
      feature('Payment/PayOS/refund legal evidence', 'Payment/Checkout/Terms', 'Customer/Admin', 'PayOSPaymentService; PaymentLedgerService; legal audit', 'thanh toán mock, kiểm tra điều khoản cọc/hủy/hoàn tiền, ledger', 'User thấy policy trước thanh toán; ledger/refund audit đầy đủ'),
      feature('eKYC biometric notice/DPA/retention', '/Ekyc, /Home/Privacy, /Admin/Privacy', 'Customer/Admin', 'EkycService; legal audit', 'kiểm tra notice, log metadata, export/delete scope và evidence ngoài repo', 'Có notice/scope; thiếu DPA/vendor evidence được ghi risk'),
      feature('Retention/export/delete legal hold', '/Privacy/RequestDelete, /Admin/Privacy/RetentionPreview', 'Customer/Admin', 'PrivacyRequestService; legal audit', 'delete user có booking/payment/contract/dispute và complete request test', 'Không xóa dữ liệu cần legal hold; manifest/evidence rõ'),
    ],
  },
];

function makeDataSheet(workbook) {
  const ws = workbook.addWorksheet('Dữ liệu kiểm thử', { views: [{ state: 'frozen', ySplit: 1 }] });
  ws.columns = [
    { header: 'Nhóm', key: 'group', width: 22 },
    { header: 'Tên dữ liệu', key: 'name', width: 32 },
    { header: 'Giá trị', key: 'value', width: 44 },
    { header: 'Ghi chú sử dụng', key: 'note', width: 76 },
  ];
  ws.getRow(1).height = 28;
  ws.getRow(1).eachCell((cell) => {
    cell.font = { bold: true, color: { argb: colors.white } };
    cell.fill = fill(colors.header);
    cell.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
    cell.border = borderAll();
  });
  [
    ['Tài khoản', 'Admin', 'admin@admin.com / Cypress@123', 'Toàn quyền Admin, dashboard, users, reports, snapshot, API keys.'],
    ['Tài khoản', 'Manager', 'manager@cypress.local / Cypress@123', 'Kiểm thử quản lý theo khu vực và một số module vận hành.'],
    ['Tài khoản', 'Staff', 'staff@cypress.local / Cypress@123', 'Kiểm thử staff area scope, booking/car/maintenance theo quyền.'],
    ['Tài khoản', 'Viewer', 'viewer@cypress.local / Cypress@123', 'Kiểm thử role chỉ xem nếu hệ thống bật Viewer.'],
    ['Tài khoản', 'Customer verified', 'customer@cypress.local / Cypress@123', 'Đặt xe, checkout, cart, profile, eKYC đã duyệt.'],
    ['Tài khoản', 'Customer unverified', 'customer-unverified@cypress.local / Cypress@123', 'Kiểm thử chặn checkout và eKYC pending.'],
    ['Tài khoản', 'Owner/Partner approved', 'owner@cypress.local / Cypress@123', 'Kiểm thử OwnerCars và đăng xe P2P.'],
    ['Xe', 'First available car', 'CYP-READY hoặc seed.firstAvailableCarId', 'Xe mẫu từ CypressTestDataSeeder.'],
    ['Khu vực', 'Area fixture', 'seed.areaId', 'Khu vực test cho Manager/Staff/Viewer.'],
    ['Booking', 'Booking fixture', 'seed.bookingId', 'Booking mẫu có payment/contract records.'],
    ['Reset data', 'Cypress reset endpoint', 'POST /__test/reset', 'Chỉ dùng môi trường Cypress với X-Cypress-Reset-Secret, không dùng production.'],
  ].forEach((data, idx) => {
    const row = ws.addRow({ group: data[0], name: data[1], value: data[2], note: data[3] });
    row.height = 36;
    row.eachCell((cell, col) => {
      cell.alignment = { vertical: 'top', wrapText: true };
      cell.border = borderAll();
      cell.fill = fill(idx % 2 === 0 ? colors.white : colors.alt);
      if (col <= 2) cell.font = { bold: true };
    });
  });
  ws.autoFilter = 'A1:D1';
}

function makeModuleSheet(workbook, mod, rows) {
  const ws = workbook.addWorksheet(sheetName(mod.code, mod.title), {
    properties: { tabColor: { argb: mod.color } },
    views: [{ state: 'frozen', ySplit: 9 }],
  });
  ws.columns = [
    { width: 13 },
    { width: 42 },
    { width: 68 },
    { width: 72 },
    { width: 16 },
    { width: 16 },
    { width: 48 },
  ];
  ws.mergeCells('A1:G1');
  ws.getCell('A1').value = 'TESTCASE';
  ws.getCell('A1').font = { bold: true, size: 18, color: { argb: colors.white } };
  ws.getCell('A1').fill = fill(mod.color);
  ws.getCell('A1').alignment = { horizontal: 'center', vertical: 'middle' };
  ws.getRow(1).height = 34;

  [
    ['A3', 'Tên hệ thống:', 'B3', 'Hệ thống thuê xe ô tô'],
    ['A4', 'Mã module:', 'B4', `${mod.code} - ${mod.title}`],
    ['A5', 'Yêu cầu kiểm thử:', 'B5', mod.req],
  ].forEach(([labelCell, label, valueCell, value]) => {
    ws.getCell(labelCell).value = label;
    ws.getCell(labelCell).font = { bold: true };
    ws.getCell(valueCell).value = value;
    ws.getCell(valueCell).font = { bold: true, color: { argb: mod.color } };
  });

  ws.getCell('B6').value = 'Đạt';
  ws.getCell('C6').value = 0;
  ws.getCell('E6').value = 'Chưa test';
  ws.getCell('F6').value = rows.length;
  ws.getCell('B7').value = 'Không đạt';
  ws.getCell('C7').value = 0;
  ws.getCell('D7').value = 'Tổng số testcase:';
  ws.getCell('F7').value = rows.length;
  ['B6', 'C6', 'E6', 'F6', 'B7', 'C7', 'D7', 'F7'].forEach((addr) => {
    const cell = ws.getCell(addr);
    cell.border = borderAll();
    cell.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
    if (['B6', 'E6', 'B7', 'D7'].includes(addr)) cell.font = { bold: true };
    if (addr === 'E6') cell.fill = fill(colors.pending);
  });

  ['Mã TC', 'Mô tả kiểm thử', 'Các bước thực hiện', 'Kết quả mong đợi', 'Ngày kiểm thử', 'Kết quả', 'Ghi chú'].forEach((header, idx) => {
    const cell = ws.getCell(9, idx + 1);
    cell.value = header;
    cell.font = { bold: true, color: { argb: colors.white } };
    cell.fill = fill(colors.header);
    cell.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
    cell.border = borderAll();
  });
  ws.getRow(9).height = 30;
  ws.autoFilter = 'A9:G9';

  rows.forEach((tc, idx) => {
    const row = ws.getRow(10 + idx);
    row.getCell(1).value = tc.id;
    row.getCell(2).value = tc.desc;
    row.getCell(3).value = tc.steps;
    row.getCell(4).value = tc.expected;
    row.getCell(5).value = '';
    row.getCell(6).value = '';
    row.getCell(7).value = tc.notes;
    const maxLen = Math.max(tc.steps.length, tc.expected.length, tc.desc.length);
    row.height = Math.max(56, Math.min(158, 34 + Math.ceil(maxLen / 90) * 12));
    row.eachCell({ includeEmpty: true }, (cell, col) => {
      cell.border = borderAll();
      cell.alignment = { vertical: 'top', wrapText: true };
      cell.fill = fill(idx % 2 === 0 ? colors.white : colors.alt);
      if (col === 1) {
        cell.font = { bold: true, color: { argb: mod.color } };
        cell.alignment = { horizontal: 'center', vertical: 'top', wrapText: true };
      }
      if (col === 5 || col === 6) {
        cell.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
      }
    });
  });
}

function makeReportSheet(workbook, moduleStats) {
  const ws = workbook.addWorksheet('Báo cáo kiểm thử', {
    properties: { tabColor: { argb: colors.green } },
    views: [{ state: 'frozen', ySplit: 6 }],
  });
  ws.columns = [
    { width: 8 },
    { width: 42 },
    { width: 12 },
    { width: 12 },
    { width: 14 },
    { width: 18 },
    { width: 44 },
  ];
  ws.mergeCells('A1:G1');
  ws.getCell('A1').value = 'BÁO CÁO KIỂM THỬ';
  ws.getCell('A1').font = { bold: true, size: 18, color: { argb: colors.white } };
  ws.getCell('A1').fill = fill(colors.green);
  ws.getCell('A1').alignment = { horizontal: 'center', vertical: 'middle' };
  ws.getRow(1).height = 34;
  ws.getCell('B3').value = 'Ghi chú:';
  ws.getCell('C3').value = 'Các testcase mới để trống Ngày kiểm thử và Kết quả; toàn bộ được tính là Chưa test.';
  ws.mergeCells('C3:G3');
  ws.getCell('B4').value = 'Ngày:';
  ws.getCell('D4').value = '20/05/2026';

  ['STT', 'Mã module', 'Đạt', 'Không đạt', 'Chưa test', 'Tổng số testcase', 'Ghi chú'].forEach((header, idx) => {
    const cell = ws.getCell(6, idx + 1);
    cell.value = header;
    cell.font = { bold: true, color: { argb: colors.white } };
    cell.fill = fill(colors.header);
    cell.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
    cell.border = borderAll();
  });

  let total = 0;
  moduleStats.forEach((stat, idx) => {
    total += stat.count;
    const row = ws.getRow(7 + idx);
    row.getCell(1).value = idx + 1;
    row.getCell(2).value = stat.name;
    row.getCell(3).value = 0;
    row.getCell(4).value = 0;
    row.getCell(5).value = stat.count;
    row.getCell(6).value = stat.count;
    row.getCell(7).value = stat.req;
    row.height = 28;
    row.eachCell({ includeEmpty: true }, (cell, col) => {
      cell.border = borderAll();
      cell.alignment = { horizontal: col >= 3 && col <= 6 ? 'center' : 'left', vertical: 'middle', wrapText: true };
      cell.fill = fill(idx % 2 === 0 ? colors.white : colors.alt);
      if (col === 2) cell.font = { bold: true };
    });
  });
  const totalRow = ws.getRow(7 + moduleStats.length);
  totalRow.getCell(2).value = 'TỔNG CỘNG';
  totalRow.getCell(3).value = 0;
  totalRow.getCell(4).value = 0;
  totalRow.getCell(5).value = total;
  totalRow.getCell(6).value = total;
  totalRow.getCell(7).value = 'Generated detailed full-system test suite';
  totalRow.height = 30;
  totalRow.eachCell({ includeEmpty: true }, (cell, col) => {
    cell.border = borderAll();
    cell.font = { bold: true, color: { argb: colors.white } };
    cell.fill = fill(colors.dark);
    cell.alignment = { horizontal: col >= 3 && col <= 6 ? 'center' : 'left', vertical: 'middle', wrapText: true };
  });
  ws.autoFilter = 'A6:G6';
}

async function main() {
  if (!fs.existsSync(templatePath)) {
    throw new Error(`Không tìm thấy file template: ${templatePath}`);
  }

  const workbook = new ExcelJS.Workbook();
  await workbook.xlsx.readFile(templatePath);
  workbook.creator = 'Codex - CarRentalSystem QA';
  workbook.lastModifiedBy = 'Codex - CarRentalSystem QA';
  workbook.modified = new Date('2026-05-20T00:00:00+07:00');
  workbook.subject = 'Detailed full-system test cases for CarRentalSystem';
  workbook.title = 'TestCase CarRentalSystem Full Detailed 2026-05-20';

  for (const ws of [...workbook.worksheets]) {
    if (ws.name !== 'Trang bìa') workbook.removeWorksheet(ws.id);
  }

  makeDataSheet(workbook);

  let tcCounter = 1;
  const allRows = [];
  const moduleStats = [];
  for (const mod of modules) {
    const testRows = mod.features.flatMap((item) => expandFeature(mod.title, item)).map((tc) => {
      const id = `TC${String(tcCounter++).padStart(3, '0')}`;
      const row = { id, ...tc, module: `${mod.code} - ${mod.title}` };
      allRows.push(row);
      return row;
    });
    makeModuleSheet(workbook, mod, testRows);
    moduleStats.push({ name: `${mod.code} - ${mod.title}`, req: mod.req, count: testRows.length });
  }
  makeReportSheet(workbook, moduleStats);

  const cover = workbook.getWorksheet('Trang bìa');
  if (cover) {
    cover.getCell('B2').value = '3.0';
    cover.getCell('B3').value = '20/05/2026';
    cover.getCell('B5').value = 'Hệ thống thuê xe ô tô (Car Rental System)';
    cover.getCell('A11').value = '20-05-2026';
    cover.getCell('B11').value = '3.0';
    cover.getCell('C11').value = 'Toàn bộ workbook';
    cover.getCell('D11').value = `Sinh bộ testcase chi tiết toàn hệ thống, bao phủ ${modules.length} module và ${allRows.length} testcase; ngày/kết quả để trống.`;
    cover.getCell('E11').value = 'Codex QA';
    cover.getCell('G11').value = 'CRS-2026-FULL-DETAILED';
    cover.getCell('D11').alignment = { wrapText: true, vertical: 'top' };
  }

  const ids = allRows.map((row) => row.id);
  if (ids.length !== new Set(ids).size) throw new Error('Duplicate TC IDs detected');
  ids.forEach((id, idx) => {
    const expected = `TC${String(idx + 1).padStart(3, '0')}`;
    if (id !== expected) throw new Error(`Non-sequential TC ID: ${id} != ${expected}`);
  });

  for (const stat of moduleStats) {
    const ws = workbook.getWorksheet(stat.name.slice(0, 31));
    if (!ws) throw new Error(`Missing module sheet ${stat.name}`);
    for (let row = 10; row < 10 + stat.count; row += 1) {
      for (const col of [1, 2, 3, 4]) {
        const value = ws.getCell(row, col).value;
        if (value == null || String(value).trim() === '') throw new Error(`Empty required cell ${ws.name}!${row},${col}`);
      }
      for (const col of [5, 6]) {
        const value = ws.getCell(row, col).value;
        if (value != null && String(value).trim() !== '') throw new Error(`Date/result must be blank ${ws.name}!${row},${col}`);
      }
    }
  }

  const outputDir = path.dirname(outputPath);
  if (outputDir && outputDir !== path.parse(outputDir).root) {
    fs.mkdirSync(outputDir, { recursive: true });
  }
  await workbook.xlsx.writeFile(outputPath);
  console.log(`Created: ${outputPath}`);
  console.log(`Modules: ${modules.length}`);
  console.log(`Test cases: ${allRows.length}`);
}

main().catch((error) => {
  console.error(error.message || error);
  process.exitCode = 1;
});
