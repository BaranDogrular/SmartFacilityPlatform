namespace SmartFacility.Application.Imports.Models;

public sealed class ImportProfileOptions
{
    public string SourceType { get; set; } = string.Empty;
    public List<WorksheetProfileOptions> Worksheets { get; set; } = [];
    public Dictionary<string, string> Columns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> RequiredFields { get; set; } = [];
}

public sealed class WorksheetProfileOptions
{
    public string Name { get; set; } = string.Empty;
    public int HeaderRowNumber { get; set; } = 1;
    public int FirstDataRowNumber { get; set; } = 2;
    public DateTime? ReferenceDate { get; set; }
    public Dictionary<string, string> ExpectedHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
