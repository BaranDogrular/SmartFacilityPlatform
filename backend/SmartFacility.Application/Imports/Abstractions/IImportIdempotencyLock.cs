namespace SmartFacility.Application.Imports.Abstractions;

public interface IImportIdempotencyLock
{
    Task<IAsyncDisposable> AcquireAsync(
        string sourceType,
        string sourceSheet,
        string? fingerprintAlgorithm,
        string fingerprint,
        CancellationToken cancellationToken);
}
