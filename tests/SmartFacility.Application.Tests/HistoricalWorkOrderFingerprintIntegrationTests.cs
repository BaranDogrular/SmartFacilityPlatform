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

public sealed class HistoricalWorkOrderFingerprintIntegrationTests
{
    private const string SheetName = "Toplam İş Emri";

    [Fact]
    public async Task Q_precision_change_is_duplicate_but_selected_field_change_is_new_record()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var original = CreateDataRow("TIM-100", "0.4166666666666667");
        var qChanged = CreateDataRow("TIM-100", "0.41666666666667003");
        var selectedFieldChanged = CreateDataRow("TIM-101", "0.41666666666667003");

        var first = await CreateService(database, original)
            .ImportAsync(new ImportRequest(ImportProfileKeys.HistoricalWorkOrder, "pilot.xlsx"));
        var second = await CreateService(database, qChanged)
            .ImportAsync(new ImportRequest(ImportProfileKeys.HistoricalWorkOrder, "pilot-resaved.xlsx"));
        var third = await CreateService(database, selectedFieldChanged)
            .ImportAsync(new ImportRequest(ImportProfileKeys.HistoricalWorkOrder, "pilot-changed.xlsx"));

        Assert.Equal(1, first.SuccessfulRows);
        Assert.Equal(1, second.DuplicateRows);
        Assert.Equal(1, third.SuccessfulRows);
        Assert.Equal(2, await database.Context.HistoricalWorkOrders.CountAsync());

        var sourceRecords = await database.Context.ImportSourceRecords
            .OrderBy(record => record.Id)
            .ToListAsync();
        Assert.Equal(3, sourceRecords.Count);
        Assert.NotEqual(sourceRecords[0].RowFingerprint, sourceRecords[1].RowFingerprint);
        Assert.Equal(
            sourceRecords[0].IdempotencyFingerprint,
            sourceRecords[1].IdempotencyFingerprint);
        Assert.NotEqual(
            sourceRecords[1].IdempotencyFingerprint,
            sourceRecords[2].IdempotencyFingerprint);
        Assert.All(sourceRecords, record => Assert.Equal(
            HistoricalWorkOrderIdempotencyFingerprintCalculator.Algorithm,
            record.FingerprintAlgorithm));
    }

    [Fact]
    public async Task Backfilled_pilot_lineage_prevents_existing_entity_from_being_recreated()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var original = CreateDataRow("TIM-100", "0.4166666666666667");
        var resaved = CreateDataRow("TIM-100", "0.41666666666667003");
        var fingerprints = new ImportFingerprintProvider().Calculate(
            ImportSourceTypes.HistoricalWorkOrder,
            original);
        var batch = new ImportBatch
        {
            SourceType = ImportSourceTypes.HistoricalWorkOrder,
            FileName = "legacy-pilot.xlsx",
            Status = "Completed",
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            TotalRows = 1,
            SuccessfulRows = 1
        };
        database.Context.ImportBatches.Add(batch);
        database.Context.HistoricalWorkOrders.Add(new HistoricalWorkOrder
        {
            SourceReference = "TIM-100",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();
        database.Context.ImportSourceRecords.Add(new ImportSourceRecord
        {
            ImportBatchId = batch.Id,
            SourceSheet = SheetName,
            SourceRowNumber = 2,
            RowFingerprint = fingerprints.RowFingerprint,
            IdempotencyFingerprint = fingerprints.IdempotencyFingerprint,
            FingerprintAlgorithm = fingerprints.FingerprintAlgorithm,
            RawData = RawRowSerializer.SerializeValues(original),
            ParseStatus = "Succeeded",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var result = await CreateService(database, resaved)
            .ImportAsync(new ImportRequest(ImportProfileKeys.HistoricalWorkOrder, "original.xlsx"));

        Assert.Equal(1, result.DuplicateRows);
        Assert.Equal(0, result.SuccessfulRows);
        Assert.Equal(1, await database.Context.HistoricalWorkOrders.CountAsync());
        var duplicateRecord = await database.Context.ImportSourceRecords
            .SingleAsync(record => record.ParseStatus == "Duplicate");
        Assert.NotEqual(fingerprints.RowFingerprint, duplicateRecord.RowFingerprint);
        Assert.Equal(fingerprints.IdempotencyFingerprint, duplicateRecord.IdempotencyFingerprint);
    }

    private static ExcelImportService CreateService(
        SqliteTestDatabase database,
        RawExcelRow dataRow)
    {
        var profile = TestProfiles.HistoricalWorkOrder();
        var catalog = new ImportProfileCatalog([profile]);
        IImportRowProcessor[] processors = [new HistoricalWorkOrderImportProcessor()];

        return new ExcelImportService(
            new FakeWorkbookReader(
            [
                RawRowFactory.Row(
                    SheetName,
                    1,
                    RawRowFactory.Text("A", "Şikayet Kodu")),
                dataRow
            ]),
            database.Store,
            catalog,
            new ImportFingerprintProvider(),
            processors,
            NullLogger<ExcelImportService>.Instance);
    }

    private static RawExcelRow CreateDataRow(string sourceReference, string qValue) =>
        RawRowFactory.Row(
            SheetName,
            2,
            RawRowFactory.Text("A", sourceReference),
            RawRowFactory.Text("C", "A BLOK"),
            RawRowFactory.Text("D", "PERSON ONE"),
            RawRowFactory.Text("E", "2026-08-01 10:00:00"),
            RawRowFactory.Text("K", "MECHANICAL"),
            RawRowFactory.Text("M", "WATER LEAK"),
            RawRowFactory.Text("P", "10"),
            RawRowFactory.Text("Q", qValue));
}
