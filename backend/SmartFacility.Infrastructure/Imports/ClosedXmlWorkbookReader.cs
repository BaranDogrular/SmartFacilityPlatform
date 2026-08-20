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

        var worksheets = new List<WorksheetState>(request.Worksheets.Count);
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

            worksheets.Add(new WorksheetState(worksheetRequest, worksheet, lastRowNumber));
        }

        foreach (var state in worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var headerRowNumber = state.Request.HeaderRowNumber ?? state.Request.FirstRowNumber;
            yield return CreateRawRow(state.Worksheet.Row(headerRowNumber));
        }

        foreach (var state in worksheets)
        {
            var headerRowNumber = state.Request.HeaderRowNumber ?? state.Request.FirstRowNumber;

            for (var rowNumber = state.Request.FirstRowNumber;
                 rowNumber <= state.LastRowNumber;
                 rowNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (rowNumber == headerRowNumber)
                {
                    continue;
                }

                var rawRow = CreateRawRow(state.Worksheet.Row(rowNumber));
                if (rawRow.IsEmpty)
                {
                    continue;
                }

                yield return rawRow;

                if (rowNumber % 1000 == 0)
                {
                    await Task.Yield();
                }
            }
        }
    }

    private static RawExcelRow CreateRawRow(IXLRow row)
    {
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

        return new RawExcelRow(row.Worksheet.Name, row.RowNumber(), cells);
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

    private sealed record WorksheetState(
        WorksheetReadRequest Request,
        IXLWorksheet Worksheet,
        int LastRowNumber);
}
