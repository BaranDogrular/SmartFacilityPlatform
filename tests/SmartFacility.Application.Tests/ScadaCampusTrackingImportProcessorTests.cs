using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Tests;

public sealed class ScadaCampusTrackingImportProcessorTests
{
    [Fact]
    public async Task Valid_received_and_cleared_timestamps_are_parsed()
    {
        var received = new DateTime(2026, 8, 7, 8, 30, 0);
        var cleared = new DateTime(2026, 8, 7, 9, 15, 0);

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
    public async Task Dash_received_time_is_invalid_and_does_not_invent_midnight()
    {
        var alarm = await ProcessAsync(
            RawRowFactory.DateTimeCell("H", new DateTime(2026, 8, 7)),
            RawRowFactory.Text("I", "-"));

        Assert.Null(alarm.ReceivedAt);
        Assert.Equal("Received:InvalidTime;Cleared:Missing", alarm.DateTimeParseStatus);
    }

    [Theory]
    [InlineData("26:05:2022")]
    [InlineData("26:07:2022")]
    public async Task Malformed_text_date_is_invalid_and_not_silently_corrected(string rawDate)
    {
        var alarm = await ProcessAsync(
            RawRowFactory.Text("H", rawDate),
            RawRowFactory.Text("I", "08:30"));

        Assert.Null(alarm.ReceivedAt);
        Assert.Equal("Received:InvalidDate;Cleared:Missing", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Cleared_date_without_time_is_date_only_and_null()
    {
        var alarm = await ProcessAsync(
            RawRowFactory.DateTimeCell("J", new DateTime(2026, 8, 7)));

        Assert.Null(alarm.ClearedAt);
        Assert.Equal("Received:Missing;Cleared:DateOnlySource", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Missing_cleared_timestamp_is_null_and_explicit()
    {
        var alarm = await ProcessAsync(
            RawRowFactory.DateTimeCell("H", new DateTime(2026, 8, 7)),
            RawRowFactory.TimeCell("I", new TimeSpan(8, 30, 0)));

        Assert.Null(alarm.ClearedAt);
        Assert.Equal("Received:Parsed;Cleared:Missing", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Suspicious_year_is_null_and_explicit()
    {
        var alarm = await ProcessAsync(
            RawRowFactory.Text("H", "21.07.0203"),
            RawRowFactory.Text("I", "08:30"));

        Assert.Null(alarm.ReceivedAt);
        Assert.Equal("Received:SuspiciousYear;Cleared:Missing", alarm.DateTimeParseStatus);
    }

    [Fact]
    public async Task Safe_quality_flags_preserve_source_timestamps()
    {
        var alarm = await ProcessAsync(
            RawRowFactory.DateTimeCell("H", new DateTime(2026, 8, 8)),
            RawRowFactory.TimeCell("I", new TimeSpan(10, 0, 0)),
            RawRowFactory.DateTimeCell("J", new DateTime(2026, 8, 8)),
            RawRowFactory.TimeCell("K", new TimeSpan(9, 0, 0)));

        Assert.Equal(new DateTime(2026, 8, 8, 10, 0, 0), alarm.ReceivedAt);
        Assert.Equal(new DateTime(2026, 8, 8, 9, 0, 0), alarm.ClearedAt);
        Assert.Equal(
            "Received:Parsed;Cleared:Parsed;Flags:FutureDate,ClearedBeforeReceived",
            alarm.DateTimeParseStatus);
        Assert.DoesNotContain("LongDuration", alarm.DateTimeParseStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Helper_columns_do_not_change_core_mapping()
    {
        var withoutHelpers = await ProcessAsync(
            RawRowFactory.Text("A", "KAMPÜS"),
            RawRowFactory.Text("N", "Source note"));
        var withHelpers = await ProcessAsync(
            RawRowFactory.Text("A", "KAMPÜS"),
            RawRowFactory.Text("N", "Source note"),
            RawRowFactory.Text("S", "Helper intervention"),
            RawRowFactory.Text("T", "Helper alarm type"),
            RawRowFactory.Text("U", "Hidden helper"));

        Assert.Equal(withoutHelpers.SectionRaw, withHelpers.SectionRaw);
        Assert.Equal(withoutHelpers.Description, withHelpers.Description);
        Assert.Equal(withoutHelpers.Note, withHelpers.Note);
        Assert.Equal(withoutHelpers.DateTimeParseStatus, withHelpers.DateTimeParseStatus);
    }

    private static async Task<ScadaAlarmEvent> ProcessAsync(params RawExcelCell[] overrides)
    {
        RawExcelCell[] defaults =
        [
            RawRowFactory.Text("G", "Campus tracking alarm")
        ];
        var cells = defaults
            .Concat(overrides)
            .GroupBy(cell => cell.Column, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var row = new RawExcelRow("KAMPÜS TAKİP", 3, cells);

        var decision = await new ScadaAlarmImportProcessor().ProcessAsync(
            row,
            TestProfiles.ScadaCampusTracking(),
            CancellationToken.None);

        Assert.Equal(ImportRowDisposition.Success, decision.Disposition);
        var alarm = Assert.IsType<ScadaAlarmEvent>(decision.Entity);
        Assert.Equal("KAMPÜS TAKİP", alarm.SourceSheet);
        return alarm;
    }
}
