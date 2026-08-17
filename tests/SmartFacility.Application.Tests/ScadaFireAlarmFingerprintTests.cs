using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;

namespace SmartFacility.Application.Tests;

public sealed class ScadaFireAlarmFingerprintTests
{
    [Theory]
    [InlineData("O", "changed annotation")]
    [InlineData("S", "changed helper intervention")]
    [InlineData("T", "changed helper alarm type")]
    [InlineData("E", "changed visible intervention")]
    [InlineData("F", "changed zone")]
    [InlineData("L", "changed responsible")]
    [InlineData("M", "changed status")]
    [InlineData("N", "changed note")]
    [InlineData("J", "2026-08-08")]
    [InlineData("K", "12:00")]
    public void Mutable_helper_and_lifecycle_fields_do_not_change_identity(
        string column,
        string changedValue)
    {
        var original = FireRow(3);
        var changed = FireRow(999, RawRowFactory.Text(column, changedValue));

        Assert.Equal(Calculate(original), Calculate(changed));
    }

    [Theory]
    [InlineData("B", "Different location")]
    [InlineData("C", "Different floor")]
    [InlineData("D", "Different alarm type")]
    [InlineData("G", "Different description")]
    public void Canonical_business_fields_change_identity(string column, string changedValue)
    {
        Assert.NotEqual(
            Calculate(FireRow(3)),
            Calculate(FireRow(3, RawRowFactory.Text(column, changedValue))));
    }

    [Fact]
    public void Received_timestamp_changes_identity()
    {
        var changed = FireRow(
            3,
            RawRowFactory.DateTimeCell("H", new DateTime(2026, 8, 7)),
            RawRowFactory.TimeCell("I", new TimeSpan(11, 0, 0)));

        Assert.NotEqual(Calculate(FireRow(3)), Calculate(changed));
    }

    [Fact]
    public void Invalid_received_fallback_is_deterministic_and_uses_normalized_raw_values()
    {
        var first = FireRow(
            356,
            RawRowFactory.Text("H", " 21.07.0203 "),
            RawRowFactory.Text("I", " 08:30 "));
        var repeated = FireRow(
            999,
            RawRowFactory.Text("H", "21.07.0203"),
            RawRowFactory.Text("I", "08:30"));
        var differentRaw = FireRow(
            999,
            RawRowFactory.Text("H", "25.04.0206"),
            RawRowFactory.Text("I", "08:30"));

        Assert.Equal(Calculate(first), Calculate(repeated));
        Assert.NotEqual(Calculate(first), Calculate(differentRaw));
    }

    [Theory]
    [InlineData(439, 440)]
    [InlineData(596, 597)]
    public void Known_floor_collision_pairs_remain_distinct(int firstRow, int secondRow)
    {
        var first = FireRow(firstRow, RawRowFactory.Text("C", "Floor A"));
        var second = FireRow(secondRow, RawRowFactory.Text("C", "Floor B"));

        Assert.NotEqual(Calculate(first), Calculate(second));
    }

