using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CarRentalSystem.Models;

namespace CarRentalSystem.Services
{
    public interface IEvidenceHashService
    {
        string ComputeHandoverEvidenceHash(HandoverRecord record);
        string ComputeContractHash(object contractSnapshot);
        string ComputeTextHash(string? value);
    }

    public class EvidenceHashService : IEvidenceHashService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public string ComputeHandoverEvidenceHash(HandoverRecord record)
        {
            var snapshot = new
            {
                record.BookingID,
                record.Type,
                record.RecordDate,
                record.Odo,
                record.FuelLevel,
                record.FrontImgUrl,
                record.BackImgUrl,
                record.LeftImgUrl,
                record.RightImgUrl,
                record.InsideImgUrl,
                record.SignatureUrl,
                record.CustomerSignatureHash,
                record.GeoLatitude,
                record.GeoLongitude,
                record.RecordedByUserId,
                InventoryItems = record.InventoryItems
                    .OrderBy(i => i.ItemName)
                    .Select(i => new
                    {
                        i.CarInventoryItemID,
                        i.ItemName,
                        i.ExpectedQuantity,
                        i.ActualQuantity,
                        i.MissingQuantity,
                        i.ConditionStatus,
                        i.UnitReplacementCost,
                        i.MissingCharge,
                        i.Notes
                    }),
                LostFoundItems = record.LostFoundItems
                    .OrderBy(i => i.Description)
                    .Select(i => new
                    {
                        i.Description,
                        i.LocationDescription,
                        i.PhotoUrl,
                        i.Status,
                        i.ReportedByUserID,
                        i.FoundAt
                    })
            };

            return ComputeContractHash(snapshot);
        }

        public string ComputeContractHash(object contractSnapshot)
        {
            return ComputeTextHash(JsonSerializer.Serialize(contractSnapshot, JsonOptions));
        }

        public string ComputeTextHash(string? value)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty))).ToLowerInvariant();
        }
    }
}
