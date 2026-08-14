using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Imports.Processors;

public sealed class ScadaAlarmImportProcessor : IImportRowProcessor
{
    public string ProfileKey => ImportProfileKeys.ScadaAlarm;

    public Task<ImportRowDecision> ProcessAsync(
        RawExcelRow row,
        IImportSourceProfile profile,
        CancellationToken cancellationToken)
    {
        var description = ProfileCellReader.Text(profile, row, "Description");
        var alarmType = ProfileCellReader.Text(profile, row, "AlarmType");
        var status = ProfileCellReader.Text(profile, row, "StatusRaw");
        var received = ExcelValueParser.CombineDateAndTime(
            profile.GetCell(row, "ReceivedDate"),
            profile.GetCell(row, "ReceivedTime"));
        var cleared = ApplyClearedAtPolicy(ExcelValueParser.CombineDateAndTime(
            profile.GetCell(row, "ClearedDate"),
            profile.GetCell(row, "ClearedTime")));

        if (description is null && alarmType is null && status is null &&
            received.Status == "Missing" && cleared.Status == "Missing")
        {
            return Task.FromResult(ImportRowDecision.Ignore());
        }

        var entity = new ScadaAlarmEvent
        {
            SourceSheet = row.SheetName,
            SectionRaw = ProfileCellReader.Text(profile, row, "SectionRaw"),
            LocationRaw = ProfileCellReader.Text(profile, row, "LocationRaw"),
            FloorRaw = ProfileCellReader.Text(profile, row, "FloorRaw"),
            ZoneRaw = ProfileCellReader.Text(profile, row, "ZoneRaw"),
            AlarmType = alarmType,
            InterventionLevel = ProfileCellReader.Text(profile, row, "InterventionLevel"),
            Description = description,
            ReceivedAt = received.Value,
            ClearedAt = cleared.Value,
            ResponsibleRaw = ProfileCellReader.Text(profile, row, "ResponsibleRaw"),
            StatusRaw = status,
            Note = ProfileCellReader.Text(profile, row, "Note"),
            DateTimeParseStatus = $"Received:{received.Status};Cleared:{cleared.Status}",
            CreatedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(ImportRowDecision.Success(entity));
    }

    private static ParsedDateTime ApplyClearedAtPolicy(ParsedDateTime parsed) =>
        parsed.Status == "ParsedDateOnly"
            ? new ParsedDateTime(null, "DateOnlySource")
            : parsed;
}
