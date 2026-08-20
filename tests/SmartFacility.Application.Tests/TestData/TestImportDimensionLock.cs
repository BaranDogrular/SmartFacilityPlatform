using System.Collections.Concurrent;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Services;

namespace SmartFacility.Application.Tests.TestData;

internal sealed class TestImportDimensionLock : IImportDimensionLock
{
    private readonly ConcurrentBag<string> _acquiredKeys = [];
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> AcquiredKeys => _acquiredKeys.ToArray();

    public async Task<IAsyncDisposable> AcquireAsync(
        string dimensionName,
        string? identityPart1,
        string? identityPart2,
        string? identityPart3,
        CancellationToken cancellationToken)
    {
        var key = string.Join(
            '|',
            ImportValueNormalizer.NormalizeForComparison(dimensionName),
            ImportValueNormalizer.NormalizeForComparison(identityPart1),
            ImportValueNormalizer.NormalizeForComparison(identityPart2),
            ImportValueNormalizer.NormalizeForComparison(identityPart3));
        _acquiredKeys.Add(key);
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
