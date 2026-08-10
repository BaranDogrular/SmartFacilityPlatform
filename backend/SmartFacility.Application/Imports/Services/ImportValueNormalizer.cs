using System.Text.RegularExpressions;

namespace SmartFacility.Application.Imports.Services;

public static partial class ImportValueNormalizer
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return WhitespaceRegex().Replace(value.Trim(), " ");
    }

    public static string? NormalizeForComparison(string? value) =>
        Normalize(value)?.ToUpperInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
