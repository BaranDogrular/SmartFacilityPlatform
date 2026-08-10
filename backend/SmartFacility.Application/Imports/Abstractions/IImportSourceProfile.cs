using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Abstractions;

public interface IImportSourceProfile
{
    string Key { get; }
    string SourceType { get; }
    IReadOnlyList<WorksheetProfileOptions> Worksheets { get; }
    IReadOnlyDictionary<string, string> Columns { get; }
    IReadOnlySet<string> RequiredFields { get; }

    RawExcelCell? GetCell(RawExcelRow row, string fieldName);
    WorksheetProfileOptions GetWorksheet(string sheetName);
}
