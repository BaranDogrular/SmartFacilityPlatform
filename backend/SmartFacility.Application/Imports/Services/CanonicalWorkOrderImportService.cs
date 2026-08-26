using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Domain;

namespace SmartFacility.Application.Imports.Services;

public sealed class CanonicalWorkOrderImportService(
    IExcelWorkbookReader workbookReader,
    IImportProfileCatalog profileCatalog,
    ICanonicalWorkOrderSnapshotStore snapshotStore) : ICanonicalWorkOrderImportService
{
    public async Task<CanonicalWorkOrderPreflightResult> PreflightAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(filePath, cancellationToken);
        var database = await snapshotStore.PreflightAsync(loaded.Rows, cancellationToken);
        var duplicateIdentityCount = loaded.Rows
            .GroupBy(row => row.IdentityFingerprint, StringComparer.Ordinal)
            .Count(group => group.Count() > 1);

        return new CanonicalWorkOrderPreflightResult(
            loaded.TotalRows,
            loaded.Rows.Count(row => WorkOrderSourceState.IsOpen(row.RawStatusCode)),
            loaded.Rows.Count(row => WorkOrderSourceState.IsClosed(row.RawStatusCode)),
            loaded.Rows.Count(row =>
                !WorkOrderSourceState.IsOpen(row.RawStatusCode)
                && !WorkOrderSourceState.IsClosed(row.RawStatusCode)),
            loaded.Rows.Select(row => row.AssetCode).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            duplicateIdentityCount,
            loaded.Rows.Count == 0 ? null : loaded.Rows.Min(row => row.ReportedDateTime),
            loaded.Rows.Count == 0 ? null : loaded.Rows.Max(row => row.ReportedDateTime),
            database,
            loaded.Errors);
    }

    public async Task<ImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(filePath, cancellationToken);
        var database = await snapshotStore.PreflightAsync(loaded.Rows, cancellationToken);
        var duplicateIdentityCount = loaded.Rows
            .GroupBy(row => row.IdentityFingerprint, StringComparer.Ordinal)
            .Count(group => group.Count() > 1);

        if (loaded.Errors.Count > 0
            || duplicateIdentityCount > 0
            || database.ExistingIdentityCollisions.Count > 0)
        {
            throw new ImportPipelineException(
                "Canonical WorkOrder preflight failed; no database writes were made. " +
                $"Parse errors: {loaded.Errors.Count}; " +
                $"incoming identity collisions: {duplicateIdentityCount}; " +
                $"unresolved asset codes (warning): {database.UnresolvedAssetCodes.Count}; " +
                $"existing identity collisions: {database.ExistingIdentityCollisions.Count}.");
        }

        var profile = profileCatalog.GetRequired(ImportProfileKeys.WorkOrder);
        return await snapshotStore.ApplyAsync(
            profile.SourceType,
            Path.GetFileName(filePath),
            loaded.Rows,
            cancellationToken);
    }

    private async Task<LoadedRows> LoadAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var profile = profileCatalog.GetRequired(ImportProfileKeys.WorkOrder);
        var readRequest = new ExcelReadRequest(
            filePath,
            profile.Worksheets.Select(worksheet => new WorksheetReadRequest(
                worksheet.Name,
                Math.Min(worksheet.HeaderRowNumber, worksheet.FirstDataRowNumber),
                worksheet.HeaderRowNumber)).ToArray());
        var validatedSheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<CanonicalWorkOrderRow>();
        var errors = new List<string>();
        var totalRows = 0;

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
                    errors.AddRange(headerErrors.Select(error => $"{row.SheetName}: {error}"));
                }
                else
                {
                    validatedSheets.Add(row.SheetName);
                }

                continue;
            }

            if (row.RowNumber < worksheet.FirstDataRowNumber || row.IsEmpty)
            {
                continue;
            }

            totalRows++;
            if (!validatedSheets.Contains(row.SheetName))
            {
                errors.Add($"{row.SheetName}!{row.RowNumber}: header was not validated.");
                continue;
            }

            var workOrderNumber = ProfileCellReader.Text(profile, row, "WorkOrderNumber");
            var assetCode = ProfileCellReader.Text(profile, row, "AssetCode");
            var reportedAt = ExcelValueParser.CombineDateAndTime(
                profile.GetCell(row, "ReportedDate"),
                profile.GetCell(row, "ReportedTime"));
            if (workOrderNumber is null || assetCode is null || reportedAt.Value is null)
            {
                errors.Add(
                    $"{row.SheetName}!{row.RowNumber}: canonical identity requires " +
                    "WorkOrderNumber, a parseable ReportedDateTime, and AssetCode.");
                continue;
            }

            rows.Add(new CanonicalWorkOrderRow(
                row.SheetName,
                row.RowNumber,
                RowFingerprintCalculator.Calculate(profile.SourceType, row),
                CanonicalWorkOrderIdentityCalculator.Calculate(
                    workOrderNumber,
                    reportedAt.Value.Value,
                    assetCode),
                RawRowSerializer.SerializeValues(row),
                RawRowSerializer.SerializeFormulas(row),
                workOrderNumber,
                reportedAt.Value.Value,
                assetCode,
                ProfileCellReader.Text(profile, row, "Description"),
                ProfileCellReader.Text(profile, row, "Discipline"),
                ProfileCellReader.Text(profile, row, "RequestedByName"),
                ProfileCellReader.Text(profile, row, "AssignedPersonnelName"),
                ProfileCellReader.Text(profile, row, "Status"),
                ProfileCellReader.Text(profile, row, "WorkType"),
                ProfileCellReader.Text(profile, row, "FailureType"),
                ProfileCellReader.Text(profile, row, "FailureReason"),
                ProfileCellReader.Text(profile, row, "LocationName"),
                ProfileCellReader.Text(profile, row, "ResponseDurationRaw"),
                ProfileCellReader.Text(profile, row, "DowntimeRaw"),
                ProfileCellReader.Text(profile, row, "MaintenanceDurationRaw"),
                ProfileCellReader.Text(profile, row, "TotalCostRaw"),
                ProfileCellReader.Text(profile, row, "ServiceCostRaw"),
                ProfileCellReader.Text(profile, row, "RawStatusCode")));
        }

        var missingSheets = profile.Worksheets
            .Where(worksheet => !validatedSheets.Contains(worksheet.Name))
            .Select(worksheet => worksheet.Name)
            .ToArray();
        if (missingSheets.Length > 0)
        {
            errors.Add($"Configured worksheet header was not found: {string.Join(", ", missingSheets)}.");
        }

        return new LoadedRows(rows, errors, totalRows);
    }

    private sealed record LoadedRows(
        IReadOnlyList<CanonicalWorkOrderRow> Rows,
        IReadOnlyList<string> Errors,
        int TotalRows);
}
