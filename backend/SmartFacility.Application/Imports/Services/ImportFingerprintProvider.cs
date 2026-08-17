using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public sealed class ImportFingerprintProvider : IImportFingerprintProvider
{
    public string? GetIdempotencyAlgorithm(string sourceType, string sourceSheet)
    {
        if (IsHistoricalWorkOrder(sourceType))
        {
            return HistoricalWorkOrderIdempotencyFingerprintCalculator.Algorithm;
        }

        if (IsScadaOutage(sourceType))
        {
            return ScadaOutageIdempotencyFingerprintCalculator.Algorithm;
        }

        if (IsYanginAlarm(sourceType, sourceSheet))
        {
            return ScadaFireAlarmIdempotencyFingerprintCalculator.Algorithm;
        }

        return IsEnergyAlarm(sourceType, sourceSheet)
            ? ScadaEnergyAlarmIdempotencyFingerprintCalculator.Algorithm
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

        if (IsScadaOutage(sourceType))
        {
            return new ImportRowFingerprints(
                rowFingerprint,
                ScadaOutageIdempotencyFingerprintCalculator.Calculate(sourceType, row),
                ScadaOutageIdempotencyFingerprintCalculator.Algorithm);
        }

        if (IsYanginAlarm(sourceType, row.SheetName))
        {
            return new ImportRowFingerprints(
                rowFingerprint,
                ScadaFireAlarmIdempotencyFingerprintCalculator.Calculate(sourceType, row),
                ScadaFireAlarmIdempotencyFingerprintCalculator.Algorithm);
        }

        return IsEnergyAlarm(sourceType, row.SheetName)
            ? new ImportRowFingerprints(
                rowFingerprint,
                ScadaEnergyAlarmIdempotencyFingerprintCalculator.Calculate(sourceType, row),
                ScadaEnergyAlarmIdempotencyFingerprintCalculator.Algorithm)
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

    private static bool IsYanginAlarm(string sourceType, string sourceSheet) =>
        string.Equals(sourceType, ImportSourceTypes.ScadaAlarm, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(sourceSheet, ScadaAlarmWorksheetNames.Yangin, StringComparison.OrdinalIgnoreCase);

    private static bool IsEnergyAlarm(string sourceType, string sourceSheet) =>
        string.Equals(sourceType, ImportSourceTypes.ScadaAlarm, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(sourceSheet, ScadaAlarmWorksheetNames.Enerji, StringComparison.OrdinalIgnoreCase);
}
