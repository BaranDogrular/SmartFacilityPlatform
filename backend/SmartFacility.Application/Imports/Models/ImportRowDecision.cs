namespace SmartFacility.Application.Imports.Models;

public enum ImportRowDisposition
{
    Success,
    Error,
    Ignore,
    Duplicate
}

public sealed record ImportRowDecision(
    ImportRowDisposition Disposition,
    object? Entity = null,
    string? ErrorMessage = null)
{
    public static ImportRowDecision Success(object entity) =>
        new(ImportRowDisposition.Success, entity);

    public static ImportRowDecision Error(string errorMessage) =>
        new(ImportRowDisposition.Error, ErrorMessage: errorMessage);

    public static ImportRowDecision Ignore() => new(ImportRowDisposition.Ignore);

    public static ImportRowDecision Duplicate() => new(ImportRowDisposition.Duplicate);
}
