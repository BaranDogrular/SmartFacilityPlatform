using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public static class ProfileHeaderValidator
{
    public static IReadOnlyList<string> Validate(
        RawExcelRow headerRow,
        WorksheetProfileOptions worksheet)
    {
        var errors = new List<string>();

        foreach (var expected in worksheet.ExpectedHeaders)
        {
            var actual = ImportValueNormalizer.Normalize(headerRow.GetCell(expected.Key)?.RawValue);
            var expectedValue = ImportValueNormalizer.Normalize(expected.Value);

            if (!string.Equals(actual, expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Column {expected.Key} header does not match the configured profile.");
            }
        }

        return errors;
    }
}
