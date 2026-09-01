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
    [InlineData("/api/analytics/assets/search?q=A-1")]
    [InlineData("/api/analytics/assets/1/summary")]
    [InlineData("/api/analytics/assets/1/activity")]
    [InlineData("/api/analytics/assets/maintenance-activity-pareto")]
    [InlineData("/api/analytics/assets/inspection-priority")]
    [InlineData("/api/analytics/assets/early-warning")]
    [InlineData("/api/analytics/work-orders/overview")]
    [InlineData("/api/analytics/work-orders/trend")]
    [InlineData("/api/analytics/work-orders/activity")]
    [InlineData("/api/analytics/work-orders/1/similar-cases")]
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
    [InlineData("/api/analytics/assets/inspection-priority?top=0")]
    [InlineData("/api/analytics/assets/inspection-priority?top=101")]
    [InlineData("/api/analytics/assets/early-warning?top=0")]
    [InlineData("/api/analytics/assets/early-warning?top=101")]
    [InlineData("/api/analytics/assets/1/activity?pageSize=0")]
    [InlineData("/api/analytics/assets/1/activity?pageSize=51")]
    [InlineData("/api/analytics/assets/search?q=A")]
    [InlineData("/api/analytics/assets/search?q=ASSET&limit=0")]
    [InlineData("/api/analytics/assets/search?q=ASSET&limit=21")]
    [InlineData("/api/analytics/work-orders/1/similar-cases?top=0")]
    [InlineData("/api/analytics/work-orders/1/similar-cases?top=51")]
    [InlineData("/api/analytics/assets/maintenance-activity-pareto?dateFrom=2026-02-01&dateTo=2026-01-01")]
    [InlineData("/api/analytics/work-orders/overview?dateFrom=2026-02-01&dateTo=2026-01-01")]
    [InlineData("/api/analytics/work-orders/activity?dateFrom=2026-02-01&dateTo=2026-01-01")]
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
        Assert.Equal(0, pareto.RootElement.GetProperty("totalWorkOrders").GetInt64());
        Assert.Empty(pareto.RootElement.GetProperty("topAssets").EnumerateArray());
        Assert.Equal(
            "Yellow",
            pareto.RootElement.GetProperty("metadata").GetProperty("reliability").GetString());

        using var activityResponse = await _client.GetAsync(
            "/api/analytics/work-orders/activity");
        using var activity = JsonDocument.Parse(
            await activityResponse.Content.ReadAsStreamAsync());
        Assert.Empty(activity.RootElement.GetProperty("trend").EnumerateArray());
        Assert.Empty(activity.RootElement.GetProperty("byDiscipline").EnumerateArray());
        Assert.Equal(
            "core.WorkOrders",
            activity.RootElement.GetProperty("metadata")
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
    public async Task Inspection_priority_contract_serializes_level_and_metadata()
    {
        using var response = await _client.GetAsync(
            "/api/analytics/assets/inspection-priority?top=5&asOf=2026-08-25");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("2026-08-25", document.RootElement.GetProperty("metadata").GetProperty("asOf").GetString());
        Assert.Equal(5, document.RootElement.GetProperty("metadata").GetProperty("appliedTop").GetInt32());
        Assert.Equal(
            "LOW",
            document.RootElement.GetProperty("items")[0].GetProperty("priorityLevel").GetString());
    }

    [Fact]
    public async Task Early_warning_contract_serializes_level_baseline_status_and_metadata()
    {
        using var response = await _client.GetAsync(
            "/api/analytics/assets/early-warning?top=5&asOf=2026-08-25");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("2026-08-25", document.RootElement.GetProperty("metadata").GetProperty("asOf").GetString());
        Assert.Equal(5, document.RootElement.GetProperty("metadata").GetProperty("appliedTop").GetInt32());
        Assert.Equal(
            "NORMAL",
            document.RootElement.GetProperty("items")[0].GetProperty("warningLevel").GetString());
        Assert.Equal(
            "SUFFICIENT",
            document.RootElement.GetProperty("items")[0].GetProperty("baselineStatus").GetString());
    }

    [Fact]
    public async Task Asset_360_contract_is_typed_privacy_safe_and_returns_problem_details_for_unknown_asset()
    {
        using var response = await _client.GetAsync("/api/analytics/assets/1/summary");
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, document.RootElement.GetProperty("identity").GetProperty("assetId").GetInt64());
        Assert.Equal("LOW", document.RootElement.GetProperty("inspectionPriority").GetProperty("level").GetString());
        Assert.Equal(
            "INSUFFICIENT_BASELINE",
            document.RootElement.GetProperty("earlyWarning").GetProperty("baselineStatus").GetString());
        Assert.Equal(
            "Yellow",
            document.RootElement.GetProperty("scope").GetProperty("reliability").GetString());
        Assert.DoesNotContain("requestedByName", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("assignedPersonnelName", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("historicalIntervention", payload, StringComparison.OrdinalIgnoreCase);

        using var missing = await _client.GetAsync("/api/analytics/assets/404/summary");
        using var problem = JsonDocument.Parse(await missing.Content.ReadAsStreamAsync());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Canonical asset not found.", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Asset_activity_contract_is_bounded_private_and_maps_cursor_failures()
    {
        using var response = await _client.GetAsync("/api/analytics/assets/1/activity?pageSize=50");
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(50, document.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
        var item = document.RootElement.GetProperty("items")[0];
        Assert.Equal("OPEN", item.GetProperty("state").GetString());
        Assert.Equal(
            "INFORMATIVE",
            item.GetProperty("historicalIntervention").GetProperty("quality").GetString());
        Assert.DoesNotContain("requestedByName", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("assignedPersonnelName", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("descriptionRaw", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sourceFile", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fingerprint", payload, StringComparison.OrdinalIgnoreCase);

        using var missing = await _client.GetAsync("/api/analytics/assets/404/activity");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var malformed = await _client.GetAsync(
            "/api/analytics/assets/1/activity?cursor=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);

        using var stale = await _client.GetAsync(
            "/api/analytics/assets/1/activity?cursor=stale");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Asset_search_contract_returns_only_bounded_canonical_identity_fields()
    {
        using var response = await _client.GetAsync("/api/analytics/assets/search?q=A-1&limit=20");
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(1, document.RootElement[0].GetProperty("assetId").GetInt64());
        Assert.Equal("A-1", document.RootElement[0].GetProperty("assetCode").GetString());
        Assert.DoesNotContain("serialNumber", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fingerprint", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Asset_search_enforces_query_length_and_returns_empty_array_for_no_match()
    {
        using var maximum = await _client.GetAsync(
            $"/api/analytics/assets/search?q={new string('A', 100)}");
        using var tooLong = await _client.GetAsync(
            $"/api/analytics/assets/search?q={new string('A', 101)}");
        using var noMatch = await _client.GetAsync(
            "/api/analytics/assets/search?q=NO-MATCH");

        Assert.Equal(HttpStatusCode.OK, maximum.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, noMatch.StatusCode);
        using var payload = JsonDocument.Parse(await noMatch.Content.ReadAsStreamAsync());
        Assert.Empty(payload.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Similar_cases_contract_serializes_mode_excludes_pii_and_returns_problem_details_for_missing_target()
    {
        using var response = await _client.GetAsync(
            "/api/analytics/work-orders/1/similar-cases?top=5");
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "SAME_ASSET_DISCIPLINE",
            document.RootElement.GetProperty("metadata").GetProperty("retrievalMode").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("items")[0].GetProperty("workOrderId").GetInt64());
        var intervention = document.RootElement.GetProperty("items")[0]
            .GetProperty("historicalIntervention");
        Assert.Equal("INFORMATIVE", intervention.GetProperty("quality").GetString());
        Assert.Equal(
            "Motor rulmanı değiştirilerek test edildi.",
            intervention.GetProperty("workPerformedDescription").GetString());
        Assert.DoesNotContain("requestedByName", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("assignedPersonnelName", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sourceFile", payload, StringComparison.OrdinalIgnoreCase);

        using var missing = await _client.GetAsync(
            "/api/analytics/work-orders/404/similar-cases");
        using var problem = JsonDocument.Parse(await missing.Content.ReadAsStreamAsync());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Canonical WorkOrder not found.", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Swagger_document_contains_all_analytics_routes()
    {
        var swagger = await _client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/api/analytics/import-quality/overview", swagger, StringComparison.Ordinal);
        Assert.Contains("/api/analytics/assets/overview", swagger, StringComparison.Ordinal);
        Assert.Contains("/api/analytics/assets/search", swagger, StringComparison.Ordinal);
        Assert.Contains("/api/analytics/assets/{assetId}/summary", swagger, StringComparison.Ordinal);
        Assert.Contains("/api/analytics/assets/{assetId}/activity", swagger, StringComparison.Ordinal);
        Assert.Contains(
            "/api/analytics/assets/maintenance-activity-pareto",
            swagger,
            StringComparison.Ordinal);
        Assert.Contains(
            "/api/analytics/assets/inspection-priority",
            swagger,
            StringComparison.Ordinal);
        Assert.Contains(
            "/api/analytics/assets/early-warning",
            swagger,
            StringComparison.Ordinal);
        Assert.Contains("/api/analytics/work-orders/overview", swagger, StringComparison.Ordinal);
        Assert.Contains("/api/analytics/work-orders/trend", swagger, StringComparison.Ordinal);
        Assert.Contains(
            "/api/analytics/work-orders/activity",
            swagger,
            StringComparison.Ordinal);
        Assert.Contains(
            "/api/analytics/work-orders/{id}/similar-cases",
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
            services.RemoveAll<IWorkOrderActivityService>();
            services.RemoveAll<IScadaAnalyticsService>();
            services.RemoveAll<IImportQualityAnalyticsService>();

            services.AddSingleton<FakeAnalyticsServices>();
            services.AddSingleton<IAssetAnalyticsService>(provider =>
                provider.GetRequiredService<FakeAnalyticsServices>());
            services.AddSingleton<IWorkOrderAnalyticsService>(provider =>
                provider.GetRequiredService<FakeAnalyticsServices>());
            services.AddSingleton<IWorkOrderActivityService>(provider =>
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
    IWorkOrderActivityService,
    IScadaAnalyticsService,
    IImportQualityAnalyticsService
{
    private static readonly DateTimeOffset DataAsOf = new(2026, 8, 18, 0, 0, 0, TimeSpan.Zero);

    public Task<Asset360SummaryResponse?> GetAsset360SummaryAsync(
        long assetId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Asset360SummaryResponse?>(assetId == 404
            ? null
            : new Asset360SummaryResponse(
                new DateOnly(2026, 8, 25),
                new Asset360IdentityDto(
                    assetId,
                    "A-1",
                    "Asset",
                    "Equipment",
                    "In Use",
                    1,
                    "Building",
                    1,
                    "Location",
                    1,
                    "Group",
                    null,
                    null,
                    null),
                new Asset360MaintenanceSummaryDto(1, 0, 0, 1, 1, new DateTime(2026, 8, 20)),
                new Asset360InspectionPriorityDto(
                    10,
                    InspectionPriorityLevel.Low,
                    0,
                    1,
                    0,
                    1,
                    0,
                    1,
                    ["Son 30 günde 1 iş emri"],
                    null,
                    "inspection-priority/v1"),
                new Asset360EarlyWarningDto(
                    null,
                    null,
                    EarlyWarningBaselineStatus.InsufficientBaseline,
                    0,
                    0,
                    1,
                    0,
                    1,
                    0,
                    null,
                    null,
                    1,
                    null,
                    0,
                    ["Yetersiz geçmiş veri"],
                    null,
                    new EarlyWarningBaselineWindowDto(
                        new DateOnly(2025, 7, 1),
                        new DateOnly(2026, 6, 30),
                        12,
                        6),
                    "early-warning/v1"),
                new Asset360ScopeDto(
                    KpiReliability.Yellow,
                    1,
                    0,
                    100,
                    true,
                    true,
                    "core.Assets + core.WorkOrders",
                    []),
                DataAsOf));

    public Task<AssetActivityResult> GetAssetActivityAsync(
        long assetId,
        AssetActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        if (assetId == 404)
        {
            return Task.FromResult(new AssetActivityResult(
                AssetActivityResultStatus.AssetNotFound,
                null));
        }

        if (query.Cursor == "invalid")
        {
            return Task.FromResult(new AssetActivityResult(
                AssetActivityResultStatus.InvalidCursor,
                null));
        }

        if (query.Cursor == "stale")
        {
            return Task.FromResult(new AssetActivityResult(
                AssetActivityResultStatus.StaleCursor,
                null));
        }

        return Task.FromResult(new AssetActivityResult(
            AssetActivityResultStatus.Success,
            new AssetActivityResponse(
                assetId,
                [
                    new AssetActivityItemDto(
                        1,
                        "WO-1",
                        new DateTime(2026, 8, 20, 10, 0, 0),
                        AssetActivityState.Open,
                        "Open",
                        "MEKANÄ°K",
                        "KONTROL",
                        "Ä°Å TALEBÄ°",
                        "Pompa motorunda titreÅŸim gÃ¶zlendi.",
                        new AssetActivityHistoricalInterventionDto(
                            "Motor kontrol edilsin.",
                            "Rulman aÅŸÄ±nmasÄ±",
                            "Rulman deÄŸiÅŸtirildi.",
                            AssetActivityInterventionQuality.Informative,
                            new DateTime(2026, 8, 20, 12, 0, 0)),
                        1)
                ],
                query.PageSize ?? 25,
                false,
                null,
                "core.WorkOrders + core.HistoricalInterventions",
                "privacy-redaction/email-turkish-mobile/v1")));
    }

    public Task<IReadOnlyList<AssetSearchItemDto>> SearchAssetsAsync(
        AssetSearchQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AssetSearchItemDto>>(
            string.Equals(query.Q, "NO-MATCH", StringComparison.Ordinal)
                ? []
                : [new AssetSearchItemDto(1, "A-1", "Asset", "Building", "Location", "Group")]);

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

    public Task<InspectionPriorityResponse> GetInspectionPriorityAsync(
        InspectionPriorityQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new InspectionPriorityResponse(
            new InspectionPriorityMetadataDto(
                query.AsOf,
                null,
                0,
                0,
                0,
                1,
                query.Top ?? 10,
                "core.WorkOrders + core.Assets",
                "inspection-priority/v1",
                []),
            [
                new InspectionPriorityItemDto(
                    1,
                    "A-1",
                    "Asset",
                    10,
                    InspectionPriorityLevel.Low,
                    0,
                    1,
                    0,
                    1,
                    0,
                    1,
                    ["Son 30 günde 1 iş emri"])
            ]));

    public Task<EarlyWarningResponse> GetEarlyWarningAsync(
        EarlyWarningQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new EarlyWarningResponse(
            new EarlyWarningMetadataDto(
                query.AsOf,
                new EarlyWarningBaselineWindowDto(
                    new DateOnly(2025, 7, 1),
                    new DateOnly(2026, 6, 30),
                    12,
                    6),
                1,
                1,
                0,
                0,
                0,
                0,
                query.Top ?? 10,
                "core.WorkOrders + core.Assets",
                "early-warning/v1",
                []),
            [
                new EarlyWarningItemDto(
                    1,
                    "A-1",
                    "Asset",
                    10,
                    EarlyWarningLevel.Normal,
                    EarlyWarningBaselineStatus.Sufficient,
                    1,
                    1,
                    3,
                    3,
                    9,
                    9,
                    3,
                    1,
                    12,
                    0,
                    0,
                    ["Yakın dönem aktivitesi kişisel tarihsel baseline içinde"])
            ]));

    public Task<WorkOrderOverviewResponse> GetOverviewAsync(
        WorkOrderAnalyticsQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WorkOrderOverviewResponse(
            0,
            0,
            0,
            0,
            0,
            [],
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

    public Task<SimilarCasesResponse?> GetSimilarCasesAsync(
        long workOrderId,
        SimilarCasesQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<SimilarCasesResponse?>(workOrderId == 404
            ? null
            : new SimilarCasesResponse(
                new SimilarCasesMetadataDto(
                    workOrderId,
                    new DateTime(2026, 8, 25, 10, 0, 0),
                    new SimilarCasesTargetAssetDto(1, "A-1", "Asset"),
                    "MEKANİK",
                    SimilarCasesRetrievalMode.SameAssetDiscipline,
                    1,
                    1,
                    0,
                    new DateTime(2026, 8, 25, 10, 0, 0),
                    500,
                    "similar-cases/hybrid-jaccard/v1",
                    null),
                [
                    new SimilarCaseItemDto(
                        1,
                        "WO-1",
                        new DateTime(2026, 8, 20, 10, 0, 0),
                        "A-1",
                        "Asset",
                        "MEKANİK",
                        "ARIZA - İŞ TALEBİ",
                        "İŞ TALEBİ",
                        "KONTROL",
                        85,
                        ["Aynı varlık", "Aynı disiplin", "Benzer açıklama (%80)"],
                        "Pompa motorunda titreşim gözlendi.",
                        new SimilarCaseHistoricalInterventionDto(
                            "Motor titreşimi kontrol edilsin.",
                            "Rulman aşınması",
                            "Motor rulmanı değiştirilerek test edildi.",
                            SimilarCaseInterventionQuality.Informative,
                            new DateTime(2026, 8, 20, 12, 0, 0)))
                ]));

    public Task<WorkOrderActivityResponse> GetActivityAsync(
        WorkOrderActivityQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WorkOrderActivityResponse(
            TimeGrain.Month,
            [],
            [],
            query.Discipline,
            DateMetadata(
                "core.WorkOrders",
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
