using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Tests;

public sealed class AssetImportProcessorTests
{
    [Fact]
    public async Task Empty_asset_code_creates_validation_error()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var profile = TestProfiles.Asset();
        var processor = new AssetImportProcessor(database.Store);
        var row = RawRowFactory.Row("Assets", 2, RawRowFactory.Text("B", "  "));

        var result = await processor.ProcessAsync(row, profile, CancellationToken.None);

        Assert.Equal(ImportRowDisposition.Error, result.Disposition);
    }

    [Fact]
    public async Task Self_parent_and_date_sentinel_are_normalized_to_null()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var profile = TestProfiles.Asset();
        var processor = new AssetImportProcessor(database.Store);
        var row = RawRowFactory.Row(
            "Assets",
            2,
            RawRowFactory.Text("B", "A-100"),
            RawRowFactory.Text("C", "Pump"),
            RawRowFactory.Number("E", 1),
            RawRowFactory.Text("N", "A-100"));

        var result = await processor.ProcessAsync(row, profile, CancellationToken.None);
        var asset = Assert.IsType<Asset>(result.Entity);

        Assert.Equal(ImportRowDisposition.Success, result.Disposition);
        Assert.Null(asset.ParentAssetId);
        Assert.Null(asset.ParentAsset);
        Assert.Null(asset.LastMaintenanceDate);
    }
}
