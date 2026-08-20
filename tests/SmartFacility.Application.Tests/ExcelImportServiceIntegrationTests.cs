using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;

namespace SmartFacility.Application.Tests;

public sealed class ExcelImportServiceIntegrationTests
{
    [Fact]
    public async Task Invalid_row_creates_import_error_and_source_record()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var profile = TestProfiles.Asset();
        var service = CreateService(
            database,
            profile,
            RawRowFactory.Row("Assets", 1, RawRowFactory.Text("B", "Asset Code")),
            RawRowFactory.Row("Assets", 2, RawRowFactory.Text("C", "Missing code")));

        var result = await service.ImportAsync(new ImportRequest(ImportProfileKeys.Asset, "sample.xlsx"));

        Assert.Equal("CompletedWithErrors", result.Status);
        Assert.Equal(1, result.FailedRows);
        Assert.Equal(1, await database.Context.ImportErrors.CountAsync());
        Assert.Equal("Failed", (await database.Context.ImportSourceRecords.SingleAsync()).ParseStatus);
    }

    [Fact]
    public async Task Reimporting_same_row_does_not_duplicate_core_entity()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var profile = TestProfiles.Asset();
        var service = CreateService(
            database,
            profile,
            RawRowFactory.Row("Assets", 1, RawRowFactory.Text("B", "Asset Code")),
            RawRowFactory.Row(
                "Assets",
                2,
                RawRowFactory.Text("B", "A-100"),
                RawRowFactory.Text("C", "Pump")));

        var first = await service.ImportAsync(new ImportRequest(ImportProfileKeys.Asset, "sample.xlsx"));
        var second = await service.ImportAsync(new ImportRequest(ImportProfileKeys.Asset, "sample.xlsx"));

        Assert.Equal(1, first.SuccessfulRows);
        Assert.Equal(1, second.DuplicateRows);
        Assert.Equal(1, await database.Context.Assets.CountAsync());
        Assert.Equal(2, await database.Context.ImportSourceRecords.CountAsync());
    }

    [Fact]
    public async Task Same_fingerprint_on_different_sheets_is_not_a_duplicate()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var profile = new ScadaAlarmImportProfile(new ImportProfileOptions
        {
            SourceType = ImportSourceTypes.ScadaAlarm,
            Worksheets = [Worksheet("First"), Worksheet("Second")],
            Columns = new Dictionary<string, string>
            {
                ["Description"] = "G"
            }
        });
        var rows = new[]
        {
            RawRowFactory.Row("First", 1, RawRowFactory.Text("G", "Description")),
            RawRowFactory.Row("Second", 1, RawRowFactory.Text("G", "Description")),
            RawRowFactory.Row("First", 2, RawRowFactory.Text("G", "Same alarm")),
            RawRowFactory.Row("Second", 2, RawRowFactory.Text("G", "Same alarm"))
        };
        var service = new ExcelImportService(
            new FakeWorkbookReader(rows),
            database.Store,
            new ImportProfileCatalog([profile]),
            new ImportFingerprintProvider(),
            [new ScadaAlarmImportProcessor()],
            NullLogger<ExcelImportService>.Instance);

        var result = await service.ImportAsync(
            new ImportRequest(ImportProfileKeys.ScadaAlarm, "sample.xlsx"));

        Assert.Equal(2, result.SuccessfulRows);
        Assert.Equal(0, result.DuplicateRows);
        Assert.Equal(2, await database.Context.ScadaAlarmEvents.CountAsync());
    }

    private static ExcelImportService CreateService(
        SqliteTestDatabase database,
        IImportSourceProfile profile,
        params RawExcelRow[] rows)
    {
        var catalog = new ImportProfileCatalog([profile]);
        IImportRowProcessor[] processors = [new AssetImportProcessor(database.Store)];

        return new ExcelImportService(
            new FakeWorkbookReader(rows),
            database.Store,
            catalog,
            new ImportFingerprintProvider(),
            processors,
            NullLogger<ExcelImportService>.Instance);
    }

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
