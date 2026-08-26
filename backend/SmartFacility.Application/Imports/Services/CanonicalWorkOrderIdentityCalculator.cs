using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public static class CanonicalWorkOrderIdentityCalculator
{
    public const string Algorithm = "work-order/canonical-identity/v1";

    public static string Calculate(
        string workOrderNumber,
        DateTime reportedDateTime,
        string assetCode)
    {
        var canonical = string.Join('|',
            Algorithm,
            ImportValueNormalizer.NormalizeForComparison(workOrderNumber),
            reportedDateTime.ToString("O", CultureInfo.InvariantCulture),
            ImportValueNormalizer.NormalizeForComparison(assetCode));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static string Calculate(RawExcelRow row)
    {
        var workOrderNumber = ImportValueNormalizer.Normalize(row.GetCell("D")?.RawValue)
            ?? throw new InvalidOperationException("WorkOrderNumber is required for canonical identity.");
        var assetCode = ImportValueNormalizer.Normalize(row.GetCell("G")?.RawValue)
            ?? throw new InvalidOperationException("AssetCode is required for canonical identity.");
        var reportedAt = ExcelValueParser.CombineDateAndTime(row.GetCell("E"), row.GetCell("F"));
        if (reportedAt.Value is null)
        {
            throw new InvalidOperationException(
                "A parseable ReportedDateTime is required for canonical identity.");
        }

        return Calculate(workOrderNumber, reportedAt.Value.Value, assetCode);
    }
}
