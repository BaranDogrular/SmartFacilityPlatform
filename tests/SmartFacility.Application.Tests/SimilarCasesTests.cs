using System.Text.Json;
using SmartFacility.Application.Analytics.Models;
using SmartFacility.Application.Analytics.Services;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Analytics;

namespace SmartFacility.Application.Tests;

public sealed class SimilarCasesTests
{
    [Fact]
    public void Scoring_is_deterministic_bounded_turkish_safe_and_rejects_low_text_similarity()
    {
        var signals = new SimilarCaseSignals(
            "Isı pompası motorunda titreşim ve ses var.",
            "ISI POMPASI motorunda ses ve titreşim gözlendi!",
            true,
            true,
            true,
            true,
            true,
            true,
            1);

        var first = SimilarCasesScoring.Calculate(signals);
        var second = SimilarCasesScoring.Calculate(signals);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.InRange(first.Score, 0m, 100m);
        Assert.Contains("ISI POMPASI", SimilarCasesScoring.NormalizeText(signals.TargetDescription));
        Assert.True(SimilarCasesScoring.IsEligible(first));

        var structurallySameButTextDifferent = SimilarCasesScoring.Calculate(signals with
        {
            CandidateDescription = "Asansör kapı kilidi değişimi tamamlandı"
        });
        Assert.False(SimilarCasesScoring.IsEligible(structurallySameButTextDifferent));
    }

