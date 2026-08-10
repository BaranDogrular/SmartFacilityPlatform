namespace SmartFacility.Application.Imports.Services;

public sealed class ImportPipelineException(string message, Exception? innerException = null)
    : Exception(message, innerException);
