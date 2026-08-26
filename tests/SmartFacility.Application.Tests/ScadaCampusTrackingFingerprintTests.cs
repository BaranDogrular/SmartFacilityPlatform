using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;

namespace SmartFacility.Application.Tests;

public sealed class ScadaCampusTrackingFingerprintTests
{
    [Fact]
    public void Algorithm_and_fingerprint_shape_are_versioned_and_deterministic()
    {
        var row = CampusRow(3);
        var first = Calculate(row);

        Assert.Equal("scada-campus-tracking/v1", ScadaCampusTrackingIdempotencyFingerprintCalculator.Algorithm);
        Assert.Equal(64, first.Length);
        Assert.Equal(first, Calculate(row));
    }

    [Fact]
    public void Source_row_number_and_row_order_do_not_affect_occurrence_identity()
    {
        Assert.Equal(Calculate(CampusRow(3)), Calculate(CampusRow(999)));

        var forward = Enumerable.Range(3, 20).Select(AcceptanceRow).Select(Calculate).ToArray();
        var reversed = Enumerable.Range(3, 20).Reverse().Select(AcceptanceRow).Select(Calculate).ToArray();
        Assert.Equal(forward.Order(), reversed.Order());
    }

    [Fact]
    public void Formatted_only_changes_do_not_affect_occurrence_identity()
    {
        var originalDate = RawRowFactory.DateTimeCell("H", new DateTime(2026, 8, 7));
        var changedFormat = originalDate with { FormattedValue = "7 Ağustos 2026" };

        Assert.Equal(
            Calculate(CampusRow(3, originalDate)),
            Calculate(CampusRow(999, changedFormat)));
    }

    [Fact]
    public void Equivalent_semantic_timestamp_representations_have_same_identity()
    {
        var timestamp = new DateTime(2026, 8, 7, 8, 30, 0);
        var typed = CampusRow(
            3,
            RawRowFactory.DateTimeCell("H", timestamp.Date),
            RawRowFactory.TimeCell("I", timestamp.TimeOfDay));
        var numeric = CampusRow(
            4,
            RawRowFactory.Number("H", timestamp.Date.ToOADate()),
            RawRowFactory.Number("I", timestamp.TimeOfDay.TotalDays));
        var text = CampusRow(
            5,
            RawRowFactory.Text("H", "07.08.2026"),
            RawRowFactory.Text("I", "08:30"));

        Assert.Equal(Calculate(typed), Calculate(numeric));
        Assert.Equal(Calculate(typed), Calculate(text));
    }

    [Fact]
    public void Helper_columns_are_excluded_but_legacy_lineage_identity_remains_helper_sensitive()
    {
        var original = CampusRow(3);
        var changed = CampusRow(
            999,
            RawRowFactory.Text("S", "Changed helper intervention"),
            RawRowFactory.Text("T", "Changed helper alarm type"),
            RawRowFactory.Text("U", "Hidden helper"));

        Assert.Equal(Calculate(original), Calculate(changed));
        Assert.NotEqual(
            RowFingerprintCalculator.Calculate(ImportSourceTypes.ScadaAlarm, original),
            RowFingerprintCalculator.Calculate(ImportSourceTypes.ScadaAlarm, changed));
    }

    [Theory]
    [InlineData("A", "Changed section")]
    [InlineData("B", "Changed location")]
    [InlineData("C", "Changed floor")]
    [InlineData("D", "Changed alarm type")]
    [InlineData("E", "Changed intervention")]
    [InlineData("F", "Changed zone")]
    [InlineData("G", "Changed description")]
    [InlineData("L", "Changed responsible")]
    [InlineData("M", "Changed status")]
    [InlineData("N", "Changed note")]
    public void Meaningful_authoritative_text_change_changes_occurrence_identity(
        string column,
        string value)
    {
        Assert.NotEqual(
            Calculate(CampusRow(3)),
            Calculate(CampusRow(999, RawRowFactory.Text(column, value))));
    }

