using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;

namespace SmartFacility.Application.Tests;

public sealed class ScadaOutageIdempotencyFingerprintCalculatorTests
{
    private const string SourceType = ImportSourceTypes.ScadaOutage;

    [Fact]
    public void Duration_Number_and_TimeSpan_representations_have_same_business_fingerprint()
    {
        var numberDuration = CreateRow(duration: RawRowFactory.Number("I", 0.5));
        var timeSpanDuration = CreateRow(duration: RawRowFactory.TimeCell("I", TimeSpan.FromHours(12)));

        Assert.Equal(Calculate(numberDuration), Calculate(timeSpanDuration));
        Assert.NotEqual(
            RowFingerprintCalculator.Calculate(SourceType, numberDuration),
            RowFingerprintCalculator.Calculate(SourceType, timeSpanDuration));
    }

    [Theory]
    [InlineData("DurationRaw")]
    [InlineData("RestoredAt")]
    [InlineData("StatusRaw")]
    public void Non_identity_field_change_does_not_change_fingerprint(string field)
    {
        var original = CreateRow();
        var changed = field switch
        {
            "DurationRaw" => CreateRow(duration: RawRowFactory.Text("I", "changed")),
            "RestoredAt" => CreateRow(
                restoredDate: RawRowFactory.DateTimeCell("F", new DateTime(2026, 8, 3)),
                restoredTime: RawRowFactory.TimeCell("G", new TimeSpan(8, 0, 0))),
            "StatusRaw" => CreateRow(status: "Changed"),
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        Assert.Equal(Calculate(original), Calculate(changed));
    }

    [Theory]
    [InlineData("StartedAt")]
    [InlineData("Reason")]
    [InlineData("Description")]
    public void Identity_field_change_changes_fingerprint(string field)
    {
        var original = CreateRow();
        var changed = field switch
        {
            "StartedAt" => CreateRow(startedTime: RawRowFactory.TimeCell("E", new TimeSpan(11, 0, 0))),
            "Reason" => CreateRow(reason: "Different reason"),
            "Description" => CreateRow(description: "Different description"),
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        Assert.NotEqual(Calculate(original), Calculate(changed));
    }

    [Fact]
    public void Source_row_number_does_not_change_fingerprint()
    {
        Assert.Equal(Calculate(CreateRow(rowNumber: 2)), Calculate(CreateRow(rowNumber: 500)));
    }

    [Fact]
    public void Semantically_equal_started_time_representations_have_same_fingerprint()
    {
        var timeSpan = CreateRow(startedTime: RawRowFactory.TimeCell("E", TimeSpan.FromHours(10)));
        var number = CreateRow(startedTime: RawRowFactory.Number("E", 10d / 24d));

        Assert.Equal(Calculate(timeSpan), Calculate(number));
    }

    [Fact]
    public void Invalid_started_at_fallback_is_deterministic_and_uses_raw_values()
    {
        var first = CreateRow(startedDate: RawRowFactory.Text("D", "invalid-date"));
        var repeated = CreateRow(
            rowNumber: 100,
            startedDate: RawRowFactory.Text("D", "  INVALID-DATE  "));
        var changedRawDate = CreateRow(startedDate: RawRowFactory.Text("D", "another-invalid-date"));

        Assert.Equal(Calculate(first), Calculate(repeated));
        Assert.NotEqual(Calculate(first), Calculate(changedRawDate));
    }

    [Fact]
    public void Missing_and_invalid_started_at_have_distinct_deterministic_fallbacks()
    {
        var missing = CreateRow(includeStartedAt: false);
        var repeatedMissing = CreateRow(rowNumber: 20, includeStartedAt: false);
        var invalid = CreateRow(startedDate: null, startedTime: RawRowFactory.Text("E", "invalid-time"));

        Assert.Equal(Calculate(missing), Calculate(repeatedMissing));
        Assert.NotEqual(Calculate(missing), Calculate(invalid));
    }

    [Fact]
    public void Whitespace_and_case_normalization_is_deterministic()
    {
        var original = CreateRow(reason: "Power Failure", description: "Main Feed");
        var normalizedEquivalent = CreateRow(
            reason: "  power   failure ",
            description: " main   feed ");

        Assert.Equal(Calculate(original), Calculate(normalizedEquivalent));
    }

    private static string Calculate(RawExcelRow row) =>
        ScadaOutageIdempotencyFingerprintCalculator.Calculate(SourceType, row);

    private static RawExcelRow CreateRow(
        int rowNumber = 2,
        string reason = "Power interruption",
        string description = "Main feed",
        RawExcelCell? startedDate = null,
        RawExcelCell? startedTime = null,
        bool includeStartedAt = true,
        RawExcelCell? restoredDate = null,
        RawExcelCell? restoredTime = null,
        string status = "Completed",
        RawExcelCell? duration = null)
    {
        var cells = new List<RawExcelCell>
        {
            RawRowFactory.Text("B", reason),
            RawRowFactory.Text("C", description),
            restoredDate ?? RawRowFactory.DateTimeCell("F", new DateTime(2026, 8, 1)),
            restoredTime ?? RawRowFactory.TimeCell("G", new TimeSpan(12, 0, 0)),
            RawRowFactory.Text("H", status),
            duration ?? RawRowFactory.TimeCell("I", TimeSpan.FromHours(2))
        };

        if (includeStartedAt)
        {
            cells.Add(startedDate ?? RawRowFactory.DateTimeCell("D", new DateTime(2026, 8, 1)));
            cells.Add(startedTime ?? RawRowFactory.TimeCell("E", new TimeSpan(10, 0, 0)));
        }

        return RawRowFactory.Row("SCADA SÜREKLİLİK", rowNumber, [.. cells]);
    }
}
