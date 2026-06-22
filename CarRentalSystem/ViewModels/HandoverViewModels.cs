using CarRentalSystem.Models;
using Microsoft.AspNetCore.Http;

namespace CarRentalSystem.ViewModels
{
    public class HandoverCaptureViewModel
    {
        public int BookingID { get; set; }
        public string Type { get; set; } = "CheckIn";
        public Booking? Booking { get; set; }
        public int Odo { get; set; }
        public string FuelLevel { get; set; } = "F";
        public double? GeoLatitude { get; set; }
        public double? GeoLongitude { get; set; }
        public string? SignatureBase64 { get; set; }

        public IFormFile? ImgFront { get; set; }
        public IFormFile? ImgBack { get; set; }
        public IFormFile? ImgLeft { get; set; }
        public IFormFile? ImgRight { get; set; }
        public IFormFile? ImgInside { get; set; }

        public List<HandoverInventoryInput> InventoryItems { get; set; } = new();
    }

    public class HandoverInventoryInput
    {
        public int? CarInventoryItemID { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int ExpectedQuantity { get; set; }
        public int ActualQuantity { get; set; }
        public string ConditionStatus { get; set; } = HandoverInventoryConditionStatusConstants.Present;
        public decimal UnitReplacementCost { get; set; }
        public string? Notes { get; set; }
    }
}
