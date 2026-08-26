namespace SmartFacility.Application.Analytics.Models;

public sealed record AssetOverviewQuery
{
    public long? BuildingId { get; init; }
    public long? LocationId { get; init; }
    public long? AssetGroupId { get; init; }
    public long? AssetId { get; init; }
    public DateOnly? WorkOrderDateFrom { get; init; }
    public DateOnly? WorkOrderDateTo { get; init; }
    public int? Top { get; init; }
}

public sealed record AssetMaintenanceActivityParetoQuery
{
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public int? Top { get; init; }
}

public sealed record InspectionPriorityQuery
{
    public int? Top { get; init; }
    public DateOnly? AsOf { get; init; }
}

public sealed record WorkOrderAnalyticsQuery
{
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public string? Discipline { get; init; }
    public string? WorkType { get; init; }
    public string? Status { get; init; }
    public string? FailureType { get; init; }
    public long? BuildingId { get; init; }
    public long? LocationId { get; init; }
    public long? AssetId { get; init; }
    public TimeGrain? Grain { get; init; }
}

public sealed record ScadaAnalyticsQuery
{
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public string? SourceSheet { get; init; }
    public string? AlarmType { get; init; }
    public string? InterventionLevel { get; init; }
    public string? Section { get; init; }
    public string? LocationRaw { get; init; }
    public TimeGrain? Grain { get; init; }
}

public sealed record WorkOrderActivityQuery
{
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public string? Discipline { get; init; }
}

public sealed record ScadaClearanceIntervalQuery
{
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public string? SourceSheet { get; init; }
    public string? AlarmType { get; init; }
    public string? InterventionLevel { get; init; }
    public string? Section { get; init; }
    public string? LocationRaw { get; init; }
}
