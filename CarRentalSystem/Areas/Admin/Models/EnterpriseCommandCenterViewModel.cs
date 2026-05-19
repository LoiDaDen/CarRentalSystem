namespace CarRentalSystem.Areas.Admin.Models
{
    public class EnterpriseCommandCenterViewModel
    {
        public DateTime GeneratedAtUtc { get; set; }
        public EnterpriseFinanceSnapshot Finance { get; set; } = new();
        public EnterpriseFleetSnapshot Fleet { get; set; } = new();
        public List<EnterpriseKpiCard> Kpis { get; set; } = new();
        public List<EnterpriseQueueItem> Queues { get; set; } = new();
        public List<EnterpriseCapabilityStatus> Capabilities { get; set; } = new();
        public List<EnterpriseWorkflowGroup> WorkflowGroups { get; set; } = new();
    }

    public class EnterpriseKpiCard
    {
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Severity { get; set; } = EnterpriseSeverity.Info;
        public string Icon { get; set; } = "bi-speedometer2";
        public string? Url { get; set; }
    }

    public class EnterpriseQueueItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Severity { get; set; } = EnterpriseSeverity.Info;
        public string Owner { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public class EnterpriseCapabilityStatus
    {
        public string Module { get; set; } = string.Empty;
        public string Status { get; set; } = EnterpriseCapabilityState.Partial;
        public string CurrentCoverage { get; set; } = string.Empty;
        public string NextStep { get; set; } = string.Empty;
        public string Priority { get; set; } = "P1";
    }

    public class EnterpriseWorkflowGroup
    {
        public string Role { get; set; } = string.Empty;
        public string Focus { get; set; } = string.Empty;
        public List<EnterpriseWorkflowLink> Links { get; set; } = new();
    }

    public class EnterpriseWorkflowLink
    {
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-box-arrow-up-right";
    }

    public class EnterpriseFinanceSnapshot
    {
        public decimal LedgerCharge30Days { get; set; }
        public decimal LedgerRefund30Days { get; set; }
        public decimal LedgerDeduction30Days { get; set; }
        public decimal LedgerPayout30Days { get; set; }
        public int PendingRefunds { get; set; }
        public int PendingManualPayouts { get; set; }
        public int PayOSPaidWithoutLedger { get; set; }
        public int LedgerWithoutPayOS { get; set; }
    }

    public class EnterpriseFleetSnapshot
    {
        public int TotalCars { get; set; }
        public int AvailableCars { get; set; }
        public int RentedCars { get; set; }
        public int MaintenanceCars { get; set; }
        public int PendingApprovalCars { get; set; }
        public int MaintenanceDueSoon { get; set; }
        public int MissingTracking { get; set; }
        public int OpenAlerts { get; set; }
        public decimal MonthlyUtilizationPercent { get; set; }
    }

    public static class EnterpriseSeverity
    {
        public const string Critical = "critical";
        public const string Warning = "warning";
        public const string Success = "success";
        public const string Info = "info";
    }

    public static class EnterpriseCapabilityState
    {
        public const string Live = "Live";
        public const string Partial = "Partial";
        public const string NeedsEvidence = "NeedsEvidence";
        public const string Planned = "Planned";
    }
}
