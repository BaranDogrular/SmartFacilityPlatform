using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Tests.TestData;

internal sealed class FakeWorkbookReader(IReadOnlyList<RawExcelRow> rows) : IExcelWorkbookReader
{
    public async IAsyncEnumerable<RawExcelRow> ReadRowsAsync(
        ExcelReadRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
            await Task.Yield();
        }
    }
}
