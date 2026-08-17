using System.Security.Cryptography;
using System.Text;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public static class ScadaEnergyAlarmIdempotencyFingerprintCalculator
{
    public const string Algorithm = "scada-energy-alarm/v1";

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
            .Append("|SECTION=").Append(ScadaEnergyAlarmCanonicalizer.Text(row, "A"))
            .Append("|LOCATION=").Append(ScadaEnergyAlarmCanonicalizer.Text(row, "B"))
            .Append("|FLOOR=").Append(ScadaEnergyAlarmCanonicalizer.Text(row, "C"))
            .Append("|ALARM_TYPE=").Append(ScadaEnergyAlarmCanonicalizer.Text(row, "D"))
            .Append("|INTERVENTION_LEVEL=").Append(ScadaEnergyAlarmCanonicalizer.Text(row, "E"))
            .Append("|ZONE=").Append(ScadaEnergyAlarmCanonicalizer.Text(row, "F"))
            .Append("|DESCRIPTION=").Append(ScadaEnergyAlarmCanonicalizer.Text(row, "G"))
            .Append("|RECEIVED_AT=").Append(ScadaEnergyAlarmCanonicalizer.Timestamp(
                row.GetCell("H"), row.GetCell("I"), recognizePlaceholderX: false))
            .Append("|CLEARED_AT=").Append(ScadaEnergyAlarmCanonicalizer.Timestamp(
                row.GetCell("J"), row.GetCell("K"), recognizePlaceholderX: true))
            .Append("|RESPONSIBLE=").Append(ScadaEnergyAlarmCanonicalizer.Text(row, "L"))
            .Append("|STATUS=").Append(ScadaEnergyAlarmCanonicalizer.Text(row, "M"))
            .Append("|NOTE=").Append(ScadaEnergyAlarmCanonicalizer.Text(row, "N"));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}
