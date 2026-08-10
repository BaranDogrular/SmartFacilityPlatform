using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Profiles;

public abstract class ConfiguredImportSourceProfile : IImportSourceProfile
{
    protected ConfiguredImportSourceProfile(string key, ImportProfileOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.SourceType))
        {
            throw new InvalidOperationException($"Import profile '{key}' has no SourceType.");
        }

        if (options.Worksheets.Count == 0)
        {
            throw new InvalidOperationException($"Import profile '{key}' has no worksheet definition.");
        }

        Key = key;
        SourceType = options.SourceType;
        Worksheets = options.Worksheets;
        Columns = new Dictionary<string, string>(options.Columns, StringComparer.OrdinalIgnoreCase);
        RequiredFields = options.RequiredFields.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public string Key { get; }
    public string SourceType { get; }
    public IReadOnlyList<WorksheetProfileOptions> Worksheets { get; }
    public IReadOnlyDictionary<string, string> Columns { get; }
    public IReadOnlySet<string> RequiredFields { get; }

    public RawExcelCell? GetCell(RawExcelRow row, string fieldName)
    {
        if (!Columns.TryGetValue(fieldName, out var column))
        {
            return null;
        }

        return row.GetCell(column);
    }

    public WorksheetProfileOptions GetWorksheet(string sheetName) =>
        Worksheets.FirstOrDefault(
            worksheet => string.Equals(worksheet.Name, sheetName, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(
            $"Worksheet '{sheetName}' is not configured for profile '{Key}'.");
}
