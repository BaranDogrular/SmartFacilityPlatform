using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Abstractions;

public interface IExcelWorkbookReader
{
    IAsyncEnumerable<RawExcelRow> ReadRowsAsync(
        ExcelReadRequest request,
        CancellationToken cancellationToken = default);
}