    [Fact]
    public void Snippet_is_bounded_html_free_and_redacts_basic_email_and_mobile_patterns()
    {
        var source = $"<b>Teknik kayıt</b> test@example.com +90 532 123 45 67 {new string('x', 300)}";

        var snippet = SimilarCasesScoring.CreatePrivacySafeSnippet(source);

        Assert.True(snippet.Length <= SimilarCasesScoring.SnippetLength + 1);
        Assert.DoesNotContain("<b>", snippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test@example.com", snippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("532 123 45 67", snippet, StringComparison.Ordinal);
        Assert.Contains("[e-posta gizlendi]", snippet, StringComparison.Ordinal);
        Assert.Contains("[telefon gizlendi]", snippet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Primary_retrieval_is_deterministic_temporal_safe_self_safe_and_does_not_query_legacy()
    {
        var commandCapture = new CommandCaptureInterceptor();
        await using var database = await SqliteTestDatabase.CreateAsync(commandCapture);
        var group = new AssetGroup { Code = "PUMP", Name = "Pumps" };
        var asset = Asset("P-1", "Pump", group);
        database.Context.Assets.Add(asset);

        var similar = WorkOrder(
            "REUSED-NUMBER",
            new DateTime(2026, 8, 20, 10, 0, 0),
            asset,
            "Pompa motorunda aşırı ses ve titreşim var");
        database.Context.WorkOrders.Add(similar);
        for (var index = 0; index < 25; index++)
        {
            database.Context.WorkOrders.Add(WorkOrder(
                $"FILLER-{index}",
                new DateTime(2026, 7, 1).AddHours(index),
                asset,
                $"Rutin kontrol kaydı sıra {index}"));
        }

        database.Context.WorkOrders.Add(WorkOrder(
            "SAME-TIME",
            new DateTime(2026, 8, 25, 10, 0, 0),
            asset,
            "Pompa motorunda aşırı ses ve titreşim var"));
        database.Context.WorkOrders.Add(WorkOrder(
            "FUTURE",
            new DateTime(2026, 8, 26, 10, 0, 0),
            asset,
            "Pompa motorunda aşırı ses ve titreşim var"));
        database.Context.WorkOrders.Add(WorkOrder(
            "OTHER-DISCIPLINE",
            new DateTime(2026, 8, 19, 10, 0, 0),
            asset,
            "Pompa motorunda aşırı ses ve titreşim var",
            discipline: "ELEKTRİK"));
        database.Context.WorkOrders.Add(WorkOrder(
            "DUPLICATE-IDENTITY",
            new DateTime(2026, 8, 18, 10, 0, 0),
            asset,
            "Pompa motorunda aşırı ses ve titreşim var",
            fingerprint: "TARGET-FP"));
        var target = WorkOrder(
            "REUSED-NUMBER",
            new DateTime(2026, 8, 25, 10, 0, 0),
            asset,
            "Pompa motoru aşırı ses yapıyor ve titreşim mevcut",
            fingerprint: "TARGET-FP");
        database.Context.WorkOrders.Add(target);
        database.Context.HistoricalWorkOrders.Add(new HistoricalWorkOrder
        {
            SourceReference = "LEGACY",
            Description = "Pompa motorunda aşırı ses ve titreşim var",
            ReportedDateTime = new DateTime(2026, 8, 1),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();
        commandCapture.Commands.Clear();

        var service = new EfAnalyticsQueryService(database.Context);
        var first = await service.GetSimilarCasesAsync(target.Id, new SimilarCasesQuery { Top = 10 });
        var second = await service.GetSimilarCasesAsync(target.Id, new SimilarCasesQuery { Top = 10 });

        Assert.NotNull(first);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(SimilarCasesRetrievalMode.SameAssetDiscipline, first.Metadata.RetrievalMode);
        Assert.Equal(target.ReportedDateTime, first.Metadata.TemporalCutoff);
        Assert.DoesNotContain(first.Items, item => item.WorkOrderId == target.Id);
        Assert.DoesNotContain(first.Items, item => item.WorkOrderNumber is "SAME-TIME" or "FUTURE" or "DUPLICATE-IDENTITY");
        var match = Assert.Single(first.Items, item => item.WorkOrderId == similar.Id);
        Assert.Equal("REUSED-NUMBER", match.WorkOrderNumber);
        Assert.True(match.SimilarityScore >= SimilarCasesScoring.MinimumHybridScore);
        Assert.All(first.Items, item => Assert.True(item.ReportedDateTime < target.ReportedDateTime));
        Assert.Equal(6, commandCapture.Commands.Count);
        Assert.DoesNotContain(
            commandCapture.Commands,
            command => command.Contains("HistoricalWorkOrders", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Retrieval_widens_to_asset_group_and_does_not_cross_group()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var pumpGroup = new AssetGroup { Code = "PUMP", Name = "Pumps" };
        var otherGroup = new AssetGroup { Code = "HVAC", Name = "HVAC" };
        var targetAsset = Asset("P-1", "Pump one", pumpGroup);
        var siblingAsset = Asset("P-2", "Pump two", pumpGroup);
        var unrelatedAsset = Asset("H-1", "HVAC", otherGroup);
        database.Context.Assets.AddRange(targetAsset, siblingAsset, unrelatedAsset);
        var sibling = WorkOrder(
            "SIBLING",
            new DateTime(2026, 8, 20),
            siblingAsset,
            "Sirkülasyon pompasında yüksek titreşim ve ses mevcut");
        database.Context.WorkOrders.AddRange(
            sibling,
            WorkOrder(
                "OTHER-GROUP",
                new DateTime(2026, 8, 20),
                unrelatedAsset,
                "Sirkülasyon pompasında yüksek titreşim ve ses mevcut"));
        var target = WorkOrder(
            "TARGET",
            new DateTime(2026, 8, 25),
            targetAsset,
            "Sirkülasyon pompasında yüksek ses ve titreşim mevcut");
        database.Context.WorkOrders.Add(target);
        await database.Context.SaveChangesAsync();

        var result = await new EfAnalyticsQueryService(database.Context)
            .GetSimilarCasesAsync(target.Id, new SimilarCasesQuery());

        Assert.NotNull(result);
        Assert.Equal(SimilarCasesRetrievalMode.AssetGroupDiscipline, result.Metadata.RetrievalMode);
        Assert.Contains(result.Items, item => item.WorkOrderId == sibling.Id);
        Assert.DoesNotContain(result.Items, item => item.WorkOrderNumber == "OTHER-GROUP");
        Assert.Contains(result.Items[0].SimilarityReasons, reason => reason == "Aynı varlık grubu");
    }

    [Fact]
    public async Task Repeated_normalized_templates_are_penalized_and_collapsed_to_one_representative()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var group = new AssetGroup { Code = "CTRL", Name = "Controls" };
        var asset = Asset("C-1", "Control", group);
        database.Context.Assets.Add(asset);
        for (var index = 0; index < 30; index++)
        {
            database.Context.WorkOrders.Add(WorkOrder(
                $"TEMPLATE-{index}",
                new DateTime(2026, 7, 1).AddHours(index),
                asset,
                index % 2 == 0
                    ? "Vardiya kontrol çizelgesinin doldurulması."
                    : "vardiya kontrol çizelgesinin doldurulması!"));
        }

        var target = WorkOrder(
            "TARGET",
            new DateTime(2026, 8, 25),
            asset,
            "Vardiya kontrol çizelgesinin doldurulması");
        database.Context.WorkOrders.Add(target);
        await database.Context.SaveChangesAsync();

        var result = await new EfAnalyticsQueryService(database.Context)
            .GetSimilarCasesAsync(target.Id, new SimilarCasesQuery { Top = 10 });

        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(29, result.Metadata.DuplicateTemplatesSuppressed);
        Assert.InRange(result.Items[0].SimilarityScore, 0m, 100m);
        Assert.True(result.Items[0].SimilarityScore < 100m);
    }

    [Fact]
    public async Task Unlinked_short_missing_and_no_quality_targets_return_safe_results_and_cancellation_propagates()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var group = new AssetGroup { Code = "G", Name = "Group" };
        var asset = Asset("A-1", "Asset", group);
        database.Context.Assets.Add(asset);
        var unlinked = new WorkOrder
        {
            WorkOrderNumber = "UNLINKED",
            ReportedDateTime = new DateTime(2026, 8, 25),
            AssetCodeRaw = "RAW",
            Description = "Pompa motorunda titreşim mevcut",
            Discipline = "MEKANİK",
            IsInCanonicalSnapshot = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var shortTarget = WorkOrder("SHORT", new DateTime(2026, 8, 25), asset, "elektrik");
        var noMatch = WorkOrder(
            "NO-MATCH",
            new DateTime(2026, 8, 25),
            asset,
            "Jeneratör yakıt basıncı düşük uyarısı");
        database.Context.WorkOrders.AddRange(unlinked, shortTarget, noMatch);
        for (var index = 0; index < 25; index++)
        {
            database.Context.WorkOrders.Add(WorkOrder(
                $"UNRELATED-{index}",
                new DateTime(2026, 7, 1).AddHours(index),
                asset,
                $"Aydınlatma armatürü rutin kontrolü sıra {index}"));
        }

        await database.Context.SaveChangesAsync();
        var service = new EfAnalyticsQueryService(database.Context);

        var unavailable = await service.GetSimilarCasesAsync(unlinked.Id, new SimilarCasesQuery());
        Assert.NotNull(unavailable);
        Assert.Equal(SimilarCasesRetrievalMode.NotAvailable, unavailable.Metadata.RetrievalMode);
        Assert.Empty(unavailable.Items);

        var shortResult = await service.GetSimilarCasesAsync(shortTarget.Id, new SimilarCasesQuery());
        Assert.NotNull(shortResult);
        Assert.Equal(SimilarCasesRetrievalMode.NotAvailable, shortResult.Metadata.RetrievalMode);

        var empty = await service.GetSimilarCasesAsync(noMatch.Id, new SimilarCasesQuery());
        Assert.NotNull(empty);
        Assert.Empty(empty.Items);
        Assert.Equal("No sufficiently similar historical cases found.", empty.Metadata.AvailabilityMessage);

        Assert.Null(await service.GetSimilarCasesAsync(long.MaxValue, new SimilarCasesQuery()));

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetSimilarCasesAsync(noMatch.Id, new SimilarCasesQuery(), cancellation.Token));
    }

    private static Asset Asset(string code, string name, AssetGroup group) =>
        new()
        {
            AssetCode = code,
            Name = name,
            AssetGroup = group,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static WorkOrder WorkOrder(
        string number,
        DateTime reportedAt,
        Asset asset,
        string description,
        string discipline = "MEKANİK",
        string? fingerprint = null) =>
        new()
        {
            WorkOrderNumber = number,
            ReportedDateTime = reportedAt,
            Asset = asset,
            AssetCodeRaw = asset.AssetCode,
            Description = description,
            Discipline = discipline,
            WorkType = "ARIZA - İŞ TALEBİ",
            FailureType = "İŞ TALEBİ",
            FailureReason = "KONTROL",
            CanonicalIdentityFingerprint = fingerprint ?? Guid.NewGuid().ToString("N"),
            IsInCanonicalSnapshot = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}
