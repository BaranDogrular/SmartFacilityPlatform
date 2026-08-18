using System.Globalization;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

internal static class ScadaCampusTrackingCanonicalizer
{
    public static string Timestamp(RawExcelCell? dateCell, RawExcelCell? timeCell)
    {
        var parsed = ScadaCampusTrackingDateTimePolicy.EvaluateTimestamp(dateCell, timeCell);

        return parsed.Value is { } value
            ? $"{parsed.Status.ToUpperInvariant()}:{RoundToMillisecond(value).ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture)}"
            : $"{parsed.Status.ToUpperInvariant()}:DATE={DateFallback(dateCell)};TIME={ImportValueNormalizer.NormalizeForComparison(timeCell?.RawValue)}";
    }

    public static string Text(RawExcelRow row, string column) =>
        ImportValueNormalizer.NormalizeForComparison(row.GetCell(column)?.RawValue) ?? string.Empty;

    private static string? DateFallback(RawExcelCell? cell)
    {
        var parsedDate = ExcelValueParser.ParseDate(cell);
        return parsedDate.Value is { Year: >= 1900 } value
            ? value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : ImportValueNormalizer.NormalizeForComparison(cell?.RawValue);
    }

    private static DateTime RoundToMillisecond(DateTime value)
    {
        var remainder = value.Ticks % TimeSpan.TicksPerMillisecond;
        var roundedTicks = value.Ticks - remainder;
        if (remainder >= TimeSpan.TicksPerMillisecond / 2 &&
            roundedTicks <= DateTime.MaxValue.Ticks - TimeSpan.TicksPerMillisecond)
        {
            roundedTicks += TimeSpan.TicksPerMillisecond;
        }

        return new DateTime(roundedTicks, DateTimeKind.Unspecified);
    }
}
