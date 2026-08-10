using System.Globalization;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public static class ExcelValueParser
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static ParsedDateTime ParseDate(RawExcelCell? cell, bool treatOneAsNull = false)
    {
        if (IsMissing(cell))
        {
            return new(null, "Missing");
        }

        if (treatOneAsNull && IsSentinelOne(cell!))
        {
            return new(null, "SentinelNull");
        }

        if (cell!.DateTimeValue is { } dateTime)
        {
            if (treatOneAsNull && dateTime.Date == new DateTime(1900, 1, 1))
            {
                return new(null, "SentinelNull");
            }

            return new(dateTime, "Parsed");
        }

        if (cell.NumericValue is > 1 and < 2958466)
        {
            try
            {
                return new(DateTime.FromOADate(cell.NumericValue.Value), "Parsed");
            }
            catch (ArgumentException)
            {
                return new(null, "Invalid");
            }
        }

        var value = ImportValueNormalizer.Normalize(cell.RawValue);
        if (DateTime.TryParse(value, TurkishCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed) ||
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
        {
            return new(parsed, "Parsed");
        }

        return new(null, "Invalid");
    }

    public static ParsedDateTime CombineDateAndTime(RawExcelCell? dateCell, RawExcelCell? timeCell)
    {
        var date = ParseDate(dateCell);
        if (date.Status == "Missing")
        {
            return new(null, IsMissing(timeCell) ? "Missing" : "InvalidDate");
        }

        if (date.Value is null)
        {
            return new(null, "InvalidDate");
        }

        if (IsMissing(timeCell))
        {
            return new(date.Value.Value.Date, "ParsedDateOnly");
        }

        if (!TryParseTime(timeCell!, out var time))
        {
            return new(null, "InvalidTime");
        }

        return new(date.Value.Value.Date.Add(time), "Parsed");
    }

    private static bool TryParseTime(RawExcelCell cell, out TimeSpan time)
    {
        if (cell.TimeSpanValue is { } timeSpan && timeSpan >= TimeSpan.Zero && timeSpan < TimeSpan.FromDays(1))
        {
            time = timeSpan;
            return true;
        }

        if (cell.DateTimeValue is { } dateTime)
        {
            time = dateTime.TimeOfDay;
            return true;
        }

        if (cell.NumericValue is >= 0 and < 1)
        {
            time = TimeSpan.FromDays(cell.NumericValue.Value);
            return true;
        }

        var value = ImportValueNormalizer.Normalize(cell.RawValue);
        if (TimeSpan.TryParse(value, TurkishCulture, out time) ||
            TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out time))
        {
            return time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);
        }

        time = default;
        return false;
    }

    private static bool IsSentinelOne(RawExcelCell cell) =>
        cell.NumericValue is { } numeric && Math.Abs(numeric - 1d) < double.Epsilon ||
        string.Equals(ImportValueNormalizer.Normalize(cell.RawValue), "1", StringComparison.Ordinal);

    private static bool IsMissing(RawExcelCell? cell) =>
        cell is null || string.IsNullOrWhiteSpace(cell.RawValue);
}

public sealed record ParsedDateTime(DateTime? Value, string Status);
