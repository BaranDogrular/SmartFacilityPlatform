using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Imports.Processors;

public sealed class AssetImportProcessor(IImportDataStore dataStore) : IImportRowProcessor
{
    public string ProfileKey => ImportProfileKeys.Asset;

    public async Task<ImportRowDecision> ProcessAsync(
        RawExcelRow row,
        IImportSourceProfile profile,
        CancellationToken cancellationToken)
    {
        var assetCode = ProfileCellReader.Text(profile, row, "AssetCode");
        if (assetCode is null)
        {
            return ImportRowDecision.Error("AssetCode is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var asset = await dataStore.FindAssetByCodeAsync(assetCode, cancellationToken);
        asset ??= new Asset
        {
            AssetCode = assetCode,
            CreatedAt = now
        };

        asset.Name = ProfileCellReader.Text(profile, row, "Name") ?? assetCode;
        asset.AssetType = ProfileCellReader.Text(profile, row, "AssetType");
        asset.SerialNumber = ProfileCellReader.Text(profile, row, "SerialNumber");
        asset.Status = ProfileCellReader.Text(profile, row, "Status");
        asset.LastMaintenanceDate = ExcelValueParser
            .ParseDate(profile.GetCell(row, "LastMaintenanceDate"), treatOneAsNull: true)
            .Value;
        asset.UpdatedAt = now;

        var upperAssetCode = ProfileCellReader.Text(profile, row, "UpperAssetCode");
        if (upperAssetCode is null ||
            string.Equals(upperAssetCode, assetCode, StringComparison.OrdinalIgnoreCase))
        {
            asset.ParentAssetId = null;
            asset.ParentAsset = null;
        }
        else
        {
            asset.ParentAsset = await dataStore.FindAssetByCodeAsync(upperAssetCode, cancellationToken);
            asset.ParentAssetId = asset.ParentAsset?.Id;
        }

        var buildingName = ProfileCellReader.Text(profile, row, "BuildingName");
        if (buildingName is not null)
        {
            var building = await dataStore.GetOrAddBuildingAsync(
                ProfileCellReader.Text(profile, row, "BuildingCode"),
                buildingName,
                cancellationToken);
            asset.Building = building;
            asset.BuildingId = building.Id == 0 ? null : building.Id;

            var locationName = ProfileCellReader.Text(profile, row, "LocationName");
            if (locationName is not null)
            {
                var location = await dataStore.GetOrAddLocationAsync(building, locationName, cancellationToken);
                asset.Location = location;
                asset.LocationId = location.Id == 0 ? null : location.Id;
            }
        }

        var groupName = ProfileCellReader.Text(profile, row, "AssetGroupName");
        if (groupName is not null)
        {
            var group = await dataStore.GetOrAddAssetGroupAsync(
                ProfileCellReader.Text(profile, row, "AssetGroupCode"),
                groupName,
                cancellationToken);
            asset.AssetGroup = group;
            asset.AssetGroupId = group.Id == 0 ? null : group.Id;
        }

        return ImportRowDecision.Success(asset);
    }
}
