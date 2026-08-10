namespace SmartFacility.Domain.Entities;

public sealed class ImportBatch
{
    public long Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int SuccessfulRows { get; set; }
    public int FailedRows { get; set; }

    public ICollection<ImportError> Errors { get; set; } = [];
    public ICollection<ImportSourceRecord> SourceRecords { get; set; } = [];
}
