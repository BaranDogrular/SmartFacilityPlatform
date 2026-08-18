using System.Text.Json;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Imports;

namespace SmartFacility.Application.Tests;

public sealed class ScadaCampusTrackingRealWorkbookTests
{
    private const string WorkbookEnvironmentVariable = "SMARTFACILITY_SCADA_WORKBOOK_PATH";

    private static readonly int[] PilotRows =
        [3, 8, 11, 19, 21, 22, 23, 24, 69, 70, 85, 86, 92, 98, 121, 128, 129];

    [Fact]
    public async Task Real_workbook_has_127_safe_distinct_occurrences_and_expected_edge_cases()
    {
        var workbookPath = Environment.GetEnvironmentVariable(WorkbookEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(workbookPath))
        {
            return;
        }

        Assert.True(File.Exists(workbookPath), $"Workbook does not exist: {workbookPath}");
        var profile = LoadProductionProfile();
        var worksheet = profile.GetWorksheet(ScadaAlarmWorksheetNames.KampusTakip);
        var allRows = await ReadRowsAsync(
            workbookPath,
            [new WorksheetReadRequest(worksheet.Name, worksheet.HeaderRowNumber)]);
        var header = Assert.Single(allRows, row => row.RowNumber == worksheet.HeaderRowNumber);
        Assert.Empty(ProfileHeaderValidator.Validate(header, worksheet));
        var rows = allRows.Where(row => row.RowNumber >= worksheet.FirstDataRowNumber).ToArray();

        Assert.Equal(127, rows.Length);
        Assert.Equal(3, rows.Min(row => row.RowNumber));
        Assert.Equal(129, rows.Max(row => row.RowNumber));
        Assert.All(PilotRows, rowNumber => Assert.Contains(rows, row => row.RowNumber == rowNumber));

        var processor = new ScadaAlarmImportProcessor();
        var decisions = new List<ImportRowDecision>(rows.Length);
        foreach (var row in rows)
        {
            decisions.Add(await processor.ProcessAsync(row, profile, CancellationToken.None));
        }

        Assert.Equal(127, decisions.Count(decision => decision.Disposition == ImportRowDisposition.Success));
        Assert.DoesNotContain(decisions, decision => decision.Disposition == ImportRowDisposition.Error);
        Assert.DoesNotContain(decisions, decision => decision.Disposition == ImportRowDisposition.Ignore);
        var alarms = rows.Zip(decisions)
            .ToDictionary(pair => pair.First.RowNumber, pair => Assert.IsType<ScadaAlarmEvent>(pair.Second.Entity));

        var provider = new ImportFingerprintProvider();
        var occurrences = rows.Select(row => provider.Calculate(ImportSourceTypes.ScadaAlarm, row)).ToArray();
        Assert.All(occurrences, fingerprints =>
        {
            Assert.Equal(ScadaCampusTrackingIdempotencyFingerprintCalculator.Algorithm, fingerprints.FingerprintAlgorithm);
            Assert.Equal(64, Assert.IsType<string>(fingerprints.IdempotencyFingerprint).Length);
            Assert.Equal(fingerprints.IdempotencyFingerprint, fingerprints.DuplicateFingerprint);
            Assert.NotEmpty(fingerprints.RowFingerprint);
        });
        Assert.Equal(127, occurrences.Select(item => item.IdempotencyFingerprint).Distinct().Count());

        var correlations = rows.Select(row =>
            ScadaCampusTrackingCorrelationKeyCalculator.Calculate(ImportSourceTypes.ScadaAlarm, row)).ToArray();
        Assert.Equal(127, correlations.Distinct().Count());

        var known = new HashSet<string>(StringComparer.Ordinal);
        Assert.Equal(127, occurrences.Count(item => known.Add(Assert.IsType<string>(item.IdempotencyFingerprint))));
        Assert.Equal(127, occurrences.Count(item => !known.Add(Assert.IsType<string>(item.IdempotencyFingerprint))));
        Assert.Equal(127, known.Count);

        Assert.Equal(12, rows.Count(HasHelperValue));
        Assert.DoesNotContain(rows, IsHelperOnlyRow);
        await AssertHelperInvarianceAsync(rows.First(HasHelperValue), profile, processor);

        AssertEdgeCases(rows.ToDictionary(row => row.RowNumber), alarms, occurrences);
        await AssertCrossSheetSafetyAsync(workbookPath, profile, occurrences);
    }

