using System.Globalization;
using System.Text.RegularExpressions;

namespace SmartFacility.Application.Imports.Services;

public static partial class HistoricalInterventionTextNormalizer
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static string? NormalizeOriginal(string? value) => ImportValueNormalizer.Normalize(value);

    public static string? NormalizeForClassification(string? value)
    {
        var normalized = NormalizeOriginal(value);
        if (normalized is null)
        {
            return null;
        }

        normalized = PunctuationRegex().Replace(normalized, " ");
        normalized = WhitespaceRegex().Replace(normalized, " ").Trim();
        return normalized.ToUpper(TurkishCulture);
    }

    public static string? NormalizeForFingerprint(string? value) =>
        NormalizeOriginal(value)?.ToUpper(TurkishCulture);

    [GeneratedRegex(@"[\p{P}\p{S}]+")]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
