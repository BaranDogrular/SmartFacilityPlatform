using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Services;

namespace SmartFacility.Application.Tests;

public sealed class HistoricalInterventionFingerprintTests
{
    [Fact]
    public void Fingerprint_is_versioned_deterministic_and_not_tied_to_file_or_row_location()
    {
        var first = Row("first.xls", 2, "Filtre değiştirildi");
        var relocated = Row("second.xls", 999, " Filtre   değiştirildi ");

        var firstFingerprint = HistoricalInterventionFingerprintCalculator.Calculate(first, "IDENTITY");
        var secondFingerprint = HistoricalInterventionFingerprintCalculator.Calculate(
            relocated,
            "IDENTITY");

        Assert.Equal("historical-intervention/v1", HistoricalInterventionFingerprintCalculator.Algorithm);
        Assert.Equal(firstFingerprint, secondFingerprint);
        Assert.Equal(64, firstFingerprint.Length);
    }

    [Fact]
    public void Material_business_change_changes_fingerprint()
    {
        var first = HistoricalInterventionFingerprintCalculator.Calculate(
            Row("source.xls", 2, "Filtre değiştirildi"),
            "IDENTITY");
        var changed = HistoricalInterventionFingerprintCalculator.Calculate(
            Row("source.xls", 2, "Kontaktör değiştirildi"),
            "IDENTITY");

        Assert.NotEqual(first, changed);
    }

    private static HistoricalInterventionSourceRow Row(
        string fileName,
        int rowNumber,
        string action) =>
        new(
            fileName,
            fileName,
            "HASH",
            "Varlık Tarihçesi",
            rowNumber,
            2026,
            "WO-1",
            new DateTime(2026, 1, 1, 8, 0, 0),
            "ASSET-1",
            "K",
            "Asset",
            new DateTime(2026, 1, 1, 9, 0, 0),
            "Problem",
            action,
            "R1",
            "Reason",
            "1",
            "0",
            "1",
            "10",
            "20",
            "30",
            "TRY 30");
}
