using System.Text.RegularExpressions;

namespace SmartFacility.Application.Imports.Services;

public static partial class HistoricalInterventionPrivacyRedactor
{
    public static string? Redact(string? value)
    {
        var normalized = HistoricalInterventionTextNormalizer.NormalizeOriginal(value);
        if (normalized is null)
        {
            return null;
        }

        var redacted = EmailRegex().Replace(normalized, "[REDACTED_EMAIL]");
        return TurkishMobileRegex().Replace(redacted, "[REDACTED_PHONE]");
    }

    [GeneratedRegex(
        @"(?<![\w.])[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}(?!\w)",
        RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(
        @"(?<!\d)(?:(?:\+|00)?90[\s.\-]?)?0?\s*\(?5\d{2}\)?(?:[\s.\-]?\d){7}(?!\d)")]
    private static partial Regex TurkishMobileRegex();
}
