using CarRentalSystem.Models;

namespace CarRentalSystem.ViewModels;

public class BookingFlowProgressViewModel
{
    public IReadOnlyList<BookingFlowStepViewModel> Steps { get; set; } = Array.Empty<BookingFlowStepViewModel>();
    public string? Note { get; set; }

    public static BookingFlowProgressViewModel ForCheckout(Booking booking)
    {
        var mode = CarContractSigningModes.Normalize(booking.Car?.ContractSigningMode);
        return Build(1, mode, "Bạn đang kiểm tra thông tin và thanh toán giữ chỗ.");
    }

    public static BookingFlowProgressViewModel ForBooking(Booking booking)
    {
        var mode = CarContractSigningModes.Normalize(booking.Car?.ContractSigningMode);
        var contract = booking.DigitalContract ?? booking.BookingTransaction?.DigitalContract;
        var checkIn = booking.HandoverRecords?.Any(h => h.Type == "CheckIn") == true;
        var checkOut = booking.HandoverRecords?.Any(h => h.Type == "CheckOut") == true;
        var status = booking.BookingStatus?.Name;

        var activeIndex = status switch
        {
            BookingStatusConstants.Completed => 6,
            BookingStatusConstants.Active when checkOut => 5,
            BookingStatusConstants.Active => 4,
            BookingStatusConstants.Confirmed when checkIn => 3,
            BookingStatusConstants.Confirmed when mode == CarContractSigningModes.Digital && contract?.IsSignedByCustomer == true => 3,
            BookingStatusConstants.Confirmed when mode == CarContractSigningModes.PhysicalSelfManaged => 2,
            BookingStatusConstants.Confirmed => 2,
            BookingStatusConstants.Pending => 1,
            BookingStatusConstants.Draft or BookingStatusConstants.Unpaid => 1,
            _ => 0
        };

        var note = mode == CarContractSigningModes.Digital
            ? "Xe này dùng hợp đồng số, cần ký trước biên bản nhận xe."
            : "Xe này ký hợp đồng giấy với chủ xe; app không lưu file hợp đồng vật lý.";

        return Build(activeIndex, mode, note);
    }

    public static BookingFlowProgressViewModel ForTransaction(BookingTransaction transaction)
    {
        var firstBooking = transaction.Bookings.FirstOrDefault();
        if (firstBooking == null)
            return Build(1, CarContractSigningModes.PhysicalSelfManaged, "Thanh toán giỏ hàng đã được ghi nhận.");

        var mode = CarContractSigningModes.Normalize(firstBooking.Car?.ContractSigningMode);
        var contract = transaction.DigitalContract;
        var activeIndex = mode == CarContractSigningModes.Digital && contract?.IsSignedByCustomer != true ? 2 : 3;
        return Build(activeIndex, mode, "Các xe trong giỏ hàng dùng cùng một kiểu hợp đồng.");
    }

    private static BookingFlowProgressViewModel Build(int activeIndex, string mode, string note)
    {
        var contractLabel = mode == CarContractSigningModes.Digital ? "Ký hợp đồng số" : "Ký giấy với chủ xe";
        var titles = new[]
        {
            "Chọn xe",
            "Thanh toán giữ chỗ",
            contractLabel,
            "Biên bản nhận xe",
            "Đang thuê",
            "Biên bản trả xe",
            "Hoàn tất"
        };

        return new BookingFlowProgressViewModel
        {
            Note = note,
            Steps = titles.Select((title, index) => new BookingFlowStepViewModel
            {
                Index = index,
                Title = title,
                IsActive = index == activeIndex,
                IsCompleted = index < activeIndex
            }).ToList()
        };
    }
}

public class BookingFlowStepViewModel
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsCompleted { get; set; }
}
