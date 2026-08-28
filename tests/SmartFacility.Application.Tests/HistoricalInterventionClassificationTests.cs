using SmartFacility.Application.Imports.Services;
using SmartFacility.Domain;

namespace SmartFacility.Application.Tests;

public sealed class HistoricalInterventionClassificationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("-")]
    [InlineData("YOK")]
    [InlineData("...")]
    public void Missing_or_placeholder_action_is_no_action(string? value)
    {
        Assert.Equal(
            HistoricalInterventionQuality.NoAction,
            HistoricalInterventionQualityClassifier.Classify(value));
    }

    [Theory]
    [InlineData("KONTROLLER SAĞLANMIŞTIR.")]
    [InlineData("ÇALIŞMA TAMAMLANDI")]
    [InlineData("ARIZA GİDERİLDİ")]
    public void Actual_low_information_templates_are_generic(string value)
    {
        Assert.Equal(
            HistoricalInterventionQuality.Generic,
            HistoricalInterventionQualityClassifier.Classify(value));
    }

    [Fact]
    public void Concrete_multi_word_action_is_informative()
    {
        Assert.Equal(
            HistoricalInterventionQuality.Informative,
            HistoricalInterventionQualityClassifier.Classify(
                "Arızalı kontaktör değiştirilerek bağlantıları kontrol edildi."));
    }

    [Fact]
    public void Redactor_removes_email_and_turkish_mobile_without_removing_business_text()
    {
        var result = HistoricalInterventionPrivacyRedactor.Redact(
            "Kontrol edildi; test@example.com ve +90 532 123 45 67 ile görüşüldü.");

        Assert.NotNull(result);
        Assert.DoesNotContain("test@example.com", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("532 123 45 67", result, StringComparison.Ordinal);
        Assert.Contains("[REDACTED_EMAIL]", result, StringComparison.Ordinal);
        Assert.Contains("[REDACTED_PHONE]", result, StringComparison.Ordinal);
        Assert.Contains("Kontrol edildi", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("ARIZA", false)]
    [InlineData("Pompa basınç üretmiyor", true)]
    public void Context_usability_is_conservative_and_explainable(string? value, bool expected)
    {
        Assert.Equal(expected, HistoricalInterventionContextClassifier.IsUsable(value));
    }
}
