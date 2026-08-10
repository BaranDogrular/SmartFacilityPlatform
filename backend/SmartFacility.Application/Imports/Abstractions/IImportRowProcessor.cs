using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Abstractions;

public interface IImportRowProcessor
{
    string ProfileKey { get; }

    Task<ImportRowDecision> ProcessAsync(
        RawExcelRow row,
        IImportSourceProfile profile,
        CancellationToken cancellationToken);
}
