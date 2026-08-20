using Microsoft.AspNetCore.Http.HttpResults;
using SmartFacility.Application.Analytics.Abstractions;
using SmartFacility.Application.Analytics.Models;

namespace SmartFacility.Api.Endpoints;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/analytics")
            .WithTags("Analytics");

        group.MapGet("/import-quality/overview", GetImportQualityOverviewAsync)
            .WithName("GetImportQualityOverview")
            .WithSummary("Returns import pipeline quality and audit metrics.")
            .Produces<ImportQualityOverviewResponse>();

        group.MapGet("/assets/overview", GetAssetOverviewAsync)
            .WithName("GetAssetOverview")
            .WithSummary("Returns asset snapshot and current work-order presence metrics.")
            .Produces<AssetOverviewResponse>()
            .ProducesValidationProblem();

        group.MapGet("/assets/maintenance-activity-pareto", GetAssetMaintenanceActivityParetoAsync)
            .WithName("GetAssetMaintenanceActivityPareto")
            .WithSummary("Returns the concentration of current work-order records by asset.")
            .Produces<AssetMaintenanceActivityParetoResponse>()
            .ProducesValidationProblem();

        group.MapGet("/work-orders/overview", GetWorkOrderOverviewAsync)
            .WithName("GetWorkOrderOverview")
            .WithSummary("Returns current WorkOrder aggregations without historical data.")
            .Produces<WorkOrderOverviewResponse>()
            .ProducesValidationProblem();

        group.MapGet("/work-orders/trend", GetWorkOrderTrendAsync)
            .WithName("GetWorkOrderTrend")
            .WithSummary("Returns the monthly current WorkOrder trend.")
            .Produces<WorkOrderTrendResponse>()
            .ProducesValidationProblem();

        group.MapGet("/historical-work-orders/activity", GetHistoricalMaintenanceActivityAsync)
            .WithName("GetHistoricalMaintenanceActivity")
            .WithSummary("Returns historical-only monthly record activity and raw discipline counts.")
            .Produces<HistoricalMaintenanceActivityResponse>()
            .ProducesValidationProblem();

        group.MapGet("/scada/overview", GetScadaOverviewAsync)
            .WithName("GetScadaOverview")
            .WithSummary("Returns SCADA source-occurrence and timestamp-quality metrics.")
            .Produces<ScadaOverviewResponse>()
            .ProducesValidationProblem();

        group.MapGet("/scada/trend", GetScadaTrendAsync)
            .WithName("GetScadaTrend")
            .WithSummary("Returns a monthly SCADA source-occurrence trend using valid ReceivedAt values.")
            .Produces<ScadaTrendResponse>()
            .ProducesValidationProblem();

        group.MapGet("/scada/clearance-interval", GetScadaClearanceIntervalAsync)
            .WithName("GetScadaClearanceInterval")
            .WithSummary("Returns clearance interval percentiles for timestamp-quality eligible occurrences.")
            .Produces<ScadaClearanceIntervalResponse>()
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<Ok<ImportQualityOverviewResponse>> GetImportQualityOverviewAsync(
        IImportQualityAnalyticsService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.GetOverviewAsync(cancellationToken));

    private static async Task<Results<Ok<AssetOverviewResponse>, ValidationProblem>> GetAssetOverviewAsync(
        [AsParameters] AssetOverviewQuery query,
        IAssetAnalyticsService service,
        CancellationToken cancellationToken)
    {
        var errors = AnalyticsQueryValidation.Validate(query);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(await service.GetOverviewAsync(query, cancellationToken));
    }

    private static async Task<Results<Ok<AssetMaintenanceActivityParetoResponse>, ValidationProblem>>
        GetAssetMaintenanceActivityParetoAsync(
            [AsParameters] AssetMaintenanceActivityParetoQuery query,
            IAssetAnalyticsService service,
            CancellationToken cancellationToken)
    {
        var errors = AnalyticsQueryValidation.Validate(query);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(
            await service.GetMaintenanceActivityParetoAsync(query, cancellationToken));
    }

    private static async Task<Results<Ok<WorkOrderOverviewResponse>, ValidationProblem>> GetWorkOrderOverviewAsync(
        [AsParameters] WorkOrderAnalyticsQuery query,
        IWorkOrderAnalyticsService service,
        CancellationToken cancellationToken)
    {
        var errors = AnalyticsQueryValidation.Validate(query);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(await service.GetOverviewAsync(query, cancellationToken));
    }

    private static async Task<Results<Ok<WorkOrderTrendResponse>, ValidationProblem>> GetWorkOrderTrendAsync(
        [AsParameters] WorkOrderAnalyticsQuery query,
        IWorkOrderAnalyticsService service,
        CancellationToken cancellationToken)
    {
        var errors = AnalyticsQueryValidation.Validate(query);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(await service.GetTrendAsync(query, cancellationToken));
    }

    private static async Task<Results<Ok<HistoricalMaintenanceActivityResponse>, ValidationProblem>>
        GetHistoricalMaintenanceActivityAsync(
            [AsParameters] HistoricalMaintenanceActivityQuery query,
            IHistoricalWorkOrderAnalyticsService service,
            CancellationToken cancellationToken)
    {
        var errors = AnalyticsQueryValidation.Validate(query);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(await service.GetActivityAsync(query, cancellationToken));
    }

    private static async Task<Results<Ok<ScadaOverviewResponse>, ValidationProblem>> GetScadaOverviewAsync(
        [AsParameters] ScadaAnalyticsQuery query,
        IScadaAnalyticsService service,
        CancellationToken cancellationToken)
    {
        var errors = AnalyticsQueryValidation.Validate(query);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(await service.GetOverviewAsync(query, cancellationToken));
    }

    private static async Task<Results<Ok<ScadaTrendResponse>, ValidationProblem>> GetScadaTrendAsync(
        [AsParameters] ScadaAnalyticsQuery query,
        IScadaAnalyticsService service,
        CancellationToken cancellationToken)
    {
        var errors = AnalyticsQueryValidation.Validate(query);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(await service.GetTrendAsync(query, cancellationToken));
    }

    private static async Task<Results<Ok<ScadaClearanceIntervalResponse>, ValidationProblem>>
        GetScadaClearanceIntervalAsync(
            [AsParameters] ScadaClearanceIntervalQuery query,
            IScadaAnalyticsService service,
            CancellationToken cancellationToken)
    {
        var errors = AnalyticsQueryValidation.Validate(query);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(await service.GetClearanceIntervalAsync(query, cancellationToken));
    }
}

