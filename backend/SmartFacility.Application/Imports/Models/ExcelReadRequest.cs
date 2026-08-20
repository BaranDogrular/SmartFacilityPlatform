namespace SmartFacility.Application.Imports.Models;

public sealed record ExcelReadRequest(
    string FilePath,
    IReadOnlyList<WorksheetReadRequest> Worksheets);

public sealed record WorksheetReadRequest(
    string Name,
    int FirstRowNumber,
    int? HeaderRowNumber = null);