    private static void AssertEdgeCases(
        IReadOnlyDictionary<int, RawExcelRow> rows,
        IReadOnlyDictionary<int, ScadaAlarmEvent> alarms,
        IReadOnlyList<ImportRowFingerprints> occurrences)
    {
        Assert.Equal("Received:InvalidTime;Cleared:Missing", alarms[3].DateTimeParseStatus);
        Assert.Null(alarms[3].ReceivedAt);
        Assert.Equal("Received:Parsed;Cleared:Parsed", alarms[8].DateTimeParseStatus);
        Assert.Equal("ASANSÖR", alarms[11].AlarmType);
        Assert.Equal("ÖNEMLİ", alarms[11].InterventionLevel);
        Assert.Equal("SU BASKINI", alarms[19].AlarmType);
        Assert.Equal("ACİL", alarms[19].InterventionLevel);
        Assert.Equal(3, new[] { 21, 22, 23 }.Select(row => FingerprintAt(row, rows)).Distinct().Count());
        Assert.False(string.IsNullOrWhiteSpace(alarms[24].FloorRaw));
        Assert.Null(alarms[24].ClearedAt);
        Assert.Equal("Received:InvalidDate;Cleared:InvalidDate", alarms[69].DateTimeParseStatus);
        Assert.Null(alarms[69].ReceivedAt);
        Assert.Null(alarms[69].ClearedAt);
        var malformedRawDates = new[]
        {
            rows[69].GetCell("H")?.RawValue,
            rows[69].GetCell("J")?.RawValue,
            rows[70].GetCell("H")?.RawValue
        };
        Assert.Contains("26:05:2022", malformedRawDates);
        Assert.Contains("26:07:2022", malformedRawDates);
        Assert.Equal("Received:InvalidDate;Cleared:DateOnlySource", alarms[70].DateTimeParseStatus);
        Assert.Null(alarms[70].ReceivedAt);
        Assert.Null(alarms[70].ClearedAt);
        Assert.NotEqual(FingerprintAt(85, rows), FingerprintAt(86, rows));
        Assert.Contains('\n', Assert.IsType<string>(rows[92].GetCell("N")?.RawValue));
        Assert.DoesNotContain('\n', Assert.IsType<string>(alarms[92].Note));
        Assert.Null(alarms[98].ClearedAt);
        Assert.Null(alarms[98].StatusRaw);
        Assert.Null(alarms[98].Note);
        Assert.Contains("Cleared:DateOnlySource", alarms[121].DateTimeParseStatus, StringComparison.Ordinal);
        Assert.Null(alarms[121].Note);
        Assert.Contains("DateOnlySource", alarms[128].DateTimeParseStatus, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(alarms[128].Note));
        Assert.Contains("DateOnlySource", alarms[129].DateTimeParseStatus, StringComparison.Ordinal);
        Assert.Null(alarms[129].StatusRaw);
        Assert.Null(alarms[129].Note);
        Assert.Equal(127, occurrences.Count);
    }

    private static async Task AssertHelperInvarianceAsync(
        RawExcelRow original,
        IImportSourceProfile profile,
        ScadaAlarmImportProcessor processor)
    {
        var withoutHelpers = original with
        {
            Cells = original.Cells
                .Where(pair => pair.Key is not ("S" or "T" or "U"))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        };
        var changedHelpers = original with
        {
            Cells = original.Cells
                .Where(pair => pair.Key is not ("S" or "T" or "U"))
                .Append(new KeyValuePair<string, RawExcelCell>("S", NewTextCell("S", "Changed helper")))
                .Append(new KeyValuePair<string, RawExcelCell>("T", NewTextCell("T", "Changed helper")))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        };

        Assert.Equal(Fingerprint(original), Fingerprint(withoutHelpers));
        Assert.Equal(Fingerprint(original), Fingerprint(changedHelpers));
        Assert.NotEqual(
            RowFingerprintCalculator.Calculate(ImportSourceTypes.ScadaAlarm, original),
            RowFingerprintCalculator.Calculate(ImportSourceTypes.ScadaAlarm, changedHelpers));

        var originalAlarm = Assert.IsType<ScadaAlarmEvent>(
            (await processor.ProcessAsync(original, profile, CancellationToken.None)).Entity);
        var changedAlarm = Assert.IsType<ScadaAlarmEvent>(
            (await processor.ProcessAsync(changedHelpers, profile, CancellationToken.None)).Entity);
        Assert.Equal(originalAlarm.SectionRaw, changedAlarm.SectionRaw);
        Assert.Equal(originalAlarm.LocationRaw, changedAlarm.LocationRaw);
        Assert.Equal(originalAlarm.FloorRaw, changedAlarm.FloorRaw);
        Assert.Equal(originalAlarm.AlarmType, changedAlarm.AlarmType);
        Assert.Equal(originalAlarm.InterventionLevel, changedAlarm.InterventionLevel);
        Assert.Equal(originalAlarm.ZoneRaw, changedAlarm.ZoneRaw);
        Assert.Equal(originalAlarm.Description, changedAlarm.Description);
        Assert.Equal(originalAlarm.ReceivedAt, changedAlarm.ReceivedAt);
        Assert.Equal(originalAlarm.ClearedAt, changedAlarm.ClearedAt);
        Assert.Equal(originalAlarm.ResponsibleRaw, changedAlarm.ResponsibleRaw);
        Assert.Equal(originalAlarm.StatusRaw, changedAlarm.StatusRaw);
        Assert.Equal(originalAlarm.Note, changedAlarm.Note);
    }

