using CarRentalSystem.Areas.Admin.Models;
using CarRentalSystem.Data;
using CarRentalSystem.Helpers;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface IOperationsCopilotService
    {
        Task<OpsSnapshot> GetOpsSnapshotAsync(CancellationToken cancellationToken = default);
        Task<OpsMoneyExceptionReport> ExplainMoneyExceptionsAsync(CancellationToken cancellationToken = default);
        Task<OpsVehicleTimeline> InspectVehicleTimelineAsync(int? carId = null, string? licensePlate = null, CancellationToken cancellationToken = default);
        Task<OpsCameraRiskSummary> SummarizeCameraRiskAsync(CancellationToken cancellationToken = default);
        Task<OpsActionPlan> RecommendNextActionsAsync(CancellationToken cancellationToken = default);
    }

    public sealed record OpsSnapshot(
        DateTime GeneratedAtUtc,
        int RiskScore,
        string RiskLevel,
        IReadOnlyList<OpsRiskSignal> TopSignals,
        EnterpriseFinanceSnapshot Finance,
        EnterpriseFleetSnapshot Fleet,
        IReadOnlyList<EnterpriseQueueItem> Queues);

    public sealed record OpsRiskSignal(
        string Area,
        string Severity,
        int Count,
        string Message,
        string Url);

    public sealed record OpsMoneyExceptionReport(
        DateTime GeneratedAtUtc,
        int RiskScore,
        IReadOnlyList<OpsPayOSWithoutLedgerItem> PayOSPaidWithoutLedger,
        IReadOnlyList<OpsLedgerWithoutPayOSItem> LedgerWithoutPayOS,
        IReadOnlyList<OpsRefundQueueItem> PendingRefunds,
        IReadOnlyList<OpsPayoutQueueItem> PendingPayouts,
        IReadOnlyList<OpsRecommendedAction> RecommendedActions);

    public sealed record OpsPayOSWithoutLedgerItem(
        int PayOSTransactionID,
        long OrderCode,
        decimal Amount,
        string Status,
        DateTime CreatedAt,
        DateTime? PaidAt);

    public sealed record OpsLedgerWithoutPayOSItem(
        int PaymentLedgerEntryID,
        long? OrderCode,
        decimal Amount,
        string EntryType,
        string Status,
        DateTime CreatedAt);

    public sealed record OpsRefundQueueItem(
        int RefundRequestID,
        int BookingID,
        decimal Amount,
        string Status,
        string Reason,
        DateTime CreatedAt);

    public sealed record OpsPayoutQueueItem(
        int RefundPayoutID,
        int RefundRequestID,
        decimal Amount,
        string ApprovalState,
        string TransactionState,
        int AttemptCount,
        string? LastError,
        DateTime CreatedAt);

    public sealed record OpsVehicleTimeline(
        DateTime GeneratedAtUtc,
        int? CarID,
        string? Vehicle,
        string? LicensePlate,
        int RiskScore,
        string RiskLevel,
        IReadOnlyList<OpsRiskSignal> RiskSignals,
        IReadOnlyList<OpsTimelineEvent> Timeline,
        IReadOnlyList<OpsRecommendedAction> RecommendedActions);

    public sealed record OpsTimelineEvent(
        DateTime Timestamp,
        string Type,
        string Severity,
        string Title,
        string Detail,
        string? Url = null);

    public sealed record OpsCameraRiskSummary(
        DateTime GeneratedAtUtc,
        int RiskScore,
        int TotalEvents24h,
        int MatchedCount,
        int UnknownPlateCount,
        int LowConfidenceCount,
        int AreaMismatchCount,
        IReadOnlyList<OpsCameraRiskEvent> RecentRiskEvents,
        IReadOnlyList<OpsRecommendedAction> RecommendedActions);

    public sealed record OpsCameraRiskEvent(
        int CameraGateEventID,
        DateTime CapturedAt,
        string CameraCode,
        string LicensePlate,
        string MatchStatus,
        decimal Confidence,
        string? Reason,
        int? CarID);

    public sealed record OpsActionPlan(
        DateTime GeneratedAtUtc,
        int RiskScore,
        string RiskLevel,
        IReadOnlyList<OpsRecommendedAction> Actions);

    public sealed record OpsRecommendedAction(
        string Priority,
        string Owner,
        string Action,
        string Reason,
        string Url);

    public class OperationsCopilotService : IOperationsCopilotService
    {
        private static readonly string[] TerminalSupportStatuses =
        {
            SupportTicketStatusConstants.Resolved,
            SupportTicketStatusConstants.Closed
        };

        private static readonly string[] PendingRefundStatuses =
        {
            RefundRequestStatusConstants.PendingReview,
            RefundRequestStatusConstants.Approved,
            RefundRequestStatusConstants.Processing,
            RefundRequestStatusConstants.PendingManual
        };

        private readonly ApplicationDbContext _context;
        private readonly IEnterpriseOperationsService _enterpriseOperations;

        public OperationsCopilotService(ApplicationDbContext context, IEnterpriseOperationsService enterpriseOperations)
        {
            _context = context;
            _enterpriseOperations = enterpriseOperations;
        }

        public async Task<OpsSnapshot> GetOpsSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var commandCenter = await _enterpriseOperations.GetCommandCenterAsync(cancellationToken);
            var signals = commandCenter.Queues
                .Where(q => q.Count > 0)
                .OrderBy(q => SeverityRank(q.Severity))
                .ThenByDescending(q => q.Count)
                .Take(8)
                .Select(q => new OpsRiskSignal(q.Owner, q.Severity, q.Count, $"{q.Title}: {q.Description}", q.Url))
                .ToList();

            var score = CalculateQueueRiskScore(commandCenter.Queues);
            return new OpsSnapshot(
                commandCenter.GeneratedAtUtc,
                score,
                RiskLevel(score),
                signals,
                commandCenter.Finance,
                commandCenter.Fleet,
                commandCenter.Queues);
        }

        public async Task<OpsMoneyExceptionReport> ExplainMoneyExceptionsAsync(CancellationToken cancellationToken = default)
        {
            var payOSWithoutLedger = await _context.PayOSTransactions
                .AsNoTracking()
                .Where(t =>
                    t.Status == PaymentStatusConstants.Paid &&
                    !_context.PaymentLedgerEntries.Any(l =>
                        l.OrderCode == t.OrderCode &&
                        l.Status == PaymentStatusConstants.Paid))
                .OrderByDescending(t => t.PaidAt ?? t.CreatedAt)
                .Take(20)
                .Select(t => new OpsPayOSWithoutLedgerItem(
                    t.PayOSTransactionID,
                    t.OrderCode,
                    t.Amount,
                    t.Status,
                    t.CreatedAt,
                    t.PaidAt))
                .ToListAsync(cancellationToken);

            var ledgerWithoutPayOS = await _context.PaymentLedgerEntries
                .AsNoTracking()
                .Where(l =>
                    l.Provider == "PayOS" &&
                    l.Status == PaymentStatusConstants.Paid &&
                    l.OrderCode != null &&
                    !_context.PayOSTransactions.Any(t =>
                        t.OrderCode == l.OrderCode &&
                        t.Status == PaymentStatusConstants.Paid))
                .OrderByDescending(l => l.CreatedAt)
                .Take(20)
                .Select(l => new OpsLedgerWithoutPayOSItem(
                    l.PaymentLedgerEntryID,
                    l.OrderCode,
                    l.Amount,
                    l.EntryType,
                    l.Status,
                    l.CreatedAt))
                .ToListAsync(cancellationToken);

            var pendingRefunds = await _context.RefundRequests
                .AsNoTracking()
                .Where(r => PendingRefundStatuses.Contains(r.Status))
                .OrderBy(r => r.CreatedAt)
                .Take(20)
                .Select(r => new OpsRefundQueueItem(
                    r.RefundRequestID,
                    r.BookingID,
                    r.Amount,
                    r.Status,
                    r.Reason,
                    r.CreatedAt))
                .ToListAsync(cancellationToken);

            var pendingPayouts = await _context.RefundPayouts
                .AsNoTracking()
                .Where(p =>
                    p.ApprovalState == PaymentStatusConstants.PendingManual ||
                    p.TransactionState == PaymentStatusConstants.PendingManual ||
                    p.TransactionState == PaymentStatusConstants.Processing ||
                    p.TransactionState == PaymentStatusConstants.Pending)
                .OrderBy(p => p.CreatedAt)
                .Take(20)
                .Select(p => new OpsPayoutQueueItem(
                    p.RefundPayoutID,
                    p.RefundRequestID,
                    p.Amount,
                    p.ApprovalState,
                    p.TransactionState,
                    p.AttemptCount,
                    p.LastError,
                    p.CreatedAt))
                .ToListAsync(cancellationToken);

            var score = Math.Clamp(
                payOSWithoutLedger.Count * 25 +
                ledgerWithoutPayOS.Count * 25 +
                pendingRefunds.Count * 8 +
                pendingPayouts.Count * 10,
                0,
                100);

            var actions = BuildMoneyActions(payOSWithoutLedger.Count, ledgerWithoutPayOS.Count, pendingRefunds.Count, pendingPayouts.Count);
            return new OpsMoneyExceptionReport(DateTime.UtcNow, score, payOSWithoutLedger, ledgerWithoutPayOS, pendingRefunds, pendingPayouts, actions);
        }

        public async Task<OpsVehicleTimeline> InspectVehicleTimelineAsync(int? carId = null, string? licensePlate = null, CancellationToken cancellationToken = default)
        {
            var car = await ResolveCarAsync(carId, licensePlate, cancellationToken);
            if (car == null)
            {
                return new OpsVehicleTimeline(
                    DateTime.UtcNow,
                    null,
                    null,
                    null,
                    0,
                    RiskLevel(0),
                    Array.Empty<OpsRiskSignal>(),
                    Array.Empty<OpsTimelineEvent>(),
                    new[]
                    {
                        new OpsRecommendedAction("P2", "Fleet", "Chọn xe hoặc nhập biển số hợp lệ để xem timeline.", "Không tìm thấy xe phù hợp.", "/Admin/Car/Manage")
                    });
            }

            var now = DateTime.UtcNow;
            var from = now.AddDays(-30);
            var timeline = new List<OpsTimelineEvent>();
            var riskSignals = new List<OpsRiskSignal>();
            var score = 0;

            var bookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.BookingStatus)
                .Where(b => !b.IsDeleted && b.CarID == car.CarID && b.EndDate >= from && b.StartDate <= now.AddDays(14))
                .OrderByDescending(b => b.StartDate)
                .Take(10)
                .Select(b => new
                {
                    b.BookingID,
                    b.StartDate,
                    b.EndDate,
                    b.ActualReturnDate,
                    b.DepositStatus,
                    Status = b.BookingStatus != null ? b.BookingStatus.Name : null,
                    b.TotalPrice
                })
                .ToListAsync(cancellationToken);

            foreach (var booking in bookings)
            {
                var severity = booking.Status == BookingStatusConstants.Active && booking.EndDate < now && booking.ActualReturnDate == null
                    ? EnterpriseSeverity.Critical
                    : EnterpriseSeverity.Info;
                if (severity == EnterpriseSeverity.Critical)
                {
                    score += 20;
                    riskSignals.Add(new OpsRiskSignal("Fleet", severity, 1, $"Booking #{booking.BookingID} quá hạn trả xe.", "/Admin/Booking"));
                }

                timeline.Add(new OpsTimelineEvent(
                    booking.StartDate,
                    "Booking",
                    severity,
                    $"Booking #{booking.BookingID} - {booking.Status ?? "Unknown"}",
                    $"Start {booking.StartDate:dd/MM HH:mm}, end {booking.EndDate:dd/MM HH:mm}, deposit {booking.DepositStatus}, total {booking.TotalPrice:N0} VND.",
                    "/Admin/Booking"));
            }

            var latestTracking = await _context.VehicleTrackings
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.CarID == car.CarID)
                .OrderByDescending(t => t.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestTracking == null)
            {
                score += 25;
                riskSignals.Add(new OpsRiskSignal("Fleet", EnterpriseSeverity.Critical, 1, "Xe chưa có dữ liệu GPS.", "/Tracking/Map"));
            }
            else
            {
                var age = now - latestTracking.Timestamp;
                var severity = age.TotalHours > 24 ? EnterpriseSeverity.Critical :
                    age.TotalMinutes > 5 ? EnterpriseSeverity.Warning :
                    EnterpriseSeverity.Success;
                if (severity == EnterpriseSeverity.Critical) score += 25;
                else if (severity == EnterpriseSeverity.Warning) score += 10;

                if (severity != EnterpriseSeverity.Success)
                    riskSignals.Add(new OpsRiskSignal("Fleet", severity, 1, $"GPS stale {age.TotalMinutes:N0} phút.", "/Tracking/Map"));

                timeline.Add(new OpsTimelineEvent(
                    latestTracking.Timestamp,
                    "GPS",
                    severity,
                    "GPS mới nhất",
                    $"Nguồn {latestTracking.Source}, tốc độ {latestTracking.Speed:N1} km/h, pin {latestTracking.BatteryLevel?.ToString("N0") ?? "-"}%.",
                    "/Tracking/Map"));
            }

            var recentTrackings = await _context.VehicleTrackings
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.CarID == car.CarID && t.Timestamp >= from)
                .OrderByDescending(t => t.Timestamp)
                .Skip(latestTracking == null ? 0 : 1)
                .Take(5)
                .Select(t => new OpsTimelineEvent(
                    t.Timestamp,
                    "GPS",
                    EnterpriseSeverity.Info,
                    "Điểm GPS",
                    $"Nguồn {t.Source}, tốc độ {t.Speed:N1} km/h.",
                    "/Tracking/Map"))
                .ToListAsync(cancellationToken);
            timeline.AddRange(recentTrackings);

            var alerts = await _context.Alerts
                .AsNoTracking()
                .Where(a => !a.IsDeleted && a.CarID == car.CarID && a.AlertTime >= from)
                .OrderByDescending(a => a.AlertTime)
                .Take(10)
                .Select(a => new { a.AlertID, a.AlertTime, a.AlertType, a.Status, a.Description })
                .ToListAsync(cancellationToken);

            var openAlerts = alerts.Count(a => !IsTerminalAlertStatus(a.Status));
            if (openAlerts > 0)
            {
                score += Math.Min(30, openAlerts * 10);
                riskSignals.Add(new OpsRiskSignal("Fleet", EnterpriseSeverity.Warning, openAlerts, "Xe có cảnh báo chưa xử lý.", "/Admin/Alert"));
            }

            timeline.AddRange(alerts.Select(a => new OpsTimelineEvent(
                a.AlertTime,
                "Alert",
                IsTerminalAlertStatus(a.Status) ? EnterpriseSeverity.Info : EnterpriseSeverity.Warning,
                $"{a.AlertType} - {a.Status}",
                a.Description ?? "Không có mô tả.",
                "/Admin/Alert")));

            var cameraEvents = await _context.CameraGateEvents
                .AsNoTracking()
                .Include(e => e.CameraDevice)
                .Where(e => !e.IsDeleted && e.CarID == car.CarID && e.CapturedAt >= from)
                .OrderByDescending(e => e.CapturedAt)
                .Take(10)
                .Select(e => new
                {
                    e.CameraGateEventID,
                    e.CapturedAt,
                    CameraCode = e.CameraDevice != null ? e.CameraDevice.Code : "",
                    e.Direction,
                    e.MatchStatus,
                    e.Confidence,
                    e.FailureReason
                })
                .ToListAsync(cancellationToken);

            var areaMismatchCount = cameraEvents.Count(e => e.MatchStatus == CameraGateMatchStatusConstants.AreaMismatch);
            if (areaMismatchCount > 0)
            {
                score += Math.Min(30, areaMismatchCount * 15);
                riskSignals.Add(new OpsRiskSignal("Camera", EnterpriseSeverity.Critical, areaMismatchCount, "Camera ghi nhận xe sai khu vực.", "/Admin/Camera/Monitor"));
            }

            timeline.AddRange(cameraEvents.Select(e => new OpsTimelineEvent(
                e.CapturedAt,
                "Camera",
                e.MatchStatus == CameraGateMatchStatusConstants.Matched ? EnterpriseSeverity.Success :
                    e.MatchStatus == CameraGateMatchStatusConstants.AreaMismatch ? EnterpriseSeverity.Critical :
                    EnterpriseSeverity.Warning,
                $"{e.CameraCode} - {e.MatchStatus}",
                $"Hướng {e.Direction}, confidence {e.Confidence:P1}. {e.FailureReason}".Trim(),
                "/Admin/Camera/Monitor")));

            var bookingIds = bookings.Select(b => b.BookingID).ToList();
            var contracts = await _context.DigitalContracts
                .AsNoTracking()
                .Where(c => !c.IsDeleted && c.BookingID.HasValue && bookingIds.Contains(c.BookingID.Value))
                .OrderByDescending(c => c.CreatedAt)
                .Take(10)
                .Select(c => new
                {
                    c.BookingID,
                    c.CreatedAt,
                    c.ContractNo,
                    c.IsSignedByCustomer,
                    c.IsSignedByOwner,
                    c.RenderedContentHash
                })
                .ToListAsync(cancellationToken);

            var unsignedContracts = contracts.Count(c => !c.IsSignedByCustomer || !c.IsSignedByOwner);
            if (unsignedContracts > 0)
            {
                score += Math.Min(20, unsignedContracts * 10);
                riskSignals.Add(new OpsRiskSignal("Legal", EnterpriseSeverity.Warning, unsignedContracts, "Hợp đồng số chưa đủ chữ ký.", "/Admin/Booking"));
            }

            timeline.AddRange(contracts.Select(c => new OpsTimelineEvent(
                c.CreatedAt,
                "Contract",
                c.IsSignedByCustomer && c.IsSignedByOwner ? EnterpriseSeverity.Success : EnterpriseSeverity.Warning,
                $"Contract {c.ContractNo}",
                $"Booking #{c.BookingID}, customer signed: {c.IsSignedByCustomer}, owner signed: {c.IsSignedByOwner}, hash: {(string.IsNullOrWhiteSpace(c.RenderedContentHash) ? "missing" : "present")}.",
                "/Admin/Booking")));

            var handovers = await _context.HandoverRecords
                .AsNoTracking()
                .Where(h => !h.IsDeleted && bookingIds.Contains(h.BookingID))
                .OrderByDescending(h => h.RecordDate)
                .Take(10)
                .Select(h => new
                {
                    h.BookingID,
                    h.RecordDate,
                    h.Type,
                    h.EvidenceHash,
                    h.SignatureUrl
                })
                .ToListAsync(cancellationToken);

            var missingEvidence = handovers.Count(h => string.IsNullOrWhiteSpace(h.EvidenceHash) || string.IsNullOrWhiteSpace(h.SignatureUrl));
            if (missingEvidence > 0)
            {
                score += Math.Min(20, missingEvidence * 10);
                riskSignals.Add(new OpsRiskSignal("Legal", EnterpriseSeverity.Warning, missingEvidence, "Biên bản bàn giao thiếu chữ ký hoặc evidence hash.", "/Admin/Booking"));
            }

            timeline.AddRange(handovers.Select(h => new OpsTimelineEvent(
                h.RecordDate,
                "Handover",
                string.IsNullOrWhiteSpace(h.EvidenceHash) || string.IsNullOrWhiteSpace(h.SignatureUrl) ? EnterpriseSeverity.Warning : EnterpriseSeverity.Success,
                $"{h.Type} booking #{h.BookingID}",
                $"Evidence hash: {(string.IsNullOrWhiteSpace(h.EvidenceHash) ? "missing" : "present")}, signature: {(string.IsNullOrWhiteSpace(h.SignatureUrl) ? "missing" : "present")}.",
                "/Admin/Booking")));

            if (car.NextMaintenanceDate.HasValue && car.NextMaintenanceDate.Value <= now.AddDays(14))
            {
                score += 10;
                riskSignals.Add(new OpsRiskSignal("Fleet", EnterpriseSeverity.Warning, 1, $"Xe đến hạn bảo trì {car.NextMaintenanceDate:dd/MM/yyyy}.", "/Admin/Maintenance"));
            }

            var clampedScore = Math.Clamp(score, 0, 100);
            var actions = BuildVehicleActions(car.CarID, riskSignals);

            return new OpsVehicleTimeline(
                DateTime.UtcNow,
                car.CarID,
                $"{car.CarBrand?.Name} {car.CarName}".Trim(),
                car.LicensePlate,
                clampedScore,
                RiskLevel(clampedScore),
                riskSignals,
                timeline.OrderByDescending(e => e.Timestamp).Take(40).ToList(),
                actions);
        }

        public async Task<OpsCameraRiskSummary> SummarizeCameraRiskAsync(CancellationToken cancellationToken = default)
        {
            var since = DateTime.UtcNow.AddHours(-24);
            var events = await _context.CameraGateEvents
                .AsNoTracking()
                .Include(e => e.CameraDevice)
                .Where(e => !e.IsDeleted && e.CapturedAt >= since)
                .OrderByDescending(e => e.CapturedAt)
                .Take(200)
                .Select(e => new
                {
                    e.CameraGateEventID,
                    e.CapturedAt,
                    CameraCode = e.CameraDevice != null ? e.CameraDevice.Code : "",
                    e.LicensePlateRaw,
                    e.MatchStatus,
                    e.Confidence,
                    e.FailureReason,
                    e.CarID
                })
                .ToListAsync(cancellationToken);

            var matched = events.Count(e => e.MatchStatus == CameraGateMatchStatusConstants.Matched);
            var unknown = events.Count(e => e.MatchStatus == CameraGateMatchStatusConstants.UnknownPlate);
            var low = events.Count(e => e.MatchStatus == CameraGateMatchStatusConstants.LowConfidence);
            var mismatch = events.Count(e => e.MatchStatus == CameraGateMatchStatusConstants.AreaMismatch);
            var score = Math.Clamp(mismatch * 15 + unknown * 8 + low * 4, 0, 100);

            var riskEvents = events
                .Where(e => e.MatchStatus != CameraGateMatchStatusConstants.Matched)
                .Take(20)
                .Select(e => new OpsCameraRiskEvent(
                    e.CameraGateEventID,
                    e.CapturedAt,
                    e.CameraCode,
                    e.LicensePlateRaw,
                    e.MatchStatus,
                    e.Confidence,
                    e.FailureReason,
                    e.CarID))
                .ToList();

            var actions = new List<OpsRecommendedAction>();
            if (mismatch > 0)
                actions.Add(new OpsRecommendedAction("P0", "Fleet", "Mở Camera Gate Monitor và xác minh xe sai khu vực.", $"{mismatch} event AreaMismatch trong 24h.", "/Admin/Camera/Monitor"));
            if (unknown > 0)
                actions.Add(new OpsRecommendedAction("P1", "Security", "Đối soát biển số lạ với camera snapshot và danh sách xe đối tác.", $"{unknown} event UnknownPlate trong 24h.", "/Admin/Camera/Monitor"));
            if (low > 0)
                actions.Add(new OpsRecommendedAction("P2", "Fleet", "Kiểm tra góc đặt camera hoặc ngưỡng confidence OCR.", $"{low} event low confidence trong 24h.", "/Admin/Camera"));

            if (actions.Count == 0)
                actions.Add(new OpsRecommendedAction("P3", "Fleet", "Không có rủi ro camera nổi bật trong 24h.", "Camera events hiện ổn định.", "/Admin/Camera/Monitor"));

            return new OpsCameraRiskSummary(DateTime.UtcNow, score, events.Count, matched, unknown, low, mismatch, riskEvents, actions);
        }

        public async Task<OpsActionPlan> RecommendNextActionsAsync(CancellationToken cancellationToken = default)
        {
            var snapshot = await GetOpsSnapshotAsync(cancellationToken);
            var money = await ExplainMoneyExceptionsAsync(cancellationToken);
            var camera = await SummarizeCameraRiskAsync(cancellationToken);

            var actions = new List<OpsRecommendedAction>();
            actions.AddRange(money.RecommendedActions);
            actions.AddRange(camera.RecommendedActions.Where(a => a.Priority is "P0" or "P1"));

            foreach (var signal in snapshot.TopSignals)
            {
                if (signal.Message.StartsWith("Overdue returns", StringComparison.OrdinalIgnoreCase))
                    actions.Add(new OpsRecommendedAction("P0", "Fleet", "Gọi khách/owner và mở booking quá hạn để xử lý trả xe.", signal.Message, signal.Url));
                else if (signal.Message.StartsWith("Support SLA", StringComparison.OrdinalIgnoreCase))
                    actions.Add(new OpsRecommendedAction("P0", "Support", "Ưu tiên ticket quá SLA trước khi xử lý queue thường.", signal.Message, signal.Url));
                else if (signal.Message.StartsWith("Contract evidence", StringComparison.OrdinalIgnoreCase))
                    actions.Add(new OpsRecommendedAction("P1", "Legal ops", "Hoàn tất chữ ký/evidence hash trước bàn giao hoặc refund.", signal.Message, signal.Url));
                else if (signal.Message.StartsWith("Fleet readiness", StringComparison.OrdinalIgnoreCase))
                    actions.Add(new OpsRecommendedAction("P1", "Fleet", "Lọc xe thiếu tracking, bảo trì gần hạn và xe chờ duyệt.", signal.Message, signal.Url));
            }

            if (actions.Count == 0)
                actions.Add(new OpsRecommendedAction("P3", "Operations", "Tiếp tục theo dõi Command Center.", "Không có queue P0/P1 nổi bật tại thời điểm này.", "/Admin/Enterprise"));

            var deduped = actions
                .GroupBy(a => $"{a.Priority}|{a.Owner}|{a.Action}")
                .Select(g => g.First())
                .OrderBy(a => PriorityRank(a.Priority))
                .Take(10)
                .ToList();

            var score = Math.Max(snapshot.RiskScore, Math.Max(money.RiskScore, camera.RiskScore));
            return new OpsActionPlan(DateTime.UtcNow, score, RiskLevel(score), deduped);
        }

        private async Task<Car?> ResolveCarAsync(int? carId, string? licensePlate, CancellationToken cancellationToken)
        {
            var query = _context.Cars
                .Include(c => c.CarBrand)
                .Where(c => !c.IsDeleted)
                .AsQueryable();

            if (carId.HasValue && carId.Value > 0)
                return await query.FirstOrDefaultAsync(c => c.CarID == carId.Value, cancellationToken);

            var normalizedPlate = LicensePlateNormalizer.Normalize(licensePlate);
            if (!string.IsNullOrWhiteSpace(normalizedPlate))
            {
                var byNormalizedPlate = await query.FirstOrDefaultAsync(c => c.LicensePlateNormalized == normalizedPlate, cancellationToken);
                if (byNormalizedPlate != null)
                    return byNormalizedPlate;

                var candidates = await query
                    .OrderBy(c => c.CarID)
                    .Take(500)
                    .ToListAsync(cancellationToken);
                var byRawPlate = candidates.FirstOrDefault(c => LicensePlateNormalizer.Normalize(c.LicensePlate) == normalizedPlate);
                if (byRawPlate != null)
                    return byRawPlate;
            }

            var alertedCarId = await _context.Alerts
                .AsNoTracking()
                .Where(a => !a.IsDeleted && !IsTerminalAlertStatus(a.Status))
                .OrderByDescending(a => a.AlertTime)
                .Select(a => (int?)a.CarID)
                .FirstOrDefaultAsync(cancellationToken);
            if (alertedCarId.HasValue)
                return await query.FirstOrDefaultAsync(c => c.CarID == alertedCarId.Value, cancellationToken);

            var overdueCarId = await _context.Bookings
                .AsNoTracking()
                .Where(b =>
                    !b.IsDeleted &&
                    b.BookingStatus != null &&
                    b.BookingStatus.Name == BookingStatusConstants.Active &&
                    b.EndDate < DateTime.UtcNow &&
                    b.ActualReturnDate == null)
                .OrderBy(b => b.EndDate)
                .Select(b => (int?)b.CarID)
                .FirstOrDefaultAsync(cancellationToken);
            if (overdueCarId.HasValue)
                return await query.FirstOrDefaultAsync(c => c.CarID == overdueCarId.Value, cancellationToken);

            return await query.OrderBy(c => c.CarID).FirstOrDefaultAsync(cancellationToken);
        }

        private static int CalculateQueueRiskScore(IEnumerable<EnterpriseQueueItem> queues)
        {
            var score = queues.Where(q => q.Count > 0).Sum(q =>
                q.Severity switch
                {
                    EnterpriseSeverity.Critical => Math.Min(30, q.Count * 15),
                    EnterpriseSeverity.Warning => Math.Min(18, q.Count * 8),
                    EnterpriseSeverity.Info => Math.Min(10, q.Count * 4),
                    _ => 0
                });
            return Math.Clamp(score, 0, 100);
        }

        private static IReadOnlyList<OpsRecommendedAction> BuildMoneyActions(int payOSWithoutLedger, int ledgerWithoutPayOS, int pendingRefunds, int pendingPayouts)
        {
            var actions = new List<OpsRecommendedAction>();
            if (payOSWithoutLedger > 0 || ledgerWithoutPayOS > 0)
                actions.Add(new OpsRecommendedAction("P0", "Finance", "Chạy đối soát PayOS và tạo/điều chỉnh ledger idempotent.", $"Có {payOSWithoutLedger + ledgerWithoutPayOS} lệch PayOS/ledger.", "/Admin/Payment"));
            if (pendingRefunds > 0)
                actions.Add(new OpsRecommendedAction("P1", "Finance", "Duyệt refund request theo maker-checker trước khi payout.", $"{pendingRefunds} refund request chưa terminal.", "/Admin/Payment"));
            if (pendingPayouts > 0)
                actions.Add(new OpsRecommendedAction("P1", "Finance", "Kiểm tra payout pending/manual và lỗi provider gần nhất.", $"{pendingPayouts} payout cần theo dõi.", "/Admin/Payment"));
            if (actions.Count == 0)
                actions.Add(new OpsRecommendedAction("P3", "Finance", "Không có exception tiền nổi bật.", "PayOS, ledger, refund và payout hiện không có queue rủi ro.", "/Admin/Payment"));
            return actions;
        }

        private static IReadOnlyList<OpsRecommendedAction> BuildVehicleActions(int carId, IReadOnlyList<OpsRiskSignal> signals)
        {
            var actions = new List<OpsRecommendedAction>();
            if (signals.Any(s => s.Area == "Camera" && s.Severity == EnterpriseSeverity.Critical))
                actions.Add(new OpsRecommendedAction("P0", "Fleet", "Xác minh camera mismatch bằng snapshot và liên hệ nhân sự khu vực.", $"Xe #{carId} có camera mismatch.", "/Admin/Camera/Monitor"));
            if (signals.Any(s => s.Message.Contains("GPS", StringComparison.OrdinalIgnoreCase)))
                actions.Add(new OpsRecommendedAction("P1", "Fleet", "Kiểm tra tracker/mobile GPS và yêu cầu cập nhật vị trí.", $"Xe #{carId} có tín hiệu GPS thiếu hoặc cũ.", "/Tracking/Map"));
            if (signals.Any(s => s.Area == "Legal"))
                actions.Add(new OpsRecommendedAction("P1", "Legal ops", "Hoàn tất chữ ký/evidence hash trước khi đóng chuyến hoặc refund.", $"Xe #{carId} có lỗ hổng evidence.", "/Admin/Booking"));
            if (actions.Count == 0)
                actions.Add(new OpsRecommendedAction("P3", "Fleet", "Không có hành động khẩn cấp cho xe này.", $"Xe #{carId} không có tín hiệu P0/P1 trong timeline.", "/Tracking/Map"));
            return actions;
        }

        private static bool IsTerminalAlertStatus(string? status) =>
            string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase);

        private static string RiskLevel(int score) =>
            score >= 70 ? "High" :
            score >= 35 ? "Medium" :
            "Low";

        private static int SeverityRank(string severity) =>
            severity switch
            {
                EnterpriseSeverity.Critical => 0,
                EnterpriseSeverity.Warning => 1,
                EnterpriseSeverity.Info => 2,
                EnterpriseSeverity.Success => 3,
                _ => 4
            };

        private static int PriorityRank(string priority) =>
            priority switch
            {
                "P0" => 0,
                "P1" => 1,
                "P2" => 2,
                "P3" => 3,
                _ => 4
            };
    }
}
