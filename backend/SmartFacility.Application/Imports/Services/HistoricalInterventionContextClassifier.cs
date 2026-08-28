namespace SmartFacility.Application.Imports.Services;

public static class HistoricalInterventionContextClassifier
{
    private static readonly HashSet<string> PlaceholderValues = new(StringComparer.Ordinal)
    {
        "ARIZA", "BAKIM", "KONTROL", "GENEL KONTROL", "İŞ EMRİ", "DİĞER", "YOK"
    };

    public static bool IsUsable(string? value)
    {
        var normalized = HistoricalInterventionTextNormalizer.NormalizeForClassification(value);
        if (normalized is null || PlaceholderValues.Contains(normalized))
        {
            return false;
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return normalized.Length >= 10 && words.Length >= 2;
    }
}
