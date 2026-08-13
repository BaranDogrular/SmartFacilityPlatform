using Microsoft.Extensions.Logging;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Imports.Services;

public sealed class ExcelImportService(
    IExcelWorkbookReader workbookReader,
    IImportDataStore dataStore,
    IImportProfileCatalog profileCatalog,
    IImportFingerprintProvider fingerprintProvider,
    IEnumerable<IImportRowProcessor> processors,
    ILogger<ExcelImportService> logger) : IImportService
{
    private readonly IReadOnlyDictionary<string, IImportRowProcessor> _processors = processors
        .ToDictionary(processor => processor.ProfileKey, StringComparer.OrdinalIgnoreCase);

    public async Task<ImportResult> ImportAsync(
        ImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProfileKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FilePath);

        var profile = profileCatalog.GetRequired(request.ProfileKey);
        var processor = _processors.GetValueOrDefault(profile.Key)
            ?? throw new InvalidOperationException(
                $"No row processor is registered for profile '{profile.Key}'.");

        var batch = await dataStore.CreateBatchAsync(
            profile.SourceType,
            Path.GetFileName(request.FilePath),
            cancellationToken);

        var totalRows = 0;
        var successfulRows = 0;
        var failedRows = 0;
        var ignoredRows = 0;
        var duplicateRows = 0;

        logger.LogInformation(
            "Import batch {BatchId} started for source type {SourceType}.",
            batch.Id,
            profile.SourceType);

        try
        {
            var sheetNames = profile.Worksheets.Select(worksheet => worksheet.Name).ToArray();
            var fingerprintAlgorithm = fingerprintProvider.GetIdempotencyAlgorithm(profile.SourceType);
            var knownFingerprints = await dataStore.GetSuccessfulFingerprintsAsync(
                profile.SourceType,
                sheetNames,
                fingerprintAlgorithm,
                cancellationToken);
            var validatedSheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var readRequest = new ExcelReadRequest(
                request.FilePath,
                profile.Worksheets
                    .Select(worksheet => new WorksheetReadRequest(
                        worksheet.Name,
                        Math.Min(worksheet.HeaderRowNumber, worksheet.FirstDataRowNumber)))
                    .ToArray());

            await foreach (var row in workbookReader
                               .ReadRowsAsync(readRequest, cancellationToken)
                               .WithCancellation(cancellationToken))
            {
                var worksheet = profile.GetWorksheet(row.SheetName);
                if (row.RowNumber == worksheet.HeaderRowNumber)
                {
                    var headerErrors = ProfileHeaderValidator.Validate(row, worksheet);
                    if (headerErrors.Count > 0)
                    {
                        throw new ImportPipelineException(
                            $"Worksheet '{row.SheetName}' does not match its configured header profile: " +
                            string.Join(" ", headerErrors));
                    }

                    validatedSheets.Add(row.SheetName);
                }

                if (row.RowNumber < worksheet.FirstDataRowNumber || row.IsEmpty)
                {
                    continue;
                }

                if (!validatedSheets.Contains(row.SheetName))
                {
                    throw new ImportPipelineException(
                        $"Header row was not found for worksheet '{row.SheetName}'.");
                }

                totalRows++;
                var fingerprints = fingerprintProvider.Calculate(profile.SourceType, row);
                var sourceRecord = new ImportSourceRecord
                {
                    ImportBatchId = batch.Id,
                    SourceSheet = row.SheetName,
                    SourceRowNumber = row.RowNumber,
                    RowFingerprint = fingerprints.RowFingerprint,
                    IdempotencyFingerprint = fingerprints.IdempotencyFingerprint,
                    FingerprintAlgorithm = fingerprints.FingerprintAlgorithm,
                    RawData = RawRowSerializer.SerializeValues(row),
                    RawFormulaData = RawRowSerializer.SerializeFormulas(row),
                    ParseStatus = "Processing",
                    CreatedAt = DateTimeOffset.UtcNow
                };

                var decision = ImportRowDecision.Ignore();
                if (knownFingerprints.Contains(fingerprints.DuplicateFingerprint))
                {
                    decision = ImportRowDecision.Duplicate();
                    await dataStore.ExecuteRowAsync(
                        sourceRecord,
                        _ => Task.FromResult(decision),
                        cancellationToken);
                }
                else
                {
                    try
                    {
                        await dataStore.ExecuteRowAsync(
                            sourceRecord,
                            async token =>
                            {
                                var missingFields = profile.RequiredFields
                                    .Where(field => ImportValueNormalizer.Normalize(
                                        profile.GetCell(row, field)?.RawValue) is null)
                                    .ToArray();

                                decision = missingFields.Length > 0
                                    ? ImportRowDecision.Error(
                                        $"Required field is missing: {string.Join(", ", missingFields)}.")
                                    : await processor.ProcessAsync(row, profile, token);

                                return decision;
                            },
                            cancellationToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        logger.LogWarning(
                            "Import row failed in batch {BatchId}, sheet {Sheet}, row {RowNumber}.",
                            batch.Id,
                            row.SheetName,
                            row.RowNumber);

                        decision = ImportRowDecision.Error("The row could not be processed.");
                        var failedSourceRecord = new ImportSourceRecord
                        {
                            ImportBatchId = sourceRecord.ImportBatchId,
                            SourceSheet = sourceRecord.SourceSheet,
                            SourceRowNumber = sourceRecord.SourceRowNumber,
                            RowFingerprint = sourceRecord.RowFingerprint,
                            IdempotencyFingerprint = sourceRecord.IdempotencyFingerprint,
                            FingerprintAlgorithm = sourceRecord.FingerprintAlgorithm,
                            RawData = sourceRecord.RawData,
                            RawFormulaData = sourceRecord.RawFormulaData,
                            ParseStatus = "Processing",
                            CreatedAt = sourceRecord.CreatedAt
                        };
                        await dataStore.ExecuteRowAsync(
                            failedSourceRecord,
                            _ => Task.FromResult(decision),
                            cancellationToken);
                    }
                }

                switch (decision.Disposition)
                {
                    case ImportRowDisposition.Success:
                        successfulRows++;
                        knownFingerprints.Add(fingerprints.DuplicateFingerprint);
                        break;
                    case ImportRowDisposition.Error:
                        failedRows++;
                        break;
                    case ImportRowDisposition.Ignore:
                        ignoredRows++;
                        break;
                    case ImportRowDisposition.Duplicate:
                        duplicateRows++;
                        break;
                }
            }

            var missingSheets = profile.Worksheets
                .Where(worksheet => !validatedSheets.Contains(worksheet.Name))
                .Select(worksheet => worksheet.Name)
                .ToArray();
            if (missingSheets.Length > 0)
            {
                throw new ImportPipelineException(
                    $"Configured worksheet header was not found: {string.Join(", ", missingSheets)}.");
            }

            var status = failedRows == 0 ? "Completed" : "CompletedWithErrors";
            await dataStore.CompleteBatchAsync(
                batch.Id,
                status,
                totalRows,
                successfulRows,
                failedRows,
                cancellationToken);

            logger.LogInformation(
                "Import batch {BatchId} completed with status {Status}. Total {TotalRows}, successful {SuccessfulRows}, failed {FailedRows}, ignored {IgnoredRows}, duplicate {DuplicateRows}.",
                batch.Id,
                status,
                totalRows,
                successfulRows,
                failedRows,
                ignoredRows,
                duplicateRows);

            return new ImportResult(
                batch.Id,
                status,
                totalRows,
                successfulRows,
                failedRows,
                ignoredRows,
                duplicateRows);
        }
        catch (OperationCanceledException)
        {
            await dataStore.RecordBatchFailureAsync(
                batch.Id,
                "Import was cancelled.",
                totalRows,
                successfulRows,
                failedRows,
                CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Import batch {BatchId} failed for source type {SourceType}.",
                batch.Id,
                profile.SourceType);

            await dataStore.RecordBatchFailureAsync(
                batch.Id,
                "The import batch failed before completion.",
                totalRows,
                successfulRows,
                failedRows,
                cancellationToken);

            throw new ImportPipelineException(
                $"Import batch {batch.Id} failed. See ImportError records for details.",
                exception);
        }
    }
}
