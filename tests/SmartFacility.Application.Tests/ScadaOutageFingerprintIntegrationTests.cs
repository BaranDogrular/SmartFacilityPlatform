using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Tests;

public sealed class ScadaOutageFingerprintIntegrationTests
{
    [Fact]
    public async Task Backfilled_pilot_lineage_prevents_duration_representation_duplicate()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var profile = TestProfiles.ScadaOutage();
        var sheetName = profile.Worksheets.Single().Name;
        var pilot = CreateDataRow(sheetName, RawRowFactory.Number("I", 0.5));
        var original = CreateDataRow(sheetName, RawRowFactory.TimeCell("I", TimeSpan.FromHours(12)));
        var fingerprints = new ImportFingerprintProvider().Calculate(
            ImportSourceTypes.ScadaOutage,
            pilot);
        var batch = new ImportBatch
        {
            SourceType = ImportSourceTypes.ScadaOutage,
            FileName = "legacy-pilot.xlsx",
            Status = "Completed",
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            TotalRows = 1,
            SuccessfulRows = 1
        };
        database.Context.ImportBatches.Add(batch);
        database.Context.ScadaOutages.Add(new ScadaOutage
        {
            SourceSheet = sheetName,
            Reason = "Power interruption",
            Description = "Main feed",
            StartedAt = new DateTime(2026, 8, 1, 10, 0, 0),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();
        database.Context.ImportSourceRecords.Add(new ImportSourceRecord
        {
            ImportBatchId = batch.Id,
            SourceSheet = sheetName,
            SourceRowNumber = pilot.RowNumber,
            RowFingerprint = fingerprints.RowFingerprint,
            IdempotencyFingerprint = fingerprints.IdempotencyFingerprint,
            FingerprintAlgorithm = fingerprints.FingerprintAlgorithm,
            RawData = RawRowSerializer.SerializeValues(pilot),
            ParseStatus = "Succeeded",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var service = new ExcelImportService(
            new FakeWorkbookReader(
            [
                RawRowFactory.Row(sheetName, 1, RawRowFactory.Text("B", "Reason")),
                original
            ]),
            database.Store,
            new ImportProfileCatalog([profile]),
            new ImportFingerprintProvider(),
            [new ScadaOutageImportProcessor()],
            NullLogger<ExcelImportService>.Instance);

        var result = await service.ImportAsync(
            new ImportRequest(ImportProfileKeys.ScadaOutage, "original.xlsx"));

        Assert.Equal(1, result.DuplicateRows);
        Assert.Equal(0, result.SuccessfulRows);
        Assert.Equal(1, await database.Context.ScadaOutages.CountAsync());
        var records = await database.Context.ImportSourceRecords.OrderBy(record => record.Id).ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.NotEqual(records[0].RowFingerprint, records[1].RowFingerprint);
        Assert.Equal(records[0].IdempotencyFingerprint, records[1].IdempotencyFingerprint);
        Assert.All(records, record => Assert.Equal(
            ScadaOutageIdempotencyFingerprintCalculator.Algorithm,
            record.FingerprintAlgorithm));
    }

    private static RawExcelRow CreateDataRow(string sheetName, RawExcelCell duration) =>
        RawRowFactory.Row(
            sheetName,
            2,
            RawRowFactory.Text("B", "Power interruption"),
            RawRowFactory.Text("C", "Main feed"),
            RawRowFactory.DateTimeCell("D", new DateTime(2026, 8, 1)),
            RawRowFactory.TimeCell("E", new TimeSpan(10, 0, 0)),
            RawRowFactory.DateTimeCell("F", new DateTime(2026, 8, 1)),
            RawRowFactory.TimeCell("G", new TimeSpan(12, 0, 0)),
            RawRowFactory.Text("H", "Completed"),
            duration);
}
