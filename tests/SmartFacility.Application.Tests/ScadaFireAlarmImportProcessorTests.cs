using System.Text.Json;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Tests;

public sealed class ScadaFireAlarmImportProcessorTests
{
    [Fact]
    public async Task Valid_received_and_cleared_timestamps_are_preserved()
    {
        var received = new DateTime(2026, 8, 7, 8, 0, 0);
        var cleared = new DateTime(2026, 8, 7, 8, 15, 0);
        var alarm = await ProcessAsync(
            RawRowFactory.DateTimeCell("H", received.Date),
            RawRowFactory.TimeCell("I", received.TimeOfDay),
            RawRowFactory.DateTimeCell("J", cleared.Date),
            RawRowFactory.TimeCell("K", cleared.TimeOfDay));

        Assert.Equal(received, alarm.ReceivedAt);
        Assert.Equal(cleared, alarm.ClearedAt);
        Assert.Equal("Received:Parsed;Cleared:Parsed", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Received_invalid_time_is_null_without_discarding_other_fields()
    {
        var alarm = await ProcessAsync(
            RawRowFactory.Text("A", "YANGIN"),
            RawRowFactory.Text("B", "Fixture location"),
            RawRowFactory.Text("D", "YANGIN ALARMI"),
            RawRowFactory.DateTimeCell("H", new DateTime(2026, 8, 7)),
            RawRowFactory.Text("I", "15.:15"),
            RawRowFactory.Text("M", "ACTIVE"));

        Assert.Null(alarm.ReceivedAt);
        Assert.Equal("YANGIN", alarm.SectionRaw);
        Assert.Equal("Fixture location", alarm.LocationRaw);
        Assert.Equal("YANGIN ALARMI", alarm.AlarmType);
        Assert.Equal("ACTIVE", alarm.StatusRaw);
        Assert.Equal("Received:InvalidTime;Cleared:Missing", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Missing_cleared_timestamp_is_null()
    {
        var alarm = await ProcessAsync();

        Assert.Null(alarm.ClearedAt);
        Assert.Equal("Received:Missing;Cleared:Missing", alarm.DateTimeParseStatus);
    }

    [Theory]
    [InlineData("21.07.0203")]
    [InlineData("25.04.0206")]
    public async Task Suspicious_received_year_is_not_corrected(string rawDate)
    {
        var alarm = await ProcessAsync(
            RawRowFactory.Text("H", rawDate),
            RawRowFactory.Text("I", "08:30"));

        Assert.Null(alarm.ReceivedAt);
        Assert.Equal("Received:SuspiciousYear;Cleared:Missing", alarm.DateTimeParseStatus);
    }

    [Theory]
    [InlineData("H", "Received:DateOnlySource;Cleared:Missing")]
    [InlineData("J", "Received:Missing;Cleared:DateOnlySource")]
    public async Task Date_without_time_does_not_synthesize_midnight(string dateColumn, string expectedStatus)
    {
        var alarm = await ProcessAsync(
            RawRowFactory.DateTimeCell(dateColumn, new DateTime(2026, 8, 7)));

        Assert.Null(dateColumn == "H" ? alarm.ReceivedAt : alarm.ClearedAt);
        Assert.Equal(expectedStatus, alarm.DateTimeParseStatus);
    }

    [Theory]
    [MemberData(nameof(InvalidClearedDateCases))]
    public async Task Invalid_cleared_date_is_null(RawExcelCell invalidDate)
    {
        var alarm = await ProcessAsync(
            invalidDate,
            RawRowFactory.Text("K", "08:30"));

        Assert.Null(alarm.ClearedAt);
        Assert.Equal("Received:Missing;Cleared:InvalidDate", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Invalid_cleared_time_is_null()
    {
        var alarm = await ProcessAsync(
            RawRowFactory.DateTimeCell("J", new DateTime(2026, 8, 7)),
            RawRowFactory.Text("K", "invalid-time"));

        Assert.Null(alarm.ClearedAt);
        Assert.Equal("Received:Missing;Cleared:InvalidTime", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Future_date_is_preserved_and_flagged()
    {
        var future = new DateTime(2026, 8, 8, 9, 0, 0);
        var alarm = await ProcessAsync(
            RawRowFactory.DateTimeCell("J", future.Date),
            RawRowFactory.TimeCell("K", future.TimeOfDay));

        Assert.Equal(future, alarm.ClearedAt);
        Assert.Equal("Received:Missing;Cleared:Parsed;Flags:FutureDate", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Cleared_before_received_preserves_both_timestamps_and_adds_flag()
    {
        var received = new DateTime(2026, 8, 7, 10, 0, 0);
        var cleared = new DateTime(2026, 8, 7, 9, 0, 0);
        var alarm = await ProcessAsync(
            RawRowFactory.DateTimeCell("H", received.Date),
            RawRowFactory.TimeCell("I", received.TimeOfDay),
            RawRowFactory.DateTimeCell("J", cleared.Date),
            RawRowFactory.TimeCell("K", cleared.TimeOfDay));

        Assert.Equal(received, alarm.ReceivedAt);
        Assert.Equal(cleared, alarm.ClearedAt);
        Assert.EndsWith("Flags:ClearedBeforeReceived", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Multiple_flags_have_deterministic_order_and_fit_schema_limit()
    {
        var received = new DateTime(2026, 8, 10, 10, 0, 0);
        var cleared = new DateTime(2026, 8, 9, 9, 0, 0);
        var alarm = await ProcessAsync(
            RawRowFactory.DateTimeCell("H", received.Date),
            RawRowFactory.TimeCell("I", received.TimeOfDay),
            RawRowFactory.DateTimeCell("J", cleared.Date),
            RawRowFactory.TimeCell("K", cleared.TimeOfDay));

        Assert.Equal(
            "Received:Parsed;Cleared:Parsed;Flags:FutureDate,ClearedBeforeReceived",
            alarm.DateTimeParseStatus);
        Assert.True(
            Assert.IsType<string>(alarm.DateTimeParseStatus).Length <=
            ScadaFireAlarmDateTimePolicy.MaximumStatusLength);
    }

    [Theory]
    [InlineData(141, "106 adet alarm")]
    [InlineData(142, "30 adet alarm")]
    [InlineData(143, "71 adet alarm")]
    public async Task Aggregate_source_row_produces_one_event(int sourceRow, string description)
    {
        var decision = await new ScadaAlarmImportProcessor().ProcessAsync(
            Row(sourceRow, RawRowFactory.Text("G", description)),
            TestProfiles.ScadaFireAlarm(),
            CancellationToken.None);

        var alarm = Assert.IsType<ScadaAlarmEvent>(decision.Entity);
        Assert.Equal(ImportRowDisposition.Success, decision.Disposition);
        Assert.Equal(description, alarm.Description);
    }

    [Fact]
    public async Task Only_A_through_N_map_to_core_while_O_S_T_remain_in_raw_lineage()
    {
        var row = Row(
            135,
            RawRowFactory.Text("A", "YANGIN"),
            RawRowFactory.Text("B", "Visible location"),
            RawRowFactory.Text("D", "Visible alarm type"),
            RawRowFactory.Text("E", "Visible intervention"),
            RawRowFactory.Text("G", "Visible description"),
            RawRowFactory.Text("N", "Visible note"),
            RawRowFactory.Text("O", "seneryo devreye girdi"),
            RawRowFactory.Text("S", "Helper intervention"),
            RawRowFactory.Text("T", "Helper alarm type"));

        var decision = await new ScadaAlarmImportProcessor().ProcessAsync(
            row,
            TestProfiles.ScadaFireAlarm(),
            CancellationToken.None);
        var alarm = Assert.IsType<ScadaAlarmEvent>(decision.Entity);

        Assert.Equal("Visible alarm type", alarm.AlarmType);
        Assert.Equal("Visible intervention", alarm.InterventionLevel);
        Assert.Equal("Visible description", alarm.Description);
        Assert.Equal("Visible note", alarm.Note);
        using var raw = JsonDocument.Parse(RawRowSerializer.SerializeValues(row));
        Assert.Equal("seneryo devreye girdi", raw.RootElement.GetProperty("O").GetProperty("RawValue").GetString());
        Assert.Equal("Helper intervention", raw.RootElement.GetProperty("S").GetProperty("RawValue").GetString());
        Assert.Equal("Helper alarm type", raw.RootElement.GetProperty("T").GetProperty("RawValue").GetString());
    }

    [Fact]
    public async Task Helper_only_row_is_ignored_and_cannot_create_an_event()
    {
        var row = new RawExcelRow(
            "YANGIN",
            847,
            new Dictionary<string, RawExcelCell>(StringComparer.OrdinalIgnoreCase)
            {
                ["O"] = RawRowFactory.Text("O", "annotation only"),
                ["S"] = RawRowFactory.Text("S", "helper intervention"),
                ["T"] = RawRowFactory.Text("T", "helper alarm type")
            });

        var decision = await new ScadaAlarmImportProcessor().ProcessAsync(
            row,
            TestProfiles.ScadaFireAlarm(),
            CancellationToken.None);

        Assert.Equal(ImportRowDisposition.Ignore, decision.Disposition);
        Assert.Null(decision.Entity);
    }

    public static TheoryData<RawExcelCell> InvalidClearedDateCases => new()
    {
        RawRowFactory.Number("J", 9_999_999),
        RawRowFactory.Text("J", "not-a-date")
    };

    private static Task<ScadaAlarmEvent> ProcessAsync(params RawExcelCell[] cells) =>
        ProcessAsync(Row(3, cells));

    private static async Task<ScadaAlarmEvent> ProcessAsync(RawExcelRow row)
    {
        var result = await new ScadaAlarmImportProcessor()
            .ProcessAsync(row, TestProfiles.ScadaFireAlarm(), CancellationToken.None);

        Assert.Equal(ImportRowDisposition.Success, result.Disposition);
        return Assert.IsType<ScadaAlarmEvent>(result.Entity);
    }

    private static RawExcelRow Row(int rowNumber, params RawExcelCell[] cells)
    {
        var allCells = new[] { RawRowFactory.Text("G", "Fixture fire alarm") }
            .Concat(cells)
            .GroupBy(cell => cell.Column, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        return new RawExcelRow("YANGIN", rowNumber, allCells);
    }
}
