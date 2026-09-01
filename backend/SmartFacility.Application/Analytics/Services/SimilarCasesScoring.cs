using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartFacility.Application.Analytics.Services;

public sealed record SimilarCaseSignals(
    string TargetDescription,
    string CandidateDescription,
    bool SameAsset,
    bool SameDiscipline,
    bool SameAssetGroup,
    bool SameWorkType,
    bool SameFailureType,
    bool SameFailureReason,
    int NormalizedTemplateFrequency);

public sealed record SimilarCaseScoreResult(
    decimal Score,
    decimal TextSimilarity,
    string NormalizedDescription,
    int TokenCount,
    decimal TemplatePenalty,
    IReadOnlyList<string> Reasons);

public static partial class SimilarCasesScoring
{
    public const string AlgorithmVersion = "similar-cases/hybrid-jaccard/v1";
    public const int MinimumTokenCount = 2;
    public const decimal MinimumTextSimilarity = 25m;
    public const decimal MinimumHybridScore = 40m;
    public const int SnippetLength = 200;
    public const string PrivacyRuleVersion = "privacy-redaction/email-turkish-mobile/v1";

    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static SimilarCaseScoreResult Calculate(SimilarCaseSignals signals)
    {
        var targetTokens = Tokenize(signals.TargetDescription);
        var candidateTokens = Tokenize(signals.CandidateDescription);
        var normalizedCandidate = NormalizeText(signals.CandidateDescription);
        var textSimilarity = CalculateJaccard(targetTokens, candidateTokens);
        var templatePenalty = CalculateTemplatePenalty(signals.NormalizedTemplateFrequency);

        var score = (textSimilarity * 0.70m)
            + (signals.SameAsset ? 10m : 0m)
            + (signals.SameDiscipline ? 8m : 0m)
            + (signals.SameAssetGroup ? 4m : 0m)
            + (signals.SameWorkType ? 4m : 0m)
            + (signals.SameFailureType ? 2m : 0m)
            + (signals.SameFailureReason ? 2m : 0m)
            - templatePenalty;
        score = Math.Round(Math.Clamp(score, 0m, 100m), 2, MidpointRounding.AwayFromZero);

        var reasons = new List<string>();
        if (textSimilarity >= MinimumTextSimilarity)
        {
            reasons.Add($"Benzer açıklama (%{textSimilarity:0.##})");
        }

        if (signals.SameAsset)
        {
            reasons.Add("Aynı varlık");
        }
        else if (signals.SameAssetGroup)
        {
            reasons.Add("Aynı varlık grubu");
        }

        if (signals.SameDiscipline)
        {
            reasons.Add("Aynı disiplin");
        }

        if (signals.SameWorkType)
        {
            reasons.Add("Aynı iş tipi");
        }

        if (signals.SameFailureType)
        {
            reasons.Add("Aynı bakım/arıza sınıfı");
        }

        if (signals.SameFailureReason)
        {
            reasons.Add("Aynı arıza nedeni sınıflandırması");
        }

        return new SimilarCaseScoreResult(
            score,
            textSimilarity,
            normalizedCandidate,
            candidateTokens.Count,
            templatePenalty,
            reasons);
    }

    public static bool IsEligible(SimilarCaseScoreResult result) =>
        result.TokenCount >= MinimumTokenCount
        && result.TextSimilarity >= MinimumTextSimilarity
        && result.Score >= MinimumHybridScore;

    public static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormKC).ToUpper(TurkishCulture);
        var builder = new StringBuilder(normalized.Length);
        var pendingSpace = false;

        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(character);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    public static IReadOnlySet<string> Tokenize(string? value) =>
        NormalizeText(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 2)
            .ToHashSet(StringComparer.Ordinal);

    public static string CreatePrivacySafeSnippet(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = WebUtility.HtmlDecode(value);
        text = HtmlTagRegex().Replace(text, " ");
        text = EmailRegex().Replace(text, "[e-posta gizlendi]");
        text = TurkishMobileRegex().Replace(text, "[telefon gizlendi]");
        text = WhitespaceRegex().Replace(text, " ").Trim();

        if (text.Length <= SnippetLength)
        {
            return text;
        }

        var cut = text.LastIndexOf(' ', SnippetLength - 1, SnippetLength);
        if (cut < SnippetLength / 2)
        {
            cut = SnippetLength;
        }

        return $"{text[..cut].TrimEnd()}…";
    }

    private static decimal CalculateJaccard(
        IReadOnlySet<string> first,
        IReadOnlySet<string> second)
    {
        if (first.Count == 0 || second.Count == 0)
        {
            return 0m;
        }

        var intersection = first.Count(second.Contains);
        var union = first.Count + second.Count - intersection;
        return union == 0
            ? 0m
            : Math.Round(100m * intersection / union, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateTemplatePenalty(int frequency)
    {
        if (frequency <= 1)
        {
            return 0m;
        }

        return Math.Round(
            Math.Min(20m, 5m * (decimal)Math.Log2(frequency)),
            2,
            MidpointRounding.AwayFromZero);
    }

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(
        @"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(
        @"(?<!\d)(?:(?:\+|00)90[\s().-]*|0)5\d{2}[\s().-]*\d{3}[\s().-]*\d{2}[\s().-]*\d{2}(?!\d)",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex TurkishMobileRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex WhitespaceRegex();
}
