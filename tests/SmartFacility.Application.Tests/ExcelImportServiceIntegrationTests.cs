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
            processors,
            NullLogger<ExcelImportService>.Instance);
    }
}
