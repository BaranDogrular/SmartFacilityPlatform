namespace SmartFacility.Application.Imports.Models;

public sealed record RawExcelCell(
    string Column,
    string? RawValue,
    string? FormattedValue,
    string DataType,
    double? NumericValue,
    DateTime? DateTimeValue,
    TimeSpan? TimeSpanValue,
    string? Formula);
