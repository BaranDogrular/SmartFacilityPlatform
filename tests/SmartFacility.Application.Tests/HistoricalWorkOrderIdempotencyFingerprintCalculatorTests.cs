using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;

namespace SmartFacility.Application.Tests;

public sealed class HistoricalWorkOrderIdempotencyFingerprintCalculatorTests
{
    private const string SourceType = ImportSourceTypes.HistoricalWorkOrder;

    [Fact]
    public void Same_logical_row_produces_same_fingerprint()
    {
        var first = CreateRow();
        var repeated = CreateRow();

        Assert.Equal(Calculate(first), Calculate(repeated));
    }

    [Fact]
    public void Source_row_number_does_not_change_fingerprint()
    {
        var first = CreateRow(rowNumber: 2);
        var moved = CreateRow(rowNumber: 500);

        Assert.Equal(Calculate(first), Calculate(moved));
    }

    [Fact]
    public void Excluded_q_floating_point_difference_does_not_change_fingerprint()
    {
        var original = CreateRow(qValue: "0.4166666666666667");
        var resaved = CreateRow(qValue: "0.41666666666667003");

        Assert.Equal(Calculate(original), Calculate(resaved));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("C")]
    [InlineData("D")]
    [InlineData("E")]
    [InlineData("K")]
    [InlineData("M")]
    [InlineData("P")]
    public void Included_column_change_changes_fingerprint(string changedColumn)
    {
        var original = CreateRow();
        var changed = CreateRow(changedColumn: changedColumn);

        Assert.NotEqual(Calculate(original), Calculate(changed));
    }

    [Fact]
    public void Source_sheet_change_changes_fingerprint()
    {
        var original = CreateRow(sheetName: "Toplam İş Emri");
        var changed = CreateRow(sheetName: "Başka Sayfa");

        Assert.NotEqual(Calculate(original), Calculate(changed));
    }

    [Fact]
    public void Source_type_change_changes_fingerprint()
    {
        var row = CreateRow();

        Assert.NotEqual(Calculate(row), Calculate(row, "OtherSource"));
    }

    [Fact]
    public void Whitespace_and_case_normalization_is_deterministic()
    {
        var original = CreateRow();
        var normalizedEquivalent = CreateRow(
            sourceReference: "  tim-100  ",
            location: "  a   blok ",
            personnel: "  person   one ",
            discipline: " mechanical ",
            description: "  water   leak  ");

        Assert.Equal(Calculate(original), Calculate(normalizedEquivalent));
    }

    [Fact]
    public void Null_missing_and_blank_selected_values_are_equivalent()
    {
        var missing = CreateRow(includePersonnel: false);
        var blank = CreateRow(personnel: "   ");

        Assert.Equal(Calculate(missing), Calculate(blank));
    }

    private static string Calculate(RawExcelRow row, string sourceType = SourceType) =>
        HistoricalWorkOrderIdempotencyFingerprintCalculator.Calculate(sourceType, row);

    private static RawExcelRow CreateRow(
        string sheetName = "Toplam İş Emri",
        int rowNumber = 2,
        string sourceReference = "TIM-100",
        string location = "A BLOK",
        string personnel = "PERSON ONE",
        string discipline = "MECHANICAL",
        string description = "WATER LEAK",
        string qValue = "0.4166666666666667",
        string? changedColumn = null,
        bool includePersonnel = true)
    {
        var values = new Dictionary<string, string?>
        {
            ["A"] = sourceReference,
            ["C"] = location,
            ["D"] = personnel,
            ["E"] = "2026-08-01T10:00:00.0000000",
            ["K"] = discipline,
            ["M"] = description,
            ["P"] = "10",
            ["Q"] = qValue
        };

        if (!includePersonnel)
        {
            values.Remove("D");
        }

        if (changedColumn is not null)
        {
            values[changedColumn] = $"{values[changedColumn]}-CHANGED";
        }

        return RawRowFactory.Row(
            sheetName,
            rowNumber,
            values.Select(pair => RawRowFactory.Text(pair.Key, pair.Value)).ToArray());
    }
}
