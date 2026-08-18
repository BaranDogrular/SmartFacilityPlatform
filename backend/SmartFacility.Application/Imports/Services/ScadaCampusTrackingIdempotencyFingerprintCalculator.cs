using System.Security.Cryptography;
using System.Text;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public static class ScadaCampusTrackingIdempotencyFingerprintCalculator
{
    public const string Algorithm = "scada-campus-tracking/v1";

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
            .Append("|SECTION=").Append(ScadaCampusTrackingCanonicalizer.Text(row, "A"))
            .Append("|LOCATION=").Append(ScadaCampusTrackingCanonicalizer.Text(row, "B"))
            .Append("|FLOOR=").Append(ScadaCampusTrackingCanonicalizer.Text(row, "C"))
            .Append("|ALARM_TYPE=").Append(ScadaCampusTrackingCanonicalizer.Text(row, "D"))
            .Append("|INTERVENTION_LEVEL=").Append(ScadaCampusTrackingCanonicalizer.Text(row, "E"))
            .Append("|ZONE=").Append(ScadaCampusTrackingCanonicalizer.Text(row, "F"))
            .Append("|DESCRIPTION=").Append(ScadaCampusTrackingCanonicalizer.Text(row, "G"))
            .Append("|RECEIVED_AT=").Append(ScadaCampusTrackingCanonicalizer.Timestamp(
                row.GetCell("H"), row.GetCell("I")))
            .Append("|CLEARED_AT=").Append(ScadaCampusTrackingCanonicalizer.Timestamp(
                row.GetCell("J"), row.GetCell("K")))
            .Append("|RESPONSIBLE=").Append(ScadaCampusTrackingCanonicalizer.Text(row, "L"))
            .Append("|STATUS=").Append(ScadaCampusTrackingCanonicalizer.Text(row, "M"))
            .Append("|NOTE=").Append(ScadaCampusTrackingCanonicalizer.Text(row, "N"));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}
