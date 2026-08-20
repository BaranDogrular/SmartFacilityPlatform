using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SmartFacility.Application.Analytics.Abstractions;
using SmartFacility.Application.Analytics.Models;

namespace SmartFacility.Application.Tests;

public sealed class AnalyticsApiTests(AnalyticsApiFactory factory) :
    IClassFixture<AnalyticsApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/api/analytics/import-quality/overview")]
    [InlineData("/api/analytics/assets/overview")]
    [InlineData("/api/analytics/assets/maintenance-activity-pareto")]
    [InlineData("/api/analytics/work-orders/overview")]
    [InlineData("/api/analytics/work-orders/trend")]
    [InlineData("/api/analytics/historical-work-orders/activity")]
    [InlineData("/api/analytics/scada/overview")]
    [InlineData("/api/analytics/scada/trend")]
    [InlineData("/api/analytics/scada/clearance-interval")]
    public async Task Analytics_endpoint_returns_success(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 but received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    [Theory]
    [InlineData("/api/analytics/assets/overview?top=0")]
    [InlineData("/api/analytics/assets/overview?top=101")]
    [InlineData("/api/analytics/assets/maintenance-activity-pareto?top=0")]
    [InlineData("/api/analytics/assets/maintenance-activity-pareto?top=101")]
    [InlineData("/api/analytics/assets/maintenance-activity-pareto?dateFrom=2026-02-01&dateTo=2026-01-01")]
    [InlineData("/api/analytics/work-orders/overview?dateFrom=2026-02-01&dateTo=2026-01-01")]
    [InlineData("/api/analytics/historical-work-orders/activity?dateFrom=2026-02-01&dateTo=2026-01-01")]
    [InlineData("/api/analytics/scada/trend?dateFrom=2026-02-01&dateTo=2026-01-01")]
    [InlineData("/api/analytics/scada/clearance-interval?dateFrom=2026-02-01&dateTo=2026-01-01")]
    [InlineData("/api/analytics/work-orders/trend?grain=Day")]
    public async Task Invalid_query_returns_bad_request(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Empty_analytics_result_returns_ok_with_empty_aggregations()
    {
        using var response = await _client.GetAsync(
            "/api/analytics/work-orders/overview?discipline=NoMatch");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, document.RootElement.GetProperty("totalWorkOrders").GetInt64());
        Assert.Empty(document.RootElement.GetProperty("byDiscipline").EnumerateArray());
    }

    [Fact]
    public async Task P2_contracts_serialize_empty_results_and_reliability_as_strings()
    {
        using var paretoResponse = await _client.GetAsync(
            "/api/analytics/assets/maintenance-activity-pareto");
        using var pareto = JsonDocument.Parse(await paretoResponse.Content.ReadAsStreamAsync());
        Assert.Equal(0, pareto.RootElement.GetProperty("totalCurrentWorkOrders").GetInt64());
        Assert.Empty(pareto.RootElement.GetProperty("topAssets").EnumerateArray());
        Assert.Equal(
            "Yellow",
            pareto.RootElement.GetProperty("metadata").GetProperty("reliability").GetString());

        using var historicalResponse = await _client.GetAsync(
            "/api/analytics/historical-work-orders/activity");
        using var historical = JsonDocument.Parse(
            await historicalResponse.Content.ReadAsStreamAsync());
        Assert.Empty(historical.RootElement.GetProperty("trend").EnumerateArray());
        Assert.Empty(historical.RootElement.GetProperty("byDiscipline").EnumerateArray());
        Assert.Equal(
            "analytics.HistoricalWorkOrders",
            historical.RootElement.GetProperty("metadata")
                .GetProperty("sourceDataset").GetString());

        using var clearanceResponse = await _client.GetAsync(
            "/api/analytics/scada/clearance-interval");
        using var clearance = JsonDocument.Parse(
            await clearanceResponse.Content.ReadAsStreamAsync());
        Assert.Equal(0, clearance.RootElement.GetProperty("totalMatchedOccurrences").GetInt64());
        Assert.Equal(
            JsonValueKind.Null,
            clearance.RootElement.GetProperty("eligibilityPercent").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            clearance.RootElement.GetProperty("medianMinutes").ValueKind);
        Assert.Equal(JsonValueKind.Null, clearance.RootElement.GetProperty("p90Minutes").ValueKind);
    }

    [Fact]
    public async Task Swagger_document_contains_all_analytics_routes()
    {
        var swagger = await _client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/api/analytics/import-quality/overview", swagger, StringComparison.Ordinal);
        Assert.Contains("/api/analytics/assets/overview", swagger, StringComparison.Ordinal);
        Assert.Contains(
            "/api/analytics/assets/maintenance-activity-pareto",
            swagger,
            StringComparison.Ordinal);
        Assert.Contains("/api/analytics/work-orders/overview", swagger, StringComparison.Ordinal);
        Assert.Contains("/api/analytics/work-orders/trend", swagger, StringComparison.Ordinal);
        Assert.Contains(
            "/api/analytics/historical-work-orders/activity",
            swagger,
            StringComparison.Ordinal);
        Assert.Contains("/api/analytics/scada/overview", swagger, StringComparison.Ordinal);
        Assert.Contains("/api/analytics/scada/trend", swagger, StringComparison.Ordinal);
        Assert.Contains(
            "/api/analytics/scada/clearance-interval",
            swagger,
            StringComparison.Ordinal);
    }
}

