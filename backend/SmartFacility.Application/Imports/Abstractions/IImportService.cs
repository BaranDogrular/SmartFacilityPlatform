using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Abstractions;

public interface IImportService
{
    Task<ImportResult> ImportAsync(
        ImportRequest request,
        CancellationToken cancellationToken = default);
}
