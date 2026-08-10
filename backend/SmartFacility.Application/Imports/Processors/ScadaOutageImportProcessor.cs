using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Imports.Processors;

public sealed class ScadaOutageImportProcessor : IImportRowProcessor
{
    public string ProfileKey => ImportProfileKeys.ScadaOutage;

    public Task<ImportRowDecision> ProcessAsync(
        RawExcelRow row,
        IImportSourceProfile profile,
        CancellationToken cancellationToken)
    {
        var reason = ProfileCellReader.Text(profile, row, "Reason");
        var description = ProfileCellReader.Text(profile, row, "Description");
        var status = ProfileCellReader.Text(profile, row, "StatusRaw");
        var started = ExcelValueParser.CombineDateAndTime(
            profile.GetCell(row, "StartedDate"),
            profile.GetCell(row, "StartedTime"));
        var restored = ExcelValueParser.CombineDateAndTime(
            profile.GetCell(row, "RestoredDate"),
            profile.GetCell(row, "RestoredTime"));

        if (reason is null && description is null && status is null &&
            started.Status == "Missing" && restored.Status == "Missing")
        {
            return Task.FromResult(ImportRowDecision.Ignore());
        }

        var entity = new ScadaOutage
        {
            SourceSheet = row.SheetName,
            Reason = reason,
            Description = description,
            StartedAt = started.Value,
            RestoredAt = restored.Value,
            DurationRaw = ProfileCellReader.Text(profile, row, "DurationRaw"),
            StatusRaw = status,
            DateTimeParseStatus = $"Started:{started.Status};Restored:{restored.Status}",
            CreatedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(ImportRowDecision.Success(entity));
    }
}
