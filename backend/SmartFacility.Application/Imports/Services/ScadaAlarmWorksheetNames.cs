namespace SmartFacility.Application.Imports.Services;

public static class ScadaAlarmWorksheetNames
{
    public const string Yangin = "YANGIN";
    public const string Enerji = "ENERJİ";
    public const string KampusTakip = "KAMPÜS TAKİP";

    public static bool IsKampusTakip(string? worksheetName) =>
        string.Equals(
            ImportValueNormalizer.NormalizeForComparison(worksheetName),
            ImportValueNormalizer.NormalizeForComparison(KampusTakip),
            StringComparison.Ordinal);
}
