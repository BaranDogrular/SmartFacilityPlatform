using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public static class ScadaFireAlarmIdempotencyFingerprintCalculator
{
    public const string Algorithm = "scada-fire-alarm/v1";

    public static string Calculate(string sourceType, RawExcelRow row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType);
        ArgumentNullException.ThrowIfNull(row);

        var canonical = new StringBuilder()
            .Append(Algorithm)
            .Append('|')
            .Append(ImportValueNormalizer.NormalizeForComparison(sourceType))
            .Append('|')
            .Append(ImportValueNormalizer.NormalizeForComparison(row.SheetName))
            .Append("|RECEIVED_AT=")
            .Append(GetReceivedAtToken(row))
            .Append("|LOCATION=")
            .Append(ImportValueNormalizer.NormalizeForComparison(row.GetCell("B")?.RawValue))
            .Append("|FLOOR=")
            .Append(ImportValueNormalizer.NormalizeForComparison(row.GetCell("C")?.RawValue))
            .Append("|ALARM_TYPE=")
            .Append(ImportValueNormalizer.NormalizeForComparison(row.GetCell("D")?.RawValue))
            .Append("|DESCRIPTION=")
            .Append(ImportValueNormalizer.NormalizeForComparison(row.GetCell("G")?.RawValue));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string GetReceivedAtToken(RawExcelRow row)
    {
        var dateCell = row.GetCell("H");
        var timeCell = row.GetCell("I");
        var parsed = ScadaFireAlarmDateTimePolicy.ApplyTimestampPolicy(
            ExcelValueParser.CombineDateAndTime(dateCell, timeCell));

        return parsed.Value is { } value
            ? $"{parsed.Status.ToUpperInvariant()}:{RoundToMillisecond(value).ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture)}"
            : $"{parsed.Status.ToUpperInvariant()}:DATE={ImportValueNormalizer.NormalizeForComparison(dateCell?.RawValue)};TIME={ImportValueNormalizer.NormalizeForComparison(timeCell?.RawValue)}";
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
