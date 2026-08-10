using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Imports.Processors;

public sealed class WorkOrderImportProcessor(IImportDataStore dataStore) : IImportRowProcessor
{
    public string ProfileKey => ImportProfileKeys.WorkOrder;

    public async Task<ImportRowDecision> ProcessAsync(
        RawExcelRow row,
        IImportSourceProfile profile,
        CancellationToken cancellationToken)
    {
        var number = ProfileCellReader.Text(profile, row, "WorkOrderNumber");
        if (number is null)
        {
            return ImportRowDecision.Error("WorkOrderNumber is required by the current domain model.");
        }

        var reportedAt = ExcelValueParser.CombineDateAndTime(
            profile.GetCell(row, "ReportedDate"),
            profile.GetCell(row, "ReportedTime"));

        var workOrder = new WorkOrder
        {
            WorkOrderNumber = number,
            ReportedDateTime = reportedAt.Value,
            Description = ProfileCellReader.Text(profile, row, "Description"),
            Discipline = ProfileCellReader.Text(profile, row, "Discipline"),
            RequestedByName = ProfileCellReader.Text(profile, row, "RequestedByName"),
            AssignedPersonnelName = ProfileCellReader.Text(profile, row, "AssignedPersonnelName"),
            Status = ProfileCellReader.Text(profile, row, "Status"),
            WorkType = ProfileCellReader.Text(profile, row, "WorkType"),
            FailureType = ProfileCellReader.Text(profile, row, "FailureType"),
            FailureReason = ProfileCellReader.Text(profile, row, "FailureReason"),
            ResponseDurationRaw = ProfileCellReader.Text(profile, row, "ResponseDurationRaw"),
            DowntimeRaw = ProfileCellReader.Text(profile, row, "DowntimeRaw"),
            MaintenanceDurationRaw = ProfileCellReader.Text(profile, row, "MaintenanceDurationRaw"),
            TotalCostRaw = ProfileCellReader.Text(profile, row, "TotalCostRaw"),
            ServiceCostRaw = ProfileCellReader.Text(profile, row, "ServiceCostRaw"),
            RawStatusCode = ProfileCellReader.Text(profile, row, "RawStatusCode"),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var assetCode = ProfileCellReader.Text(profile, row, "AssetCode");
        if (assetCode is not null)
        {
            workOrder.Asset = await dataStore.FindAssetByCodeAsync(assetCode, cancellationToken);
            workOrder.AssetId = workOrder.Asset?.Id;
        }

        var locationName = ProfileCellReader.Text(profile, row, "LocationName");
        if (locationName is not null)
        {
            var location = await dataStore.FindUniqueLocationByNameAsync(locationName, cancellationToken);
            if (location is not null)
            {
                workOrder.Location = location;
                workOrder.LocationId = location.Id;
                workOrder.Building = location.Building;
                workOrder.BuildingId = location.BuildingId;
            }
        }

        return ImportRowDecision.Success(workOrder);
    }
}
