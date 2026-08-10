namespace SmartFacility.Domain.Entities;

public sealed class HistoricalWorkOrder
{
    public long Id { get; set; }
    public string? SourceReference { get; set; }
    public DateTime? ReportedDateTime { get; set; }
    public string? Description { get; set; }
    public string? Discipline { get; set; }
    public string? PersonnelName { get; set; }
    public string? BuildingNameRaw { get; set; }
    public string? LocationNameRaw { get; set; }
    public string? ResolutionDurationRaw { get; set; }
    public string? RawData { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
