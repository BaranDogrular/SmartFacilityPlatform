using System.Globalization;
using System.Runtime.CompilerServices;
using ClosedXML.Excel;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Infrastructure.Imports;

public sealed class ClosedXmlWorkbookReader : IExcelWorkbookReader
{
    public async IAsyncEnumerable<RawExcelRow> ReadRowsAsync(
        ExcelReadRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var stream = new FileStream(
            request.FilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 64 * 1024,
            useAsync: true);
        using var workbook = new XLWorkbook(stream);

        foreach (var worksheetRequest in request.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!workbook.TryGetWorksheet(worksheetRequest.Name, out var worksheet))
            {
                throw new InvalidOperationException(
                    $"Configured worksheet '{worksheetRequest.Name}' was not found.");
            }

            var lastRowNumber = worksheet
                .LastRowUsed(XLCellsUsedOptions.AllContents)?
                .RowNumber() ?? 0;

            for (var rowNumber = worksheetRequest.FirstRowNumber;
                 rowNumber <= lastRowNumber;
                 rowNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = worksheet.Row(rowNumber);
                var cells = new Dictionary<string, RawExcelCell>(StringComparer.OrdinalIgnoreCase);

                foreach (var cell in row.CellsUsed(XLCellsUsedOptions.AllContents))
                {
                    var rawCell = CreateRawCell(cell);
                    if (!string.IsNullOrWhiteSpace(rawCell.RawValue) ||
                        !string.IsNullOrWhiteSpace(rawCell.Formula))
                    {
                        cells[rawCell.Column] = rawCell;
                    }
                }

                if (cells.Count == 0)
                {
                    continue;
                }

                yield return new RawExcelRow(worksheet.Name, rowNumber, cells);

                if (rowNumber % 1000 == 0)
                {
                    await Task.Yield();
                }
            }
        }
    }

    private static RawExcelCell CreateRawCell(IXLCell cell)
    {
        var value = cell.Value;
        var rawValue = GetInvariantValue(value);
        var formattedValue = cell.GetFormattedString(CultureInfo.InvariantCulture);

        return new RawExcelCell(
            cell.WorksheetColumn().ColumnLetter(),
            rawValue,
            string.IsNullOrWhiteSpace(formattedValue) ? null : formattedValue,
            value.Type.ToString(),
            value.IsUnifiedNumber ? value.GetUnifiedNumber() : null,
            value.IsDateTime ? value.GetDateTime() : null,
            value.IsTimeSpan ? value.GetTimeSpan() : null,
            cell.HasFormula ? cell.FormulaA1 : null);
    }

    private static string? GetInvariantValue(XLCellValue value)
    {
        if (value.IsBlank)
        {
            return null;
        }

        if (value.IsNumber)
        {
            return value.GetNumber().ToString("R", CultureInfo.InvariantCulture);
        }

        if (value.IsDateTime)
        {
            return value.GetDateTime().ToString("O", CultureInfo.InvariantCulture);
        }

        if (value.IsTimeSpan)
        {
            return value.GetTimeSpan().ToString("c", CultureInfo.InvariantCulture);
        }

        if (value.IsBoolean)
        {
            return value.GetBoolean().ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }
}
