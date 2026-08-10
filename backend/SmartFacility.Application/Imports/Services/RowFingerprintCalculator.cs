using System.Security.Cryptography;
using System.Text;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public static class RowFingerprintCalculator
{
    public static string Calculate(string sourceType, RawExcelRow row)
    {
        var canonical = new StringBuilder()
            .Append(ImportValueNormalizer.NormalizeForComparison(sourceType))
            .Append('|')
            .Append(ImportValueNormalizer.NormalizeForComparison(row.SheetName));

        foreach (var cell in row.Cells.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            canonical
                .Append('|')
                .Append(cell.Key.ToUpperInvariant())
                .Append('=')
                .Append(ImportValueNormalizer.NormalizeForComparison(cell.Value.RawValue));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}
