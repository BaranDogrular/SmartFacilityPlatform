using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Abstractions;

public interface IImportFingerprintProvider
{
    string? GetIdempotencyAlgorithm(string sourceType, string sourceSheet);
    ImportRowFingerprints Calculate(string sourceType, RawExcelRow row);
}
