using CarRentalSystem.Models;

namespace CarRentalSystem.Helpers
{
    public static class UiLabels
    {
        public static string BookingStatus(string? value) => value switch
        {
            BookingStatusConstants.Draft => "Chờ thanh toán",
            BookingStatusConstants.Unpaid => "Chưa thanh toán",
            BookingStatusConstants.Pending => "Chờ xác nhận",
            BookingStatusConstants.Confirmed => "Đã xác nhận",
            BookingStatusConstants.Active => "Đang thuê",
            BookingStatusConstants.Completed => "Hoàn tất",
            BookingStatusConstants.Cancelled => "Đã hủy",
            BookingStatusConstants.Rejected => "Từ chối",
            BookingStatusConstants.Expired => "Hết hạn",
            BookingStatusConstants.NeedReview => "Cần rà soát",
            _ => value ?? string.Empty
        };

        public static string CarStatus(string? value) => value switch
        {
            CarStatusConstants.Available => "Sẵn sàng",
            CarStatusConstants.Rented => "Đang thuê",
            CarStatusConstants.Maintenance => "Bảo trì",
            CarStatusConstants.Pending => "Chờ duyệt",
            "Active" => "Đang hoạt động",
            "Inactive" => "Tạm ngưng",
            _ => value ?? string.Empty
        };

        public static string DepositStatus(string? value) => value switch
        {
            DepositStatusConstants.Pending => "Đang chờ",
            DepositStatusConstants.Unpaid => "Chưa thanh toán",
            DepositStatusConstants.Holding => "Đang giữ cọc",
            DepositStatusConstants.PendingRefund => "Chờ hoàn cọc",
            DepositStatusConstants.UnderReview => "Đang rà soát",
            DepositStatusConstants.Refunded => "Đã hoàn",
            DepositStatusConstants.Deducted => "Đã khấu trừ",
            DepositStatusConstants.Failed => "Thất bại",
            _ => value ?? string.Empty
        };

        public static string PaymentStatus(string? value) => value switch
        {
            PaymentStatusConstants.Pending => "Đang chờ",
            PaymentStatusConstants.Paid => "Đã thanh toán",
            PaymentStatusConstants.Failed => "Thất bại",
            PaymentStatusConstants.Cancelled => "Đã hủy",
            PaymentStatusConstants.RefundPending => "Chờ hoàn tiền",
            PaymentStatusConstants.Refunded => "Đã hoàn tiền",
            PaymentStatusConstants.Processing => "Đang xử lý",
            PaymentStatusConstants.PendingManual => "Chờ xác nhận thủ công",
            _ => value ?? string.Empty
        };

        public static string SettlementReviewType(string? value) => value switch
        {
            SettlementReviewTypeConstants.Late => "Phí trễ",
            SettlementReviewTypeConstants.Fuel => "Nhiên liệu",
            SettlementReviewTypeConstants.Cleaning => "Vệ sinh",
            SettlementReviewTypeConstants.Damage => "Hư hỏng",
            SettlementReviewTypeConstants.LostItem => "Thiếu phụ kiện",
            SettlementReviewTypeConstants.Toll => "Phí cầu đường",
            SettlementReviewTypeConstants.Violation => "Vi phạm",
            SettlementReviewTypeConstants.Other => "Khác",
            _ => value ?? string.Empty
        };

        public static string SettlementReviewStatus(string? value) => value switch
        {
            SettlementReviewStatusConstants.PendingReview => "Chờ duyệt",
            SettlementReviewStatusConstants.Approved => "Đã duyệt",
            SettlementReviewStatusConstants.Rejected => "Đã từ chối",
            _ => value ?? string.Empty
        };

        public static string LostFoundStatus(string? value) => value switch
        {
            LostFoundStatusConstants.Found => "Đã tìm thấy",
            LostFoundStatusConstants.CustomerNotified => "Đã thông báo khách",
            LostFoundStatusConstants.Returned => "Đã trả khách",
            LostFoundStatusConstants.Disposed => "Đã xử lý bỏ",
            _ => value ?? string.Empty
        };

        public static string PrivacyRequestType(string? value) => value switch
        {
            PrivacyRequestTypeConstants.Export => "Xuất dữ liệu",
            PrivacyRequestTypeConstants.Delete => "Xóa dữ liệu",
            PrivacyRequestTypeConstants.RetentionReview => "Rà soát lưu trữ",
            _ => value ?? string.Empty
        };

        public static string PrivacyRequestStatus(string? value) => value switch
        {
            PrivacyRequestStatusConstants.PendingReview => "Chờ duyệt",
            PrivacyRequestStatusConstants.Approved => "Đã duyệt",
            PrivacyRequestStatusConstants.Rejected => "Đã từ chối",
            PrivacyRequestStatusConstants.Completed => "Đã hoàn tất",
            _ => value ?? string.Empty
        };

        public static string BlogStatus(string? value) => value switch
        {
            BlogStatusConstants.Draft => "Bản nháp",
            BlogStatusConstants.Published => "Đã xuất bản",
            _ => value ?? string.Empty
        };

        public static string SnapshotStatus(string? value) => value switch
        {
            "Ready" => "Sẵn sàng",
            "Restored" => "Đã phục hồi",
            "Failed" => "Thất bại",
            "Processing" => "Đang xử lý",
            _ => value ?? string.Empty
        };

        public static string CameraStatus(string? value) => value switch
        {
            "Active" => "Đang hoạt động",
            "Inactive" => "Tạm ngưng",
            _ => value ?? string.Empty
        };

        public static string EnterpriseCapabilityStatus(string? value) => value switch
        {
            "Live" => "Đang chạy",
            "Partial" => "Một phần",
            "Planned" => "Đã lên kế hoạch",
            "Blocked" => "Đang vướng",
            _ => value ?? string.Empty
        };

        public static string GenericStatus(string? value) => value switch
        {
            "Active" => "Đang hoạt động",
            "Inactive" => "Tạm ngưng",
            "Pending" => "Đang chờ",
            "Ready" => "Sẵn sàng",
            "Restored" => "Đã phục hồi",
            "Resolved" => "Đã xử lý",
            "Open" => "Đang mở",
            "Closed" => "Đã đóng",
            "Published" => "Đã xuất bản",
            "Draft" => "Bản nháp",
            _ => value ?? string.Empty
        };

        public static string SupportTicketStatus(string? value) => value switch
        {
            SupportTicketStatusConstants.Open => "Đang mở",
            SupportTicketStatusConstants.InProgress => "Đang xử lý",
            SupportTicketStatusConstants.WaitingForCustomer => "Chờ khách hàng",
            SupportTicketStatusConstants.Resolved => "Đã giải quyết",
            SupportTicketStatusConstants.Closed => "Đã đóng",
            _ => value ?? string.Empty
        };
    }
}
