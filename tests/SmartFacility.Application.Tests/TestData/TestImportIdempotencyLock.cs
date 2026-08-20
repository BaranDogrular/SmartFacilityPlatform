using System.Collections.Concurrent;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Services;

namespace SmartFacility.Application.Tests.TestData;

internal sealed class TestImportIdempotencyLock : IImportIdempotencyLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public async Task<IAsyncDisposable> AcquireAsync(
        string sourceType,
        string sourceSheet,
        string? fingerprintAlgorithm,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var key = string.Join(
            '|',
            ImportValueNormalizer.NormalizeForComparison(sourceType),
            ImportValueNormalizer.NormalizeForComparison(sourceSheet),
            ImportValueNormalizer.NormalizeForComparison(fingerprintAlgorithm) ?? "ROW",
            ImportValueNormalizer.NormalizeForComparison(fingerprint));
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Lease(semaphore);
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