public sealed class AnalyticsApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAssetAnalyticsService>();
            services.RemoveAll<IWorkOrderAnalyticsService>();
            services.RemoveAll<IHistoricalWorkOrderAnalyticsService>();
            services.RemoveAll<IScadaAnalyticsService>();
            services.RemoveAll<IImportQualityAnalyticsService>();

            services.AddSingleton<FakeAnalyticsServices>();
            services.AddSingleton<IAssetAnalyticsService>(provider =>
                provider.GetRequiredService<FakeAnalyticsServices>());
            services.AddSingleton<IWorkOrderAnalyticsService>(provider =>
                provider.GetRequiredService<FakeAnalyticsServices>());
            services.AddSingleton<IHistoricalWorkOrderAnalyticsService>(provider =>
                provider.GetRequiredService<FakeAnalyticsServices>());
            services.AddSingleton<IScadaAnalyticsService>(provider =>
                provider.GetRequiredService<FakeAnalyticsServices>());
            services.AddSingleton<IImportQualityAnalyticsService>(provider =>
                provider.GetRequiredService<FakeAnalyticsServices>());
        });
    }
}

internal sealed class FakeAnalyticsServices :
    IAssetAnalyticsService,
    IWorkOrderAnalyticsService,
    IHistoricalWorkOrderAnalyticsService,
    IScadaAnalyticsService,
    IImportQualityAnalyticsService
{
    private static readonly DateTimeOffset DataAsOf = new(2026, 8, 18, 0, 0, 0, TimeSpan.Zero);

    public Task<AssetOverviewResponse> GetOverviewAsync(
        AssetOverviewQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AssetOverviewResponse(
            0,
            [],
            [],
            [],
            0,
            0,
            [],
            KpiReliability.Yellow,
            SnapshotMetadata("core.Assets + core.WorkOrders")));

    public Task<AssetMaintenanceActivityParetoResponse> GetMaintenanceActivityParetoAsync(
        AssetMaintenanceActivityParetoQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AssetMaintenanceActivityParetoResponse(
            0,
            0,
            query.Top ?? 10,
            [],
            DateMetadata(
                "core.WorkOrders + core.Assets",
                "ReportedDateTime",
                KpiReliability.Yellow)));

    public Task<WorkOrderOverviewResponse> GetOverviewAsync(
        WorkOrderAnalyticsQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WorkOrderOverviewResponse(
            0,
            [],
            [],
            [],
            [],
            [],
            [],
            KpiReliability.Yellow,
            KpiReliability.Yellow,
            DateMetadata("core.WorkOrders", "ReportedDateTime", KpiReliability.Green)));

    public Task<WorkOrderTrendResponse> GetTrendAsync(
        WorkOrderAnalyticsQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WorkOrderTrendResponse(
            TimeGrain.Month,
            [],
            DateMetadata("core.WorkOrders", "ReportedDateTime", KpiReliability.Green)));

    public Task<HistoricalMaintenanceActivityResponse> GetActivityAsync(
        HistoricalMaintenanceActivityQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new HistoricalMaintenanceActivityResponse(
            TimeGrain.Month,
            [],
            [],
            query.Discipline,
            DateMetadata(
                "analytics.HistoricalWorkOrders",
                "ReportedDateTime",
                KpiReliability.Green)));

    public Task<ScadaOverviewResponse> GetOverviewAsync(
        ScadaAnalyticsQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ScadaOverviewResponse(
            0,
            [],
            [],
            [],
            [],
            [],
            0,
            0,
            KpiReliability.Yellow,
            KpiReliability.Yellow,
            DateMetadata("core.ScadaAlarmEvents", "ReceivedAt", KpiReliability.Green)));

    public Task<ScadaTrendResponse> GetTrendAsync(
        ScadaAnalyticsQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ScadaTrendResponse(
            TimeGrain.Month,
            [],
            new QualitySummaryDto(0, 0),
            DateMetadata("core.ScadaAlarmEvents", "ReceivedAt", KpiReliability.Yellow)));

    public Task<ScadaClearanceIntervalResponse> GetClearanceIntervalAsync(
        ScadaClearanceIntervalQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ScadaClearanceIntervalResponse(
            0,
            0,
            0,
            null,
            null,
            null,
            new ScadaClearanceIntervalAppliedFiltersDto(
                query.SourceSheet,
                query.AlarmType,
                query.InterventionLevel,
                query.Section,
                query.LocationRaw),
            DateMetadata(
                "core.ScadaAlarmEvents",
                "ReceivedAt",
                KpiReliability.Yellow)));

    public Task<ImportQualityOverviewResponse> GetOverviewAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportQualityOverviewResponse(
            0,
            [],
            [],
            [],
            0,
            [],
            [],
            0,
            0,
            SnapshotMetadata(
                "ingestion.ImportBatches + ingestion.ImportSourceRecords + ingestion.ImportErrors")));

    private static SnapshotAnalyticsMetadataDto SnapshotMetadata(string source) =>
        new(KpiReliability.Green, source, DataAsOf, 0, []);

    private static DateRangeMetadataDto DateMetadata(
        string source,
        string dateField,
        KpiReliability reliability) =>
        new(
            reliability,
            source,
            DataAsOf,
            null,
            null,
            null,
            null,
            dateField,
            0,
            0,
            0,
            "UnspecifiedSourceLocal",
            "test/v1",
            []);
}
