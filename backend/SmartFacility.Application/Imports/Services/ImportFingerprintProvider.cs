using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public sealed class ImportFingerprintProvider : IImportFingerprintProvider
{
    public string? GetIdempotencyAlgorithm(string sourceType) =>
        IsHistoricalWorkOrder(sourceType)
            ? HistoricalWorkOrderIdempotencyFingerprintCalculator.Algorithm
            : null;

    public ImportRowFingerprints Calculate(string sourceType, RawExcelRow row)
    {
        var rowFingerprint = RowFingerprintCalculator.Calculate(sourceType, row);
        if (!IsHistoricalWorkOrder(sourceType))
        {
            return new ImportRowFingerprints(rowFingerprint, null, null);
        }

        return new ImportRowFingerprints(
            rowFingerprint,
            HistoricalWorkOrderIdempotencyFingerprintCalculator.Calculate(sourceType, row),
            HistoricalWorkOrderIdempotencyFingerprintCalculator.Algorithm);
    }

    private static bool IsHistoricalWorkOrder(string sourceType) =>
        string.Equals(
            sourceType,
            ImportSourceTypes.HistoricalWorkOrder,
            StringComparison.OrdinalIgnoreCase);
}
