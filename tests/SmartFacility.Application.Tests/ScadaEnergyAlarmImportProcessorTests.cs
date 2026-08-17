using System.Text.Json;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Tests;

public sealed class ScadaEnergyAlarmImportProcessorTests
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
    public async Task Received_date_only_is_null_and_does_not_invent_midnight()
    {
        var alarm = await ProcessAsync(
            RawRowFactory.DateTimeCell("H", new DateTime(2026, 8, 7)));

        Assert.Null(alarm.ReceivedAt);
        Assert.Equal("Received:DateOnlySource;Cleared:Missing", alarm.DateTimeParseStatus);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("21.07.0203")]
    public async Task Invalid_received_date_is_null(string rawDate)
    {
        var alarm = await ProcessAsync(
            RawRowFactory.Text("H", rawDate),
            RawRowFactory.Text("I", "08:30"));

        Assert.Null(alarm.ReceivedAt);
        Assert.Equal("Received:InvalidDate;Cleared:Missing", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Invalid_received_time_is_null_without_discarding_other_fields()
    {
        var alarm = await ProcessAsync(
            RawRowFactory.Text("A", "ENERJİ"),
            RawRowFactory.Text("B", "Fixture location"),
            RawRowFactory.Text("D", "ENERJİ ALARMI"),
            RawRowFactory.DateTimeCell("H", new DateTime(2026, 8, 7)),
            RawRowFactory.Text("I", "15.:15"),
            RawRowFactory.Text("M", "ACTIVE"));

        Assert.Null(alarm.ReceivedAt);
        Assert.Equal("ENERJİ", alarm.SectionRaw);
        Assert.Equal("Fixture location", alarm.LocationRaw);
        Assert.Equal("ENERJİ ALARMI", alarm.AlarmType);
        Assert.Equal("ACTIVE", alarm.StatusRaw);
        Assert.Equal("Received:InvalidTime;Cleared:Missing", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Missing_received_and_cleared_timestamps_remain_null()
    {
        var alarm = await ProcessAsync();

        Assert.Null(alarm.ReceivedAt);
        Assert.Null(alarm.ClearedAt);
        Assert.Equal("Received:Missing;Cleared:Missing", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Cleared_date_only_is_null()
    {
        var alarm = await ProcessAsync(
            RawRowFactory.DateTimeCell("J", new DateTime(2026, 8, 7)));

        Assert.Null(alarm.ClearedAt);
        Assert.Equal("Received:Missing;Cleared:DateOnlySource", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Invalid_cleared_date_is_null()
    {
        var alarm = await ProcessAsync(
            RawRowFactory.Text("J", "not-a-date"),
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

    [Theory]
    [InlineData("J")]
    [InlineData("K")]
    public async Task Cleared_placeholder_x_is_null_and_explicit(string column)
    {
        var alarm = await ProcessAsync(RawRowFactory.Text(column, "x"));

        Assert.Null(alarm.ClearedAt);
        Assert.Equal("Received:Missing;Cleared:PlaceholderX", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Cleared_before_received_preserves_values_and_adds_quality_flag()
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
        Assert.Equal(
            "Received:Parsed;Cleared:Parsed;Flags:ClearedBeforeReceived",
            alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Future_date_is_preserved_and_flagged()
    {
        var future = new DateTime(2026, 8, 8, 9, 0, 0);
        var alarm = await ProcessAsync(
            RawRowFactory.DateTimeCell("H", future.Date),
            RawRowFactory.TimeCell("I", future.TimeOfDay));

        Assert.Equal(future, alarm.ReceivedAt);
        Assert.Equal(
            "Received:Parsed;Cleared:Missing;Flags:FutureDate",
            alarm.DateTimeParseStatus);
        Assert.True(
            Assert.IsType<string>(alarm.DateTimeParseStatus).Length <=
            ScadaEnergyAlarmDateTimePolicy.MaximumStatusLength);
    }

    [Fact]
    public async Task Only_A_through_N_map_to_entity_while_helpers_remain_raw_lineage()
    {
        var row = Row(
            3,
            RawRowFactory.Text("D", "Visible alarm type"),
            RawRowFactory.Text("E", "Visible intervention"),
            RawRowFactory.Text("N", "Visible note"),
            RawRowFactory.Text("S", "Helper intervention"),
            RawRowFactory.Text("T", "Helper alarm type"));

        var alarm = await ProcessAsync(row);

        Assert.Equal("Visible alarm type", alarm.AlarmType);
        Assert.Equal("Visible intervention", alarm.InterventionLevel);
        Assert.Equal("Visible note", alarm.Note);
        using var raw = JsonDocument.Parse(RawRowSerializer.SerializeValues(row));
        Assert.Equal("Helper intervention", raw.RootElement.GetProperty("S").GetProperty("RawValue").GetString());
        Assert.Equal("Helper alarm type", raw.RootElement.GetProperty("T").GetProperty("RawValue").GetString());
    }

    [Fact]
    public async Task Helper_only_row_is_ignored()
    {
        var row = RawRowFactory.Row(
            "ENERJİ",
            377,
            RawRowFactory.Text("S", "helper intervention"),
            RawRowFactory.Text("T", "helper alarm type"));

        var decision = await new ScadaAlarmImportProcessor().ProcessAsync(
            row,
            TestProfiles.ScadaEnergyAlarm(),
            CancellationToken.None);

        Assert.Equal(ImportRowDisposition.Ignore, decision.Disposition);
        Assert.Null(decision.Entity);
    }

    private static Task<ScadaAlarmEvent> ProcessAsync(params RawExcelCell[] cells) =>
        ProcessAsync(Row(3, cells));

    private static async Task<ScadaAlarmEvent> ProcessAsync(RawExcelRow row)
    {
        var decision = await new ScadaAlarmImportProcessor().ProcessAsync(
            row,
            TestProfiles.ScadaEnergyAlarm(),
            CancellationToken.None);

        Assert.Equal(ImportRowDisposition.Success, decision.Disposition);
        return Assert.IsType<ScadaAlarmEvent>(decision.Entity);
    }

    private static RawExcelRow Row(int rowNumber, params RawExcelCell[] cells)
    {
        var allCells = new[] { RawRowFactory.Text("G", "Fixture energy alarm") }
            .Concat(cells)
            .GroupBy(cell => cell.Column, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        return new RawExcelRow("ENERJİ", rowNumber, allCells);
    }
}
