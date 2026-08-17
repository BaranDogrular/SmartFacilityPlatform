using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;

namespace SmartFacility.Application.Tests;

public sealed class ScadaEnergyAlarmFingerprintTests
{
    private static readonly int[][] NearCanonicalGroups =
    [
        [6, 7], [14, 15], [18, 19], [24, 25], [44, 45], [66, 67], [68, 69],
        [76, 77], [82, 83], [84, 85, 86], [87, 88], [92, 93, 94],
        [98, 99, 100], [104, 105, 106], [109, 110], [128, 129], [212, 213]
    ];

    [Fact]
    public void Source_row_number_is_not_occurrence_identity()
    {
        Assert.Equal(Calculate(EnergyRow(3)), Calculate(EnergyRow(999)));
    }

    [Fact]
    public void Helper_columns_do_not_change_occurrence_identity_but_remain_in_legacy_audit_identity()
    {
        var original = EnergyRow(3);
        var changed = EnergyRow(
            999,
            RawRowFactory.Text("S", "Changed helper intervention"),
            RawRowFactory.Text("T", "Changed helper alarm type"));

        Assert.Equal(Calculate(original), Calculate(changed));
        Assert.NotEqual(
            RowFingerprintCalculator.Calculate(ImportSourceTypes.ScadaAlarm, original),
            RowFingerprintCalculator.Calculate(ImportSourceTypes.ScadaAlarm, changed));
    }

    [Fact]
    public void Formatted_value_only_change_does_not_change_occurrence_identity()
    {
        var originalDate = RawRowFactory.DateTimeCell("H", new DateTime(2026, 7, 10));
        var changedFormat = originalDate with { FormattedValue = "10 Temmuz 2026" };

        Assert.Equal(
            Calculate(EnergyRow(3, originalDate)),
            Calculate(EnergyRow(3, changedFormat)));
    }

    [Fact]
    public void Equivalent_timestamp_representations_have_the_same_occurrence_identity()
    {
        var timestamp = new DateTime(2026, 7, 10, 8, 54, 0);
        var typed = EnergyRow(
            3,
            RawRowFactory.DateTimeCell("H", timestamp.Date),
            RawRowFactory.TimeCell("I", timestamp.TimeOfDay));
        var numeric = EnergyRow(
            3,
            RawRowFactory.Number("H", timestamp.Date.ToOADate()),
            RawRowFactory.Number("I", timestamp.TimeOfDay.TotalDays));
        var text = EnergyRow(
            3,
            RawRowFactory.Text("H", "10.07.2026"),
            RawRowFactory.Text("I", "08:54"));

        Assert.Equal(Calculate(typed), Calculate(numeric));
        Assert.Equal(Calculate(typed), Calculate(text));
    }

    [Theory]
    [InlineData("E", "Changed intervention")]
    [InlineData("J", "08.08.2026")]
    [InlineData("K", "12:00")]
    [InlineData("L", "Changed responsible")]
    [InlineData("M", "Changed status")]
    [InlineData("N", "Changed note")]
    public void Authoritative_payload_change_changes_occurrence_fingerprint(
        string column,
        string value)
    {
        Assert.NotEqual(
            Calculate(EnergyRow(3)),
            Calculate(EnergyRow(999, RawRowFactory.Text(column, value))));
    }

    [Fact]
    public void Whitespace_newline_and_casing_normalization_is_deterministic()
    {
        var first = EnergyRow(3, RawRowFactory.Text("N", "  Action\r\n  completed "));
        var equivalent = EnergyRow(999, RawRowFactory.Text("N", "ACTION COMPLETED"));
        var differentMeaning = EnergyRow(999, RawRowFactory.Text("N", "ACTION NOT COMPLETED"));

        Assert.Equal(Calculate(first), Calculate(equivalent));
        Assert.NotEqual(Calculate(first), Calculate(differentMeaning));
    }

    [Fact]
    public void Placeholder_x_raw_fallback_is_deterministic()
    {
        var first = EnergyRow(3, RawRowFactory.Text("J", " x "));
        var repeated = EnergyRow(999, RawRowFactory.Text("J", "X"));
        var different = EnergyRow(999, RawRowFactory.Text("J", "invalid-date"));

        Assert.Equal(Calculate(first), Calculate(repeated));
        Assert.NotEqual(Calculate(first), Calculate(different));
    }

