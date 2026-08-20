namespace SmartFacility.Application.Imports.Abstractions;

public interface IImportDimensionLock
{
    Task<IAsyncDisposable> AcquireAsync(
        string dimensionName,
        string? identityPart1,
        string? identityPart2,
        string? identityPart3,
        CancellationToken cancellationToken);
}
