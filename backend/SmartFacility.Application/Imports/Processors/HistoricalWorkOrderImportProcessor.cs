using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Imports.Processors;

public sealed class HistoricalWorkOrderImportProcessor : IImportRowProcessor
{
    public string ProfileKey => ImportProfileKeys.HistoricalWorkOrder;

    public Task<ImportRowDecision> ProcessAsync(
        RawExcelRow row,
        IImportSourceProfile profile,
        CancellationToken cancellationToken)
    {
        var sourceReference = ProfileCellReader.Text(profile, row, "SourceReference");
        var description = ProfileCellReader.Text(profile, row, "Description");
        var discipline = ProfileCellReader.Text(profile, row, "Discipline");
        var reportedAt = ExcelValueParser.ParseDate(profile.GetCell(row, "ReportedDateTime"));

        if (sourceReference is null && description is null && discipline is null && reportedAt.Value is null)
        {
            return Task.FromResult(ImportRowDecision.Ignore());
        }

        var entity = new HistoricalWorkOrder
        {
            SourceReference = sourceReference,
            ReportedDateTime = reportedAt.Value,
            Description = description,
            Discipline = discipline,
            PersonnelName = ProfileCellReader.Text(profile, row, "PersonnelName"),
            BuildingNameRaw = ProfileCellReader.Text(profile, row, "BuildingNameRaw"),
            LocationNameRaw = ProfileCellReader.Text(profile, row, "LocationNameRaw"),
            ResolutionDurationRaw = ProfileCellReader.Text(profile, row, "ResolutionDurationRaw"),
            RawData = RawRowSerializer.SerializeValues(row),
            CreatedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(ImportRowDecision.Success(entity));
    }
}
