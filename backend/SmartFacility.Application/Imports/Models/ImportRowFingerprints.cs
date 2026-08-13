namespace SmartFacility.Application.Imports.Models;

public sealed record ImportRowFingerprints(
    string RowFingerprint,
    string? IdempotencyFingerprint,
    string? FingerprintAlgorithm)
{
    public string DuplicateFingerprint => IdempotencyFingerprint ?? RowFingerprint;
}