    [Fact]
    public void Correlation_ignores_lifecycle_fields_but_occurrence_identity_does_not()
    {
        var original = EnergyRow(6);
        var changed = EnergyRow(
            7,
            RawRowFactory.Text("E", "Changed intervention"),
            RawRowFactory.DateTimeCell("J", new DateTime(2026, 8, 7)),
            RawRowFactory.TimeCell("K", new TimeSpan(12, 0, 0)),
            RawRowFactory.Text("L", "Changed responsible"),
            RawRowFactory.Text("M", "Changed status"),
            RawRowFactory.Text("N", "Changed note"));

        Assert.Equal(Correlation(original), Correlation(changed));
        Assert.NotEqual(Calculate(original), Calculate(changed));
    }

    [Fact]
    public void Synthetic_374_row_acceptance_set_has_expected_occurrence_and_correlation_counts()
    {
        var rows = Enumerable.Range(3, 374).Select(AcceptanceRow).ToArray();
        var correlations = rows.GroupBy(Correlation).Where(group => group.Count() > 1).ToArray();

        Assert.Equal(374, rows.Length);
        Assert.Equal(374, rows.Select(Calculate).Distinct().Count());
        Assert.Equal(372, rows.Select(Correlation).Distinct().Count());
        Assert.Equal(2, correlations.Length);
        Assert.Contains(correlations, group => group.Select(row => row.RowNumber).SequenceEqual([6, 7]));
        Assert.Contains(correlations, group => group.Select(row => row.RowNumber).SequenceEqual([212, 213]));
    }

    [Fact]
    public void Exact_collision_pairs_share_correlation_but_not_occurrence_identity()
    {
        foreach (var pair in new[] { new[] { 6, 7 }, new[] { 212, 213 } })
        {
            var first = AcceptanceRow(pair[0]);
            var second = AcceptanceRow(pair[1]);

            Assert.Equal(Correlation(first), Correlation(second));
            Assert.NotEqual(Calculate(first), Calculate(second));
        }
    }

    [Fact]
    public void All_near_canonical_groups_keep_every_source_occurrence_distinct()
    {
        Assert.Equal(38, NearCanonicalGroups.Sum(group => group.Length));

        foreach (var group in NearCanonicalGroups)
        {
            var rows = group.Select(rowNumber => NearCanonicalRow(group[0], rowNumber)).ToArray();
            Assert.Equal(rows.Length, rows.Select(Calculate).Distinct().Count());
        }
    }

    [Fact]
    public void Two_virtual_passes_produce_374_successes_then_374_duplicates()
    {
        var rows = Enumerable.Range(3, 374).Select(AcceptanceRow).ToArray();
        var known = new HashSet<string>(StringComparer.Ordinal);

        var firstSuccessful = rows.Count(row => known.Add(Calculate(row)));
        var secondDuplicates = rows.Count(row => !known.Add(Calculate(row)));

        Assert.Equal(374, firstSuccessful);
        Assert.Equal(374, secondDuplicates);
        Assert.Equal(374, known.Count);
    }

