using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Tests.TestData;

internal static class RawRowFactory
{
    public static RawExcelRow Row(
        string sheet,
        int rowNumber,
        params RawExcelCell[] cells) =>
        new(
            sheet,
            rowNumber,
            cells.ToDictionary(cell => cell.Column, StringComparer.OrdinalIgnoreCase));

    public static RawExcelCell Text(string column, string? value) =>
        new(column, value, value, "Text", null, null, null, null);

    public static RawExcelCell Number(string column, double value) =>
        new(
            column,
            value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "Number",
            value,
            null,
            null,
            null);

    public static RawExcelCell DateTimeCell(string column, DateTime value) =>
        new(
            column,
            value.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            value.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            "DateTime",
            null,
            value,
            null,
            null);

    public static RawExcelCell TimeCell(string column, TimeSpan value) =>
        new(
            column,
            value.ToString("c", System.Globalization.CultureInfo.InvariantCulture),
            value.ToString("c", System.Globalization.CultureInfo.InvariantCulture),
            "TimeSpan",
            null,
            null,
            value,
            null);
}
