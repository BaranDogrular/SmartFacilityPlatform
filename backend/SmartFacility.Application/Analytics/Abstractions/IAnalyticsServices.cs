using SmartFacility.Application.Analytics.Models;

namespace SmartFacility.Application.Analytics.Abstractions;

public interface IAssetAnalyticsService
{
    Task<Asset360SummaryResponse?> GetAsset360SummaryAsync(
        long assetId,
        CancellationToken cancellationToken = default);

    Task<AssetOverviewResponse> GetOverviewAsync(
        AssetOverviewQuery query,
        CancellationToken cancellationToken = default);

    Task<AssetMaintenanceActivityParetoResponse> GetMaintenanceActivityParetoAsync(
        AssetMaintenanceActivityParetoQuery query,
        CancellationToken cancellationToken = default);

    Task<InspectionPriorityResponse> GetInspectionPriorityAsync(
        InspectionPriorityQuery query,
        CancellationToken cancellationToken = default);

    Task<EarlyWarningResponse> GetEarlyWarningAsync(
        EarlyWarningQuery query,
        CancellationToken cancellationToken = default);
}

public interface IWorkOrderAnalyticsService
{
    Task<WorkOrderOverviewResponse> GetOverviewAsync(
        WorkOrderAnalyticsQuery query,
        CancellationToken cancellationToken = default);

    Task<WorkOrderTrendResponse> GetTrendAsync(
        WorkOrderAnalyticsQuery query,
        CancellationToken cancellationToken = default);

    Task<SimilarCasesResponse?> GetSimilarCasesAsync(
        long workOrderId,
        SimilarCasesQuery query,
        CancellationToken cancellationToken = default);
}

public interface IWorkOrderActivityService
{
    Task<WorkOrderActivityResponse> GetActivityAsync(
        WorkOrderActivityQuery query,
        CancellationToken cancellationToken = default);
}

public interface IScadaAnalyticsService
{
    Task<ScadaOverviewResponse> GetOverviewAsync(
        ScadaAnalyticsQuery query,
        CancellationToken cancellationToken = default);

    Task<ScadaTrendResponse> GetTrendAsync(
        ScadaAnalyticsQuery query,
        CancellationToken cancellationToken = default);

    Task<ScadaClearanceIntervalResponse> GetClearanceIntervalAsync(
        ScadaClearanceIntervalQuery query,
        CancellationToken cancellationToken = default);
}

public interface IImportQualityAnalyticsService
{
    Task<ImportQualityOverviewResponse> GetOverviewAsync(
        CancellationToken cancellationToken = default);
}
