namespace SmartFacility.Domain.Entities;

public sealed class ImportSourceRecord
{
    public long Id { get; set; }
    public long ImportBatchId { get; set; }
    public string SourceSheet { get; set; } = string.Empty;
    public int SourceRowNumber { get; set; }
    public string RawData { get; set; } = string.Empty;
    public string? RawFormulaData { get; set; }
    public string ParseStatus { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public ImportBatch ImportBatch { get; set; } = null!;
}
