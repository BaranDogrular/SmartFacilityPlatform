namespace SmartFacility.Domain.Entities;

public sealed class ScadaOutage
{
    public long Id { get; set; }
    public string SourceSheet { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Description { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? RestoredAt { get; set; }
    public string? DurationRaw { get; set; }
    public string? StatusRaw { get; set; }
    public string? DateTimeParseStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
