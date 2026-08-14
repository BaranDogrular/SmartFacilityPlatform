using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public static class ScadaOutageIdempotencyFingerprintCalculator
{
    public const string Algorithm = "scada-outage/v1";

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
            .Append("|STARTED_AT=")
            .Append(GetStartedAtToken(row))
            .Append("|REASON=")
            .Append(ImportValueNormalizer.NormalizeForComparison(row.GetCell("B")?.RawValue))
            .Append("|DESCRIPTION=")
            .Append(ImportValueNormalizer.NormalizeForComparison(row.GetCell("C")?.RawValue));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string GetStartedAtToken(RawExcelRow row)
    {
        var dateCell = row.GetCell("D");
        var timeCell = row.GetCell("E");
        var parsed = ExcelValueParser.CombineDateAndTime(dateCell, timeCell);

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
