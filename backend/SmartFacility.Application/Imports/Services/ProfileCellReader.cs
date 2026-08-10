using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

internal static class ProfileCellReader
{
    public static string? Text(
        IImportSourceProfile profile,
        RawExcelRow row,
        string fieldName) =>
        ImportValueNormalizer.Normalize(profile.GetCell(row, fieldName)?.RawValue);
}
