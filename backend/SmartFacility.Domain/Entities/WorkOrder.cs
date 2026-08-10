namespace SmartFacility.Domain.Entities;

public sealed class WorkOrder
{
    public long Id { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public DateTime? ReportedDateTime { get; set; }
    public long? AssetId { get; set; }
    public string? Description { get; set; }
    public string? Discipline { get; set; }
    public string? RequestedByName { get; set; }
    public string? AssignedPersonnelName { get; set; }
    public string? Status { get; set; }
    public string? WorkType { get; set; }
    public string? FailureType { get; set; }
    public string? FailureReason { get; set; }
    public long? BuildingId { get; set; }
    public long? LocationId { get; set; }
    public string? ResponseDurationRaw { get; set; }
    public string? DowntimeRaw { get; set; }
    public string? MaintenanceDurationRaw { get; set; }
    public string? TotalCostRaw { get; set; }
    public string? ServiceCostRaw { get; set; }
    public string? RawStatusCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Asset? Asset { get; set; }
    public Building? Building { get; set; }
    public Location? Location { get; set; }
}
