namespace SmartFacility.Application.Imports.Models;

public sealed record ImportResult(
    long BatchId,
    string Status,
    int TotalRows,
    int SuccessfulRows,
    int FailedRows,
    int IgnoredRows,
    int DuplicateRows);
