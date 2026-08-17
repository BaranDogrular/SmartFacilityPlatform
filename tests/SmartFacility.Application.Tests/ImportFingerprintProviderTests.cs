using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Tests;

public sealed class ImportFingerprintProviderTests
{
    [Theory]
    [InlineData(ImportSourceTypes.Asset)]
    [InlineData(ImportSourceTypes.WorkOrder)]
    [InlineData(ImportSourceTypes.ScadaAlarm)]
    public async Task Legacy_sources_keep_RowFingerprint_for_duplicate_detection(string sourceType)
    {
        var row = RawRowFactory.Row("Data", 2, RawRowFactory.Text("A", "Value"));
        var provider = new ImportFingerprintProvider();
        var fingerprints = provider.Calculate(sourceType, row);

        Assert.Equal(RowFingerprintCalculator.Calculate(sourceType, row), fingerprints.RowFingerprint);
        Assert.Equal(fingerprints.RowFingerprint, fingerprints.DuplicateFingerprint);
        Assert.Null(fingerprints.IdempotencyFingerprint);
        Assert.Null(fingerprints.FingerprintAlgorithm);
        Assert.Null(provider.GetIdempotencyAlgorithm(sourceType, row.SheetName));

        await using var database = await SqliteTestDatabase.CreateAsync();
        var batch = new ImportBatch
        {
            SourceType = sourceType,
            FileName = "legacy.xlsx",
            Status = "Completed",
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            TotalRows = 1,
            SuccessfulRows = 1
        };
        database.Context.ImportBatches.Add(batch);
        await database.Context.SaveChangesAsync();
        database.Context.ImportSourceRecords.Add(new ImportSourceRecord
        {
            ImportBatchId = batch.Id,
            SourceSheet = "Data",
            SourceRowNumber = 2,
            RowFingerprint = fingerprints.RowFingerprint,
            RawData = "{}",
            ParseStatus = "Succeeded",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var known = await database.Store.GetSuccessfulFingerprintsAsync(
            sourceType,
            ["Data"],
            fingerprintAlgorithm: null,
            CancellationToken.None);

        Assert.Contains(fingerprints.RowFingerprint, known);
    }

    [Fact]
    public void ScadaOutage_uses_versioned_idempotency_fingerprint_and_keeps_legacy_fingerprint()
    {
        var row = RawRowFactory.Row(
            "SCADA SÜREKLİLİK",
            2,
            RawRowFactory.Text("B", "Power interruption"),
            RawRowFactory.Text("C", "Main feed"),
            RawRowFactory.DateTimeCell("D", new DateTime(2026, 8, 1)),
            RawRowFactory.TimeCell("E", new TimeSpan(10, 30, 0)));
        var provider = new ImportFingerprintProvider();

        var fingerprints = provider.Calculate(ImportSourceTypes.ScadaOutage, row);

        Assert.Equal(
            RowFingerprintCalculator.Calculate(ImportSourceTypes.ScadaOutage, row),
            fingerprints.RowFingerprint);
        Assert.Equal(
            ScadaOutageIdempotencyFingerprintCalculator.Calculate(
                ImportSourceTypes.ScadaOutage,
                row),
            fingerprints.IdempotencyFingerprint);
        Assert.Equal(
            ScadaOutageIdempotencyFingerprintCalculator.Algorithm,
            fingerprints.FingerprintAlgorithm);
        Assert.Equal(fingerprints.IdempotencyFingerprint, fingerprints.DuplicateFingerprint);
        Assert.Equal(
            ScadaOutageIdempotencyFingerprintCalculator.Algorithm,
            provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaOutage, row.SheetName));
    }
}