internal static class AnalyticsQueryValidation
{
    public static Dictionary<string, string[]> Validate(AssetOverviewQuery query)
    {
        var errors = ValidateDateRange(
            query.WorkOrderDateFrom,
            query.WorkOrderDateTo,
            "workOrderDateFrom",
            "workOrderDateTo");

        if (query.Top.HasValue && query.Top is < 1 or > 100)
        {
            errors["top"] = ["Top must be between 1 and 100."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(WorkOrderAnalyticsQuery query)
    {
        var errors = ValidateDateRange(query.DateFrom, query.DateTo, "dateFrom", "dateTo");
        ValidateGrain(query.Grain, errors);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(AssetMaintenanceActivityParetoQuery query)
    {
        var errors = ValidateDateRange(query.DateFrom, query.DateTo, "dateFrom", "dateTo");

        if (query.Top.HasValue && query.Top is < 1 or > 100)
        {
            errors["top"] = ["Top must be between 1 and 100."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(HistoricalMaintenanceActivityQuery query) =>
        ValidateDateRange(query.DateFrom, query.DateTo, "dateFrom", "dateTo");

    public static Dictionary<string, string[]> Validate(ScadaAnalyticsQuery query)
    {
        var errors = ValidateDateRange(query.DateFrom, query.DateTo, "dateFrom", "dateTo");
        ValidateGrain(query.Grain, errors);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(ScadaClearanceIntervalQuery query) =>
        ValidateDateRange(query.DateFrom, query.DateTo, "dateFrom", "dateTo");

    private static Dictionary<string, string[]> ValidateDateRange(
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string dateFromKey,
        string dateToKey)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
        {
            errors[dateFromKey] = [$"{dateFromKey} must be on or before {dateToKey}."];
        }

        if (dateTo == DateOnly.MaxValue)
        {
            errors[dateToKey] = [$"{dateToKey} must be earlier than {DateOnly.MaxValue:yyyy-MM-dd}."];
        }

        return errors;
    }

    private static void ValidateGrain(
        TimeGrain? grain,
        IDictionary<string, string[]> errors)
    {
        if (grain.HasValue
            && (!Enum.IsDefined(grain.Value) || grain != TimeGrain.Month))
        {
            errors["grain"] = ["Only Month is supported."];
        }
    }
}