    [Fact]
    public void Mutable_lifecycle_fields_change_occurrence_but_not_business_correlation()
    {
        var original = CampusRow(3);
        var changed = CampusRow(
            999,
            RawRowFactory.DateTimeCell("J", new DateTime(2026, 8, 7)),
            RawRowFactory.TimeCell("K", new TimeSpan(12, 0, 0)),
            RawRowFactory.Text("L", "Changed responsible"),
            RawRowFactory.Text("M", "Changed status"),
            RawRowFactory.Text("N", "Changed note"));

        Assert.Equal(Correlation(original), Correlation(changed));
        Assert.NotEqual(Calculate(original), Calculate(changed));
    }

    [Fact]
    public void Whitespace_newline_and_casing_normalization_is_deterministic()
    {
        var first = CampusRow(3, RawRowFactory.Text("N", "  Action\r\n  completed "));
        var equivalent = CampusRow(4, RawRowFactory.Text("N", "ACTION COMPLETED"));
        var different = CampusRow(5, RawRowFactory.Text("N", "ACTION NOT COMPLETED"));

        Assert.Equal(Calculate(first), Calculate(equivalent));
        Assert.NotEqual(Calculate(first), Calculate(different));
    }

    [Fact]
    public void Invalid_timestamp_fallback_is_deterministic_and_keeps_raw_meaning()
    {
        var first = CampusRow(3, RawRowFactory.Text("H", " 26:05:2022 "));
        var equivalent = CampusRow(4, RawRowFactory.Text("H", "26:05:2022"));
        var different = CampusRow(5, RawRowFactory.Text("H", "26:07:2022"));

        Assert.Equal(Calculate(first), Calculate(equivalent));
        Assert.NotEqual(Calculate(first), Calculate(different));
    }

    [Fact]
    public void Source_sheet_is_part_of_occurrence_namespace()
    {
        var campus = CampusRow(3);
        var otherSheet = campus with { SheetName = "ENERJİ" };

        Assert.NotEqual(Calculate(campus), Calculate(otherSheet));
    }

    [Fact]
    public void Provider_routes_only_normalized_campus_alarm_sheet_to_campus_algorithm()
    {
        var provider = new ImportFingerprintProvider();
        var fingerprints = provider.Calculate(ImportSourceTypes.ScadaAlarm, CampusRow(3));

        Assert.Equal(
            RowFingerprintCalculator.Calculate(ImportSourceTypes.ScadaAlarm, CampusRow(3)),
            fingerprints.RowFingerprint);
        Assert.Equal(Calculate(CampusRow(3)), fingerprints.IdempotencyFingerprint);
        Assert.Equal(ScadaCampusTrackingIdempotencyFingerprintCalculator.Algorithm, fingerprints.FingerprintAlgorithm);
        Assert.Equal(fingerprints.IdempotencyFingerprint, fingerprints.DuplicateFingerprint);
        Assert.Equal(
            ScadaCampusTrackingIdempotencyFingerprintCalculator.Algorithm,
            provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaAlarm, "  KAMPÜS\r\n TAKİP  "));

