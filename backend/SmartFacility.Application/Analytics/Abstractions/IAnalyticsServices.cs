using SmartFacility.Application.Analytics.Models;

namespace SmartFacility.Application.Analytics.Abstractions;

public interface IAssetAnalyticsService
{
    Task<AssetOverviewResponse> GetOverviewAsync(
        AssetOverviewQuery query,
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
}

public interface IScadaAnalyticsService
{
    Task<ScadaOverviewResponse> GetOverviewAsync(
        ScadaAnalyticsQuery query,
        CancellationToken cancellationToken = default);

    Task<ScadaTrendResponse> GetTrendAsync(
        ScadaAnalyticsQuery query,
        CancellationToken cancellationToken = default);
}

public interface IImportQualityAnalyticsService
{
    Task<ImportQualityOverviewResponse> GetOverviewAsync(
        CancellationToken cancellationToken = default);
}
