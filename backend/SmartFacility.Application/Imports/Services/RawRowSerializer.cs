using System.Text.Json;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public static class RawRowSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    public static string SerializeValues(RawExcelRow row)
    {
        var values = row.Cells.ToDictionary(
            pair => pair.Key,
            pair => new
            {
                pair.Value.RawValue,
                pair.Value.FormattedValue,
                pair.Value.DataType,
                pair.Value.NumericValue,
                pair.Value.DateTimeValue,
                pair.Value.TimeSpanValue
            },
            StringComparer.OrdinalIgnoreCase);

        return JsonSerializer.Serialize(values, SerializerOptions);
    }

    public static string? SerializeFormulas(RawExcelRow row)
    {
        var formulas = row.Cells
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value.Formula))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Formula, StringComparer.OrdinalIgnoreCase);

        return formulas.Count == 0
            ? null
            : JsonSerializer.Serialize(formulas, SerializerOptions);
    }
}