    [Fact]
    public void Provider_routes_only_YANGIN_alarm_sheet_to_versioned_algorithm()
    {
        var provider = new ImportFingerprintProvider();
        var fire = provider.Calculate(ImportSourceTypes.ScadaAlarm, FireRow(3));
        var electricRow = RawRowFactory.Row(
            "ELEKTRİK ARIZALARI",
            2,
            RawRowFactory.Text("G", "Alarm"));
        var mechanicalRow = RawRowFactory.Row(
            "MEKANİK",
            3,
            RawRowFactory.Text("G", "Alarm"));

        Assert.Equal(ScadaFireAlarmIdempotencyFingerprintCalculator.Algorithm, fire.FingerprintAlgorithm);
        Assert.NotNull(fire.IdempotencyFingerprint);
        Assert.Equal(fire.IdempotencyFingerprint, fire.DuplicateFingerprint);
        Assert.Null(provider.Calculate(ImportSourceTypes.ScadaAlarm, electricRow).FingerprintAlgorithm);
        Assert.Null(provider.Calculate(ImportSourceTypes.ScadaAlarm, mechanicalRow).FingerprintAlgorithm);
        Assert.Equal(
            ScadaFireAlarmIdempotencyFingerprintCalculator.Algorithm,
            provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaAlarm, "YANGIN"));
        Assert.Null(provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaAlarm, "ELEKTRİK ARIZALARI"));
        Assert.Null(provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaAlarm, "MEKANİK"));
    }

    [Fact]
    public void Existing_historical_and_outage_algorithms_are_unchanged()
    {
        var provider = new ImportFingerprintProvider();

        Assert.Equal(
            HistoricalWorkOrderIdempotencyFingerprintCalculator.Algorithm,
            provider.GetIdempotencyAlgorithm(ImportSourceTypes.HistoricalWorkOrder, "Toplam İş Emri"));
        Assert.Equal(
            ScadaOutageIdempotencyFingerprintCalculator.Algorithm,
            provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaOutage, "SCADA SÜREKLİLİK"));
    }

    [Fact]
    public async Task Reimport_with_mutable_changes_is_duplicate_and_does_not_create_second_event()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var profile = TestProfiles.ScadaFireAlarm();
        var header = RawRowFactory.Row("YANGIN", 2, RawRowFactory.Text("G", "AÇIKLAMA"));
        var original = FireRow(3);
        var service = CreateService(database, profile, header, original);

        var first = await service.ImportAsync(
            new ImportRequest(ImportProfileKeys.ScadaAlarm, "fire-first.xlsx"));

        var changedService = CreateService(
            database,
            profile,
            header,
            FireRow(
                500,
                RawRowFactory.Text("M", "UPDATED"),
                RawRowFactory.Text("N", "Updated note"),
                RawRowFactory.Text("O", "Updated annotation")));
        var second = await changedService.ImportAsync(
            new ImportRequest(ImportProfileKeys.ScadaAlarm, "fire-second.xlsx"));

        Assert.Equal(1, first.SuccessfulRows);
        Assert.Equal(1, second.DuplicateRows);
        Assert.Equal(1, await database.Context.ScadaAlarmEvents.CountAsync());
        var records = await database.Context.ImportSourceRecords.OrderBy(record => record.Id).ToArrayAsync();
        Assert.Equal(2, records.Length);
        Assert.All(
            records,
            record => Assert.Equal(
                ScadaFireAlarmIdempotencyFingerprintCalculator.Algorithm,
                record.FingerprintAlgorithm));
        Assert.Equal("Succeeded", records[0].ParseStatus);
        Assert.Equal("Duplicate", records[1].ParseStatus);
    }

    private static string Calculate(RawExcelRow row) =>
        ScadaFireAlarmIdempotencyFingerprintCalculator.Calculate(
            ImportSourceTypes.ScadaAlarm,
            row);

    private static RawExcelRow FireRow(int rowNumber, params RawExcelCell[] overrides)
    {
        RawExcelCell[] identity =
        [
            RawRowFactory.Text("B", "Main building"),
            RawRowFactory.Text("C", "Floor 1"),
            RawRowFactory.Text("D", "Fire alarm"),
            RawRowFactory.Text("G", "Detector warning"),
            RawRowFactory.DateTimeCell("H", new DateTime(2026, 8, 7)),
            RawRowFactory.TimeCell("I", new TimeSpan(10, 0, 0))
        ];
        var cells = identity
            .Concat(overrides)
            .GroupBy(cell => cell.Column, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        return new RawExcelRow("YANGIN", rowNumber, cells);
    }

    private static ExcelImportService CreateService(
        SqliteTestDatabase database,
        IImportSourceProfile profile,
        params RawExcelRow[] rows) =>
        new(
            new FakeWorkbookReader(rows),
            database.Store,
            new ImportProfileCatalog([profile]),
            new ImportFingerprintProvider(),
            [new ScadaAlarmImportProcessor()],
            NullLogger<ExcelImportService>.Instance);
}
