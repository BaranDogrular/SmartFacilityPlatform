using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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
            .WithSummary("Returns asset snapshot and canonical work-order presence metrics.")
            .Produces<AssetOverviewResponse>()
            .ProducesValidationProblem();

        group.MapGet("/assets/{assetId:long}/summary", GetAsset360SummaryAsync)
            .WithName("GetAsset360Summary")
            .WithSummary("Returns a bounded canonical maintenance and decision-support summary for one asset.")
            .Produces<Asset360SummaryResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/assets/maintenance-activity-pareto", GetAssetMaintenanceActivityParetoAsync)
            .WithName("GetAssetMaintenanceActivityPareto")
            .WithSummary("Returns the concentration of canonical work-order records by asset.")
            .Produces<AssetMaintenanceActivityParetoResponse>()
            .ProducesValidationProblem();

        group.MapGet("/assets/inspection-priority", GetInspectionPriorityAsync)
            .WithName("GetInspectionPriority")
            .WithSummary("Returns explainable WorkOrder-activity-based asset inspection priority.")
            .Produces<InspectionPriorityResponse>()
            .ProducesValidationProblem();

        group.MapGet("/assets/early-warning", GetEarlyWarningAsync)
            .WithName("GetEarlyWarning")
            .WithSummary("Returns explainable per-asset WorkOrder activity deviations from personal history.")
            .Produces<EarlyWarningResponse>()
            .ProducesValidationProblem();

        group.MapGet("/work-orders/overview", GetWorkOrderOverviewAsync)
            .WithName("GetWorkOrderOverview")
            .WithSummary("Returns canonical WorkOrder totals and source-state aggregations.")
            .Produces<WorkOrderOverviewResponse>()
            .ProducesValidationProblem();

        group.MapGet("/work-orders/trend", GetWorkOrderTrendAsync)
            .WithName("GetWorkOrderTrend")
            .WithSummary("Returns the monthly canonical WorkOrder trend.")
            .Produces<WorkOrderTrendResponse>()
            .ProducesValidationProblem();

        group.MapGet("/work-orders/activity", GetWorkOrderActivityAsync)
            .WithName("GetWorkOrderActivity")
            .WithSummary("Returns dated activity from the canonical WorkOrder dataset.")
            .Produces<WorkOrderActivityResponse>()
            .ProducesValidationProblem();

        group.MapGet("/work-orders/{id:long}/similar-cases", GetSimilarCasesAsync)
            .WithName("GetSimilarHistoricalCases")
            .WithSummary("Returns deterministic prior canonical WorkOrders similar to the selected case.")
            .Produces<SimilarCasesResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
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

    private static async Task<Results<Ok<Asset360SummaryResponse>, NotFound<ProblemDetails>>>
        GetAsset360SummaryAsync(
            long assetId,
            IAssetAnalyticsService service,
            CancellationToken cancellationToken)
    {
        var result = await service.GetAsset360SummaryAsync(assetId, cancellationToken);
        return result is null
            ? TypedResults.NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Canonical asset not found.",
                Detail = $"No canonical asset exists with Id {assetId}."
            })
            : TypedResults.Ok(result);
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

    private static async Task<Results<Ok<InspectionPriorityResponse>, ValidationProblem>>
        GetInspectionPriorityAsync(
            [AsParameters] InspectionPriorityQuery query,
            IAssetAnalyticsService service,
            CancellationToken cancellationToken)
    {
        var errors = AnalyticsQueryValidation.Validate(query);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(
            await service.GetInspectionPriorityAsync(query, cancellationToken));
    }

    private static async Task<Results<Ok<EarlyWarningResponse>, ValidationProblem>>
        GetEarlyWarningAsync(
            [AsParameters] EarlyWarningQuery query,
            IAssetAnalyticsService service,
            CancellationToken cancellationToken)
    {
        var errors = AnalyticsQueryValidation.Validate(query);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(
            await service.GetEarlyWarningAsync(query, cancellationToken));
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

    private static async Task<Results<Ok<WorkOrderActivityResponse>, ValidationProblem>>
        GetWorkOrderActivityAsync(
            [AsParameters] WorkOrderActivityQuery query,
            IWorkOrderActivityService service,
            CancellationToken cancellationToken)
    {
        var errors = AnalyticsQueryValidation.Validate(query);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(await service.GetActivityAsync(query, cancellationToken));
    }

    private static async Task<Results<Ok<SimilarCasesResponse>, NotFound<ProblemDetails>, ValidationProblem>>
        GetSimilarCasesAsync(
            long id,
            [AsParameters] SimilarCasesQuery query,
            IWorkOrderAnalyticsService service,
            CancellationToken cancellationToken)
    {
        var errors = AnalyticsQueryValidation.Validate(query);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await service.GetSimilarCasesAsync(id, query, cancellationToken);
        return result is null
            ? TypedResults.NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Canonical WorkOrder not found.",
                Detail = $"No canonical WorkOrder exists with Id {id}."
            })
            : TypedResults.Ok(result);
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

    public static Dictionary<string, string[]> Validate(InspectionPriorityQuery query)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (query.Top.HasValue && query.Top is < 1 or > 100)
        {
            errors["top"] = ["Top must be between 1 and 100."];
        }

        if (query.AsOf.HasValue
            && (query.AsOf < DateOnly.MinValue.AddDays(89)
                || query.AsOf == DateOnly.MaxValue))
        {
            errors["asOf"] = ["AsOf must allow a complete 90-day analysis window."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(EarlyWarningQuery query)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (query.Top.HasValue && query.Top is < 1 or > 100)
        {
            errors["top"] = ["Top must be between 1 and 100."];
        }

        if (query.AsOf.HasValue
            && (query.AsOf < DateOnly.MinValue.AddYears(1).AddMonths(2)
                || query.AsOf == DateOnly.MaxValue))
        {
            errors["asOf"] = ["AsOf must allow the analysis and 12-month baseline windows."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(WorkOrderActivityQuery query) =>
        ValidateDateRange(query.DateFrom, query.DateTo, "dateFrom", "dateTo");

    public static Dictionary<string, string[]> Validate(SimilarCasesQuery query)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (query.Top.HasValue && query.Top is < 1 or > 50)
        {
            errors["top"] = ["Top must be between 1 and 50."];
        }

        return errors;
    }

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