        Assert.Equal(
            ScadaFireAlarmIdempotencyFingerprintCalculator.Algorithm,
            provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaAlarm, ScadaAlarmWorksheetNames.Yangin));
        Assert.Equal(
            ScadaEnergyAlarmIdempotencyFingerprintCalculator.Algorithm,
            provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaAlarm, ScadaAlarmWorksheetNames.Enerji));
        Assert.Equal(
            HistoricalWorkOrderIdempotencyFingerprintCalculator.Algorithm,
            provider.GetIdempotencyAlgorithm(ImportSourceTypes.HistoricalWorkOrder, "Data"));
        Assert.Equal(
            ScadaOutageIdempotencyFingerprintCalculator.Algorithm,
            provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaOutage, "Data"));
        Assert.Null(provider.GetIdempotencyAlgorithm(ImportSourceTypes.Asset, ScadaAlarmWorksheetNames.KampusTakip));
        Assert.Equal(
            CanonicalWorkOrderIdentityCalculator.Algorithm,
            provider.GetIdempotencyAlgorithm(
                ImportSourceTypes.WorkOrder,
                ScadaAlarmWorksheetNames.KampusTakip));
        Assert.Null(provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaAlarm, "ELEKTRİK ARIZALARI"));
        Assert.Null(provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaAlarm, "MEKANİK"));
    }

    [Fact]
    public async Task Exact_reimport_uses_versioned_duplicate_key_and_keeps_raw_lineage()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var firstRow = CampusRow(3, RawRowFactory.Text("S", "Helper S"));
        var repeatedRow = CampusRow(999, RawRowFactory.Text("S", "Helper S"));

        var first = await CreateService(database, firstRow).ImportAsync(
            new ImportRequest(ImportProfileKeys.ScadaAlarm, "campus-first.xlsx"));
        var second = await CreateService(database, repeatedRow).ImportAsync(
            new ImportRequest(ImportProfileKeys.ScadaAlarm, "campus-second.xlsx"));

        Assert.Equal(1, first.SuccessfulRows);
        Assert.Equal(1, second.DuplicateRows);
        Assert.Equal(1, await database.Context.ScadaAlarmEvents.CountAsync());
        var sourceRecords = await database.Context.ImportSourceRecords.OrderBy(record => record.Id).ToArrayAsync();
        Assert.Equal(2, sourceRecords.Length);
        Assert.Equal("Succeeded", sourceRecords[0].ParseStatus);
        Assert.Equal("Duplicate", sourceRecords[1].ParseStatus);
        Assert.All(sourceRecords, record =>
        {
            Assert.Equal("KAMPÜS TAKİP", record.SourceSheet);
            Assert.NotEmpty(record.RowFingerprint);
            Assert.NotEmpty(Assert.IsType<string>(record.IdempotencyFingerprint));
            Assert.Equal(ScadaCampusTrackingIdempotencyFingerprintCalculator.Algorithm, record.FingerprintAlgorithm);
            Assert.Contains("\"A\"", record.RawData, StringComparison.Ordinal);
            Assert.Contains("\"N\"", record.RawData, StringComparison.Ordinal);
            Assert.Contains("\"S\"", record.RawData, StringComparison.Ordinal);
        });
    }

    private static string Calculate(RawExcelRow row) =>
        ScadaCampusTrackingIdempotencyFingerprintCalculator.Calculate(ImportSourceTypes.ScadaAlarm, row);

    private static string Correlation(RawExcelRow row) =>
        ScadaCampusTrackingCorrelationKeyCalculator.Calculate(ImportSourceTypes.ScadaAlarm, row);

    private static RawExcelRow AcceptanceRow(int rowNumber) =>
        CampusRow(
            rowNumber,
            RawRowFactory.Text("G", $"Campus alarm {rowNumber}"),
            RawRowFactory.Text("N", $"Occurrence {rowNumber}"));

    private static RawExcelRow CampusRow(int rowNumber, params RawExcelCell[] overrides)
    {
        RawExcelCell[] payload =
        [
            RawRowFactory.Text("A", "KAMPÜS"),
            RawRowFactory.Text("B", "Main building"),
            RawRowFactory.Text("C", "Floor 1"),
            RawRowFactory.Text("D", "Campus alarm"),
            RawRowFactory.Text("E", "Level 1"),
            RawRowFactory.Text("F", "Zone 1"),
            RawRowFactory.Text("G", "Campus warning"),
            RawRowFactory.DateTimeCell("H", new DateTime(2026, 8, 7)),
            RawRowFactory.TimeCell("I", new TimeSpan(10, 0, 0)),
            RawRowFactory.DateTimeCell("J", new DateTime(2026, 8, 7)),
            RawRowFactory.TimeCell("K", new TimeSpan(10, 15, 0)),
            RawRowFactory.Text("L", "Operator"),
            RawRowFactory.Text("M", "Closed"),
            RawRowFactory.Text("N", "Resolved")
        ];
        var cells = payload
            .Concat(overrides)
            .GroupBy(cell => cell.Column, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        return new RawExcelRow("KAMPÜS TAKİP", rowNumber, cells);
    }

    private static ExcelImportService CreateService(SqliteTestDatabase database, RawExcelRow row)
    {
        var header = RawRowFactory.Row("KAMPÜS TAKİP", 2, RawRowFactory.Text("G", "AÇIKLAMA"));
        IImportSourceProfile profile = TestProfiles.ScadaCampusTracking();
        return new ExcelImportService(
            new FakeWorkbookReader([header, row]),
            database.Store,
            new ImportProfileCatalog([profile]),
            new ImportFingerprintProvider(),
            [new ScadaAlarmImportProcessor()],
            NullLogger<ExcelImportService>.Instance);
    }
}
