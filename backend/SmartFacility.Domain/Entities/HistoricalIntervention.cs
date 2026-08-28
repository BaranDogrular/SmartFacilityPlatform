namespace SmartFacility.Domain.Entities;

public sealed class HistoricalIntervention
{
    public long Id { get; set; }
    public long WorkOrderId { get; set; }
    public long ImportBatchId { get; set; }
    public int SourceYear { get; set; }
    public string SourceWorkOrderNumber { get; set; } = string.Empty;
    public DateTime ReportedDateTime { get; set; }
    public string AssetCodeRaw { get; set; } = string.Empty;
    public string? WorkOrderStatus { get; set; }
    public string? AssetName { get; set; }
    public DateTime? CompletionDateTime { get; set; }
    public string? RequestDescriptionRaw { get; set; }
    public string? RequestDescriptionSanitized { get; set; }
    public string? WorkPerformedDescriptionRaw { get; set; }
    public string? WorkPerformedDescriptionSanitized { get; set; }
    public string? FailureReasonCode { get; set; }
    public string? FailureReasonDescriptionRaw { get; set; }
    public string? FailureReasonDescriptionSanitized { get; set; }
    public string? MaintenanceDurationRaw { get; set; }
    public string? DowntimeDurationRaw { get; set; }
    public string? LaborDurationRaw { get; set; }
    public string? MaterialCostRaw { get; set; }
    public string? LaborCostRaw { get; set; }
    public string? TotalCostRaw { get; set; }
    public string? TotalCostCurrencyRaw { get; set; }
    public HistoricalInterventionQuality InterventionQuality { get; set; }
    public string SourceRowFingerprint { get; set; } = string.Empty;
    public string FingerprintAlgorithm { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceSheet { get; set; } = string.Empty;
    public int SourceRowNumber { get; set; }
    public DateTimeOffset ImportedAt { get; set; }

    public WorkOrder WorkOrder { get; set; } = null!;
    public ImportBatch ImportBatch { get; set; } = null!;
}
