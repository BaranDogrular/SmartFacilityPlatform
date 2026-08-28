using System.Text.RegularExpressions;
using SmartFacility.Domain;

namespace SmartFacility.Application.Imports.Services;

public static partial class HistoricalInterventionQualityClassifier
{
    private static readonly HashSet<string> NoActionValues = new(StringComparer.Ordinal)
    {
        "", "0", "YOK", "YOKTUR", "NULL", "N A", "NA", "BİLGİ YOK",
        "BELİRTİLMEMİŞ", "AÇIKLAMA YOK"
    };

    private static readonly HashSet<string> GenericExactValues = new(StringComparer.Ordinal)
    {
        "YAPILDI", "İŞLEM YAPILDI", "İŞLEM YAPILMIŞTIR", "İŞ TAMAMLANDI",
        "İŞ TAMAMLANMIŞTIR", "TAMAMLANDI", "TAMAMLANMIŞTIR", "KONTROL EDİLDİ",
        "KONTROL EDİLMİŞTİR", "KONTROLLER YAPILDI", "BAKILDI", "ARIZA GİDERİLDİ",
        "SORUN GİDERİLDİ", "GİDERİLDİ", "ÇÖZÜLDÜ", "İŞLEM TAMAMLANDI",
        "İŞLEM TAMAMLANMIŞTIR", "OK"
    };

    private static readonly string[] ActionRoots =
    [
        "DEĞİŞTİR", "YENİLE", "ONAR", "TAMİR", "MONTAJ", "SÖK", "TAKIL", "TEMİZ",
        "AYAR", "RESET", "DEVREYE", "ENERJİ VER", "ENERJİ KES", "BAĞLA", "BAĞLANTI",
        "KESİLD", "ÖLÇ", "TEST", "KONTROL", "BAKIM", "KAYNAK", "BOYA", "DOLUM",
        "TAHLİYE", "AÇIL", "KAPAT", "DÜZELT", "PROGRAM", "KONFİG", "SIKIL", "YAĞLA",
        "FİLTRE", "SİGORTA", "KAÇAK", "TESPİT", "GİDERİLD", "ÇÖZÜLD", "ÇALIŞTIR",
        "KALİBR", "İZOL", "DEMONTE", "SERVİS", "REFAKAT"
    ];

    public static HistoricalInterventionQuality Classify(string? action)
    {
        var normalized = HistoricalInterventionTextNormalizer.NormalizeForClassification(action);
        if (normalized is null
            || NoActionValues.Contains(normalized)
            || OnlyNumbersRegex().IsMatch(normalized))
        {
            return HistoricalInterventionQuality.NoAction;
        }

        if (GenericExactValues.Contains(normalized))
        {
            return HistoricalInterventionQuality.Generic;
        }

        var wordCount = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var containsConcreteAction = ActionRoots.Any(root =>
            normalized.Contains(root, StringComparison.Ordinal));

        return containsConcreteAction && normalized.Length >= 20 && wordCount >= 4
            ? HistoricalInterventionQuality.Informative
            : HistoricalInterventionQuality.Generic;
    }

    [GeneratedRegex(@"^[\d\s]+$")]
    private static partial Regex OnlyNumbersRegex();
}
