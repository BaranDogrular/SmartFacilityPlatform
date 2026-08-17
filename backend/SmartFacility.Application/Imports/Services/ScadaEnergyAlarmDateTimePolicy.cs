using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public static class ScadaEnergyAlarmDateTimePolicy
{
    public const int MaximumStatusLength = 100;

    public static ScadaAlarmDateTimeEvaluation Evaluate(
        RawExcelRow row,
        IImportSourceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(profile);

        var worksheet = profile.GetWorksheet(row.SheetName);
        if (worksheet.ReferenceDate is null)
        {
            throw new InvalidOperationException(
                $"Worksheet '{row.SheetName}' requires a reference date for ENERJİ date validation.");
        }

        var received = EvaluateTimestamp(
            profile.GetCell(row, "ReceivedDate"),
            profile.GetCell(row, "ReceivedTime"),
            recognizePlaceholderX: false);
        var cleared = EvaluateTimestamp(
            profile.GetCell(row, "ClearedDate"),
            profile.GetCell(row, "ClearedTime"),
            recognizePlaceholderX: true);
        var flags = new List<string>();
        var referenceDate = worksheet.ReferenceDate.Value.Date;

        if (received.Value?.Date > referenceDate || cleared.Value?.Date > referenceDate)
        {
            flags.Add("FutureDate");
        }

        if (received.Value is { } receivedAt &&
            cleared.Value is { } clearedAt &&
            clearedAt < receivedAt)
        {
            flags.Add("ClearedBeforeReceived");
        }

        var status = BuildStatus(received.Status, cleared.Status, flags);
        return new ScadaAlarmDateTimeEvaluation(received, cleared, flags, status);
    }

    public static ParsedDateTime EvaluateTimestamp(
        RawExcelCell? dateCell,
        RawExcelCell? timeCell,
        bool recognizePlaceholderX)
    {
        if (recognizePlaceholderX &&
            (IsPlaceholderX(dateCell) || IsPlaceholderX(timeCell)))
        {
            return new ParsedDateTime(null, "PlaceholderX");
        }

        var parsed = ExcelValueParser.CombineDateAndTime(dateCell, timeCell);
        if (parsed.Value is { Year: < 1900 })
        {
            return new ParsedDateTime(null, "InvalidDate");
        }

        return parsed.Status == "ParsedDateOnly"
            ? new ParsedDateTime(null, "DateOnlySource")
            : parsed;
    }

    private static bool IsPlaceholderX(RawExcelCell? cell) =>
        string.Equals(
            ImportValueNormalizer.NormalizeForComparison(cell?.RawValue),
            "X",
            StringComparison.Ordinal);

    private static string BuildStatus(
        string receivedStatus,
        string clearedStatus,
        IReadOnlyCollection<string> flags)
    {
        var status = $"Received:{receivedStatus};Cleared:{clearedStatus}";
        if (flags.Count > 0)
        {
            status += $";Flags:{string.Join(',', flags)}";
        }

        if (status.Length > MaximumStatusLength)
        {
            throw new InvalidOperationException(
                $"SCADA alarm date status exceeds {MaximumStatusLength} characters.");
        }

        return status;
    }
}
