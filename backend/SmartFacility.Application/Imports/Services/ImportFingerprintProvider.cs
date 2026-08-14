using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public sealed class ImportFingerprintProvider : IImportFingerprintProvider
{
    public string? GetIdempotencyAlgorithm(string sourceType)
    {
        if (IsHistoricalWorkOrder(sourceType))
        {
            return HistoricalWorkOrderIdempotencyFingerprintCalculator.Algorithm;
        }

        return IsScadaOutage(sourceType)
            ? ScadaOutageIdempotencyFingerprintCalculator.Algorithm
            : null;
    }

    public ImportRowFingerprints Calculate(string sourceType, RawExcelRow row)
    {
        var rowFingerprint = RowFingerprintCalculator.Calculate(sourceType, row);
        if (IsHistoricalWorkOrder(sourceType))
        {
            return new ImportRowFingerprints(
                rowFingerprint,
                HistoricalWorkOrderIdempotencyFingerprintCalculator.Calculate(sourceType, row),
                HistoricalWorkOrderIdempotencyFingerprintCalculator.Algorithm);
        }

        return IsScadaOutage(sourceType)
            ? new ImportRowFingerprints(
                rowFingerprint,
                ScadaOutageIdempotencyFingerprintCalculator.Calculate(sourceType, row),
                ScadaOutageIdempotencyFingerprintCalculator.Algorithm)
            : new ImportRowFingerprints(rowFingerprint, null, null);
    }

    private static bool IsHistoricalWorkOrder(string sourceType) =>
        string.Equals(
            sourceType,
            ImportSourceTypes.HistoricalWorkOrder,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsScadaOutage(string sourceType) =>
        string.Equals(
            sourceType,
            ImportSourceTypes.ScadaOutage,
            StringComparison.OrdinalIgnoreCase);
}
