namespace SmartFacility.Domain.Entities;

public sealed class ScadaAlarmEvent
{
    public long Id { get; set; }
    public string SourceSheet { get; set; } = string.Empty;
    public string? SectionRaw { get; set; }
    public string? LocationRaw { get; set; }
    public string? FloorRaw { get; set; }
    public string? ZoneRaw { get; set; }
    public string? AlarmType { get; set; }
    public string? InterventionLevel { get; set; }
    public string? Description { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ClearedAt { get; set; }
    public string? ResponsibleRaw { get; set; }
    public string? StatusRaw { get; set; }
    public string? Note { get; set; }
    public string? DateTimeParseStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
