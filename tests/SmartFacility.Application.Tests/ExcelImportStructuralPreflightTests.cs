using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Infrastructure.Imports;

namespace SmartFacility.Application.Tests;

public sealed class ExcelImportStructuralPreflightTests
{
    [Fact]
    public async Task Missing_later_worksheet_fails_before_any_row_write()
    {
        await AssertStructuralFailureAsync(addSecondWorksheet: false, secondHeader: null);
    }

    [Fact]
    public async Task Invalid_later_header_fails_before_any_row_write()
    {
        await AssertStructuralFailureAsync(addSecondWorksheet: true, secondHeader: "Wrong header");
    }

    private static async Task AssertStructuralFailureAsync(
        bool addSecondWorksheet,
        string? secondHeader)
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"smart-facility-structural-{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var workbook = new XLWorkbook())
            {
                var first = workbook.AddWorksheet("First");
                first.Cell("G1").Value = "Description";
                first.Cell("G2").Value = "First valid alarm";
                if (addSecondWorksheet)
                {
                    var second = workbook.AddWorksheet("Second");
                    second.Cell("G1").Value = secondHeader;
                }

                workbook.SaveAs(filePath);
            }

            await using var database = await SqliteTestDatabase.CreateAsync();
            var profile = CreateProfile();
            var service = new ExcelImportService(
                new ClosedXmlWorkbookReader(),
                database.Store,
                new ImportProfileCatalog([profile]),
                new ImportFingerprintProvider(),
                [new ScadaAlarmImportProcessor()],
                NullLogger<ExcelImportService>.Instance);

            await Assert.ThrowsAsync<ImportPipelineException>(() =>
                service.ImportAsync(new ImportRequest(ImportProfileKeys.ScadaAlarm, filePath)));

            var batch = await database.Context.ImportBatches.SingleAsync();
            Assert.Equal("Failed", batch.Status);
            Assert.Equal(0, await database.Context.ScadaAlarmEvents.CountAsync());
            Assert.Equal(0, await database.Context.ImportSourceRecords.CountAsync());
            var error = await database.Context.ImportErrors.SingleAsync();
            Assert.Null(error.RowNumber);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private static ScadaAlarmImportProfile CreateProfile() => new(new ImportProfileOptions
    {
        SourceType = ImportSourceTypes.ScadaAlarm,
        Worksheets =
        [
            Worksheet("First"),
            Worksheet("Second")
        ],
        Columns = new Dictionary<string, string>
        {
            ["Description"] = "G"
        }
    });

    private static WorksheetProfileOptions Worksheet(string name) => new()
    {
        Name = name,
        HeaderRowNumber = 1,
        FirstDataRowNumber = 2,
        ExpectedHeaders = new Dictionary<string, string>
        {
            ["G"] = "Description"
        }
    };
}