    [Fact]
    public void Provider_routes_only_energy_alarm_sheet_to_energy_algorithm()
    {
        var provider = new ImportFingerprintProvider();
        var energy = provider.Calculate(ImportSourceTypes.ScadaAlarm, EnergyRow(3));

        Assert.Equal(ScadaEnergyAlarmIdempotencyFingerprintCalculator.Algorithm, energy.FingerprintAlgorithm);
        Assert.NotNull(energy.IdempotencyFingerprint);
        Assert.Equal(energy.IdempotencyFingerprint, energy.DuplicateFingerprint);
        Assert.Equal(
            ScadaEnergyAlarmIdempotencyFingerprintCalculator.Algorithm,
            provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaAlarm, "ENERJİ"));
        Assert.Equal(
            ScadaFireAlarmIdempotencyFingerprintCalculator.Algorithm,
            provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaAlarm, "YANGIN"));
        Assert.Null(provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaAlarm, "ELEKTRİK ARIZALARI"));
        Assert.Null(provider.GetIdempotencyAlgorithm(ImportSourceTypes.ScadaAlarm, "MEKANİK"));
    }

    [Fact]
    public async Task Exact_reimport_is_duplicate_and_keeps_one_event()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var first = await CreateService(database, EnergyRow(3)).ImportAsync(
            new ImportRequest(ImportProfileKeys.ScadaAlarm, "energy-first.xlsx"));
        var second = await CreateService(database, EnergyRow(999)).ImportAsync(
            new ImportRequest(ImportProfileKeys.ScadaAlarm, "energy-second.xlsx"));

        Assert.Equal(1, first.SuccessfulRows);
        Assert.Equal(1, second.DuplicateRows);
        Assert.Equal(1, await database.Context.ScadaAlarmEvents.CountAsync());
        var sourceRecords = await database.Context.ImportSourceRecords
            .OrderBy(record => record.Id)
            .ToArrayAsync();
        Assert.Equal(2, sourceRecords.Length);
        Assert.Equal("Succeeded", sourceRecords[0].ParseStatus);
        Assert.Equal("Duplicate", sourceRecords[1].ParseStatus);
        Assert.All(sourceRecords, record =>
        {
            Assert.Equal("ENERJİ", record.SourceSheet);
            Assert.NotEmpty(record.RowFingerprint);
            Assert.NotEmpty(Assert.IsType<string>(record.IdempotencyFingerprint));
            Assert.Equal(
                ScadaEnergyAlarmIdempotencyFingerprintCalculator.Algorithm,
                record.FingerprintAlgorithm);
            Assert.Contains("\"A\"", record.RawData, StringComparison.Ordinal);
            Assert.Contains("\"N\"", record.RawData, StringComparison.Ordinal);
            Assert.Null(record.RawFormulaData);
        });
    }

    [Fact]
    public async Task Changed_source_payload_creates_a_new_occurrence_snapshot_not_a_business_event_claim()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var original = EnergyRow(3);
        var changed = EnergyRow(999, RawRowFactory.Text("N", "Changed source note"));

        var first = await CreateService(database, original).ImportAsync(
            new ImportRequest(ImportProfileKeys.ScadaAlarm, "energy-first.xlsx"));
        var second = await CreateService(database, changed).ImportAsync(
            new ImportRequest(ImportProfileKeys.ScadaAlarm, "energy-changed.xlsx"));

        Assert.Equal(1, first.SuccessfulRows);
        Assert.Equal(1, second.SuccessfulRows);
        Assert.Equal(2, await database.Context.ScadaAlarmEvents.CountAsync());
        Assert.Equal(Correlation(original), Correlation(changed));
    }

    private static string Calculate(RawExcelRow row) =>
        ScadaEnergyAlarmIdempotencyFingerprintCalculator.Calculate(
            ImportSourceTypes.ScadaAlarm,
            row);

    private static string Correlation(RawExcelRow row) =>
        ScadaEnergyAlarmCorrelationKeyCalculator.Calculate(
            ImportSourceTypes.ScadaAlarm,
            row);

    private static RawExcelRow AcceptanceRow(int rowNumber)
    {
        var correlationGroup = rowNumber switch
        {
            6 or 7 => "Collision 6-7",
            212 or 213 => "Collision 212-213",
            _ => $"Alarm {rowNumber}"
        };

        return EnergyRow(
            rowNumber,
            RawRowFactory.Text("G", correlationGroup),
            RawRowFactory.Text("N", $"Occurrence {rowNumber}"));
    }

    private static RawExcelRow NearCanonicalRow(int groupId, int rowNumber) =>
        EnergyRow(
            rowNumber,
            RawRowFactory.Text("G", $"Near canonical group {groupId}"),
            RawRowFactory.Text("N", $"Occurrence {rowNumber}"));

    private static RawExcelRow EnergyRow(int rowNumber, params RawExcelCell[] overrides)
    {
        RawExcelCell[] payload =
        [
            RawRowFactory.Text("A", "ENERJİ"),
            RawRowFactory.Text("B", "Main building"),
            RawRowFactory.Text("C", "Floor 1"),
            RawRowFactory.Text("D", "Energy alarm"),
            RawRowFactory.Text("E", "Level 1"),
            RawRowFactory.Text("F", "Zone 1"),
            RawRowFactory.Text("G", "Power warning"),
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

        return new RawExcelRow("ENERJİ", rowNumber, cells);
    }

    private static ExcelImportService CreateService(
        SqliteTestDatabase database,
        RawExcelRow row)
    {
        var header = RawRowFactory.Row("ENERJİ", 2, RawRowFactory.Text("G", "AÇIKLAMA"));
        IImportSourceProfile profile = TestProfiles.ScadaEnergyAlarm();
        return new ExcelImportService(
            new FakeWorkbookReader([header, row]),
            database.Store,
            new ImportProfileCatalog([profile]),
            new ImportFingerprintProvider(),
            [new ScadaAlarmImportProcessor()],
            NullLogger<ExcelImportService>.Instance);
    }
}