    private static async Task AssertCrossSheetSafetyAsync(
        string workbookPath,
        IImportSourceProfile profile,
        IReadOnlyCollection<ImportRowFingerprints> campusOccurrences)
    {
        string[] otherSheets = ["ELEKTRİK ARIZALARI", "MEKANİK", "YANGIN", "ENERJİ"];
        var requests = otherSheets.Select(sheetName =>
        {
            var sheet = profile.GetWorksheet(sheetName);
            return new WorksheetReadRequest(sheet.Name, sheet.FirstDataRowNumber);
        }).ToArray();
        var rows = await ReadRowsAsync(workbookPath, requests);
        var campusSet = campusOccurrences
            .Select(item => Assert.IsType<string>(item.IdempotencyFingerprint))
            .ToHashSet(StringComparer.Ordinal);
        var otherSet = rows
            .Select(row => ScadaCampusTrackingIdempotencyFingerprintCalculator.Calculate(
                ImportSourceTypes.ScadaAlarm,
                row))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(campusSet.Intersect(otherSet));
    }

    private static string FingerprintAt(int rowNumber, IReadOnlyDictionary<int, RawExcelRow> rows) =>
        Fingerprint(rows[rowNumber]);

    private static string Fingerprint(RawExcelRow row) =>
        ScadaCampusTrackingIdempotencyFingerprintCalculator.Calculate(ImportSourceTypes.ScadaAlarm, row);

    private static bool HasHelperValue(RawExcelRow row) =>
        HasValue(row.GetCell("S")) || HasValue(row.GetCell("T"));

    private static bool IsHelperOnlyRow(RawExcelRow row) =>
        HasHelperValue(row) && !Enumerable.Range('A', 14).Any(column =>
            HasValue(row.GetCell(((char)column).ToString())));

    private static bool HasValue(RawExcelCell? cell) =>
        !string.IsNullOrWhiteSpace(cell?.RawValue);

    private static RawExcelCell NewTextCell(string column, string value) =>
        new(column, value, value, "Text", null, null, null, null);

    private static async Task<List<RawExcelRow>> ReadRowsAsync(
        string workbookPath,
        IReadOnlyList<WorksheetReadRequest> worksheets)
    {
        var rows = new List<RawExcelRow>();
        var reader = new ClosedXmlWorkbookReader();
        await foreach (var row in reader.ReadRowsAsync(new ExcelReadRequest(workbookPath, worksheets)))
        {
            rows.Add(row);
        }

        return rows;
    }

    private static IImportSourceProfile LoadProductionProfile()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindAppSettingsPath()));
        var options = document.RootElement
            .GetProperty("ImportProfiles")
            .GetProperty(ImportProfileKeys.ScadaAlarm)
            .Deserialize<ImportProfileOptions>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return new ScadaAlarmImportProfile(options ?? throw new InvalidOperationException("SCADA profile is missing."));
    }

    private static string FindAppSettingsPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SmartFacilityPlatform.sln")))
            {
                return Path.Combine(directory.FullName, "backend", "SmartFacility.Api", "appsettings.json");
            }
        }

        throw new InvalidOperationException("SmartFacilityPlatform solution root could not be located.");
    }
}
