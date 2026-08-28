using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public static class HistoricalInterventionFingerprintCalculator
{
    public const string Algorithm = "historical-intervention/v1";

    public static string Calculate(
        HistoricalInterventionSourceRow row,
        string canonicalIdentityFingerprint)
    {
        var values = new[]
        {
            Algorithm,
            canonicalIdentityFingerprint,
            row.SourceYear.ToString(CultureInfo.InvariantCulture),
            Normalize(row.WorkOrderNumber),
            row.ReportedDateTime.ToString("O", CultureInfo.InvariantCulture),
            Normalize(row.AssetCode),
            Normalize(row.WorkOrderStatus),
            Normalize(row.AssetName),
            row.CompletionDateTime?.ToString("O", CultureInfo.InvariantCulture),
            Normalize(row.RequestDescription),
            Normalize(row.WorkPerformedDescription),
            Normalize(row.FailureReasonCode),
            Normalize(row.FailureReasonDescription),
            Normalize(row.MaintenanceDurationRaw),
            Normalize(row.DowntimeDurationRaw),
            Normalize(row.LaborDurationRaw),
            Normalize(row.MaterialCostRaw),
            Normalize(row.LaborCostRaw),
            Normalize(row.TotalCostRaw),
            Normalize(row.TotalCostCurrencyRaw)
        };
        var canonical = string.Concat(values.Select(value =>
            $"{value?.Length ?? -1}:{value ?? string.Empty}|"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string? Normalize(string? value) =>
        HistoricalInterventionTextNormalizer.NormalizeForFingerprint(value);
}
