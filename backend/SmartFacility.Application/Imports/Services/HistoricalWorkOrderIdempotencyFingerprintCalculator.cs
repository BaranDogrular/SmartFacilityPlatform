using System.Security.Cryptography;
using System.Text;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public static class HistoricalWorkOrderIdempotencyFingerprintCalculator
{
    public const string Algorithm = "historical-work-order/v1";

    private static readonly string[] FingerprintColumns = ["A", "C", "D", "E", "K", "M", "P"];

    public static string Calculate(string sourceType, RawExcelRow row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType);
        ArgumentNullException.ThrowIfNull(row);

        var canonical = new StringBuilder()
            .Append(Algorithm)
            .Append('|')
            .Append(ImportValueNormalizer.NormalizeForComparison(sourceType))
            .Append('|')
            .Append(ImportValueNormalizer.NormalizeForComparison(row.SheetName));

        foreach (var column in FingerprintColumns)
        {
            canonical
                .Append('|')
                .Append(column)
                .Append('=')
                .Append(ImportValueNormalizer.NormalizeForComparison(row.GetCell(column)?.RawValue));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}
