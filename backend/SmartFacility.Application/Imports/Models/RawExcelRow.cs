namespace SmartFacility.Application.Imports.Models;

public sealed record RawExcelRow(
    string SheetName,
    int RowNumber,
    IReadOnlyDictionary<string, RawExcelCell> Cells)
{
    public bool IsEmpty => Cells.Values.All(cell => string.IsNullOrWhiteSpace(cell.RawValue));

    public RawExcelCell? GetCell(string column) =>
        Cells.GetValueOrDefault(column.ToUpperInvariant());
}
