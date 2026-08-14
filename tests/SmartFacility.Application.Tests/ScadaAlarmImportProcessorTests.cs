using System.Text.Json;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Tests;

public sealed class ScadaAlarmImportProcessorTests
{
    [Fact]
    public async Task Valid_received_date_and_time_populate_received_at()
    {
        var expected = new DateTime(2026, 8, 7, 9, 15, 0);
        var alarm = await ProcessAsync(
            RawRowFactory.Text("G", "Communication alarm"),
            RawRowFactory.DateTimeCell("H", expected.Date),
            RawRowFactory.TimeCell("I", expected.TimeOfDay));

        Assert.Equal(expected, alarm.ReceivedAt);
        Assert.Contains("Received:Parsed", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Valid_cleared_date_and_time_populate_cleared_at()
    {
        var expected = new DateTime(2026, 8, 7, 10, 30, 0);
        var alarm = await ProcessAsync(
            RawRowFactory.Text("G", "Communication alarm"),
            RawRowFactory.DateTimeCell("J", expected.Date),
            RawRowFactory.TimeCell("K", expected.TimeOfDay));

        Assert.Equal(expected, alarm.ClearedAt);
        Assert.Contains("Cleared:Parsed", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Cleared_date_without_time_does_not_produce_cleared_at()
    {
        var row = RawRowFactory.Row(
            "SCADA",
            2,
            RawRowFactory.Text("G", "Communication alarm"),
            RawRowFactory.DateTimeCell("J", new DateTime(2026, 8, 7)));

        var alarm = await ProcessAsync(row);

        Assert.Null(alarm.ClearedAt);

        using var rawData = JsonDocument.Parse(RawRowSerializer.SerializeValues(row));
        Assert.True(rawData.RootElement.TryGetProperty("J", out var rawDate));
        Assert.Equal("DateTime", rawDate.GetProperty("DataType").GetString());
        Assert.False(rawData.RootElement.TryGetProperty("K", out _));
    }

    [Fact]
    public async Task Cleared_date_without_time_has_explicit_date_only_source_status()
    {
        var alarm = await ProcessAsync(
            RawRowFactory.Text("G", "Communication alarm"),
            RawRowFactory.DateTimeCell("J", new DateTime(2026, 8, 7)));

        Assert.Equal("Received:Missing;Cleared:DateOnlySource", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Missing_cleared_date_and_time_keep_alarm_with_null_cleared_at()
    {
        var alarm = await ProcessAsync(RawRowFactory.Text("G", "Communication alarm"));

        Assert.Null(alarm.ClearedAt);
        Assert.Equal("Received:Missing;Cleared:Missing", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Invalid_cleared_time_keeps_alarm_with_null_cleared_at()
    {
        var alarm = await ProcessAsync(
            RawRowFactory.Text("G", "Communication alarm"),
            RawRowFactory.DateTimeCell("J", new DateTime(2026, 8, 7)),
            RawRowFactory.Text("K", "invalid-time"));

        Assert.Null(alarm.ClearedAt);
        Assert.Contains("Cleared:InvalidTime", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Invalid_received_time_does_not_discard_alarm()
    {
        var profile = TestProfiles.ScadaAlarm();
        var processor = new ScadaAlarmImportProcessor();
        var row = RawRowFactory.Row(
            "SCADA",
            2,
            RawRowFactory.Text("G", "Communication alarm"),
            RawRowFactory.Number("H", 45248),
            RawRowFactory.Text("I", "15.:15"));

        var result = await processor.ProcessAsync(row, profile, CancellationToken.None);
        var alarm = Assert.IsType<ScadaAlarmEvent>(result.Entity);

        Assert.Equal(ImportRowDisposition.Success, result.Disposition);
        Assert.Null(alarm.ReceivedAt);
        Assert.Contains("Received:InvalidTime", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Date_parse_problem_does_not_discard_other_alarm_fields()
    {
        var alarm = await ProcessAsync(
            RawRowFactory.Text("A", "MEKANİK"),
            RawRowFactory.Text("B", "Fixture location"),
            RawRowFactory.Text("D", "BASINÇ"),
            RawRowFactory.Text("G", "Communication alarm"),
            RawRowFactory.DateTimeCell("H", new DateTime(2026, 8, 7)),
            RawRowFactory.Text("I", "invalid-time"),
            RawRowFactory.Text("M", "ACTIVE"),
            RawRowFactory.Text("N", "Fixture note"));

        Assert.Null(alarm.ReceivedAt);
        Assert.Equal("MEKANİK", alarm.SectionRaw);
        Assert.Equal("Fixture location", alarm.LocationRaw);
        Assert.Equal("BASINÇ", alarm.AlarmType);
        Assert.Equal("Communication alarm", alarm.Description);
        Assert.Equal("ACTIVE", alarm.StatusRaw);
        Assert.Equal("Fixture note", alarm.Note);
    }

    private static Task<ScadaAlarmEvent> ProcessAsync(params RawExcelCell[] cells) =>
        ProcessAsync(RawRowFactory.Row("SCADA", 2, cells));

    private static async Task<ScadaAlarmEvent> ProcessAsync(RawExcelRow row)
    {
        var result = await new ScadaAlarmImportProcessor()
            .ProcessAsync(row, TestProfiles.ScadaAlarm(), CancellationToken.None);

        Assert.Equal(ImportRowDisposition.Success, result.Disposition);
        return Assert.IsType<ScadaAlarmEvent>(result.Entity);
    }
}
