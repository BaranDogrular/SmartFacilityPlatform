namespace SmartFacility.Domain.Entities;

public sealed class ImportError
{
    public long Id { get; set; }
    public long ImportBatchId { get; set; }
    public int? RowNumber { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? RawData { get; set; }

    public ImportBatch ImportBatch { get; set; } = null!;
}
