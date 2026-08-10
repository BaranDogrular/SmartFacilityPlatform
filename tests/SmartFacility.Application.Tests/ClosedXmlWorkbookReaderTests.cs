using ClosedXML.Excel;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Infrastructure.Imports;

namespace SmartFacility.Application.Tests;

public sealed class ClosedXmlWorkbookReaderTests
{
    [Fact]
    public async Task Reader_preserves_raw_value_and_formula_metadata()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"smart-facility-{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.AddWorksheet("Data");
                worksheet.Cell("A1").Value = "Header";
                worksheet.Cell("A2").Value = 1d;
                worksheet.Cell("B2").FormulaA1 = "A2+1";
                workbook.SaveAs(filePath);
            }

            var reader = new ClosedXmlWorkbookReader();
            var rows = new List<RawExcelRow>();
            await foreach (var row in reader.ReadRowsAsync(
                               new ExcelReadRequest(
                                   filePath,
                                   [new WorksheetReadRequest("Data", 1)])))
            {
                rows.Add(row);
            }

            var dataRow = Assert.Single(rows, row => row.RowNumber == 2);
            Assert.Equal(1d, dataRow.GetCell("A")?.NumericValue);
            Assert.Equal("A2+1", dataRow.GetCell("B")?.Formula);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
