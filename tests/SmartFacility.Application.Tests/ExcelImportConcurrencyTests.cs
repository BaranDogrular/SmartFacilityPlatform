using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Infrastructure.Imports;
using SmartFacility.Infrastructure.Persistence;

namespace SmartFacility.Application.Tests;

public sealed class ExcelImportConcurrencyTests
{
    [Fact]
    public async Task Concurrent_same_occurrence_creates_one_success_and_one_duplicate()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"smart-facility-concurrency-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<SmartFacilityDbContext>()
                .UseSqlite($"Data Source={databasePath};Default Timeout=30;Pooling=False")
                .Options;
            await using (var setupContext = new SmartFacilityDbContext(options))
            {
                await setupContext.Database.EnsureCreatedAsync();
            }

            await using (var firstContext = new SmartFacilityDbContext(options))
            await using (var secondContext = new SmartFacilityDbContext(options))
            {
                var idempotencyLock = new TestImportIdempotencyLock();
                var readerGate = new TwoParticipantGate();
                var firstService = CreateService(firstContext, idempotencyLock, readerGate);
                var secondService = CreateService(secondContext, idempotencyLock, readerGate);

                var results = await Task.WhenAll(
                    firstService.ImportAsync(new ImportRequest(
                        ImportProfileKeys.HistoricalWorkOrder,
                        "same-workbook.xlsx")),
                    secondService.ImportAsync(new ImportRequest(
                        ImportProfileKeys.HistoricalWorkOrder,
                        "same-workbook.xlsx")));

                Assert.Equal(1, results.Sum(result => result.SuccessfulRows));
                Assert.Equal(1, results.Sum(result => result.DuplicateRows));
            }

            await using (var verificationContext = new SmartFacilityDbContext(options))
            {
                Assert.Equal(1, await verificationContext.HistoricalWorkOrders.CountAsync());
                var statuses = await verificationContext.ImportSourceRecords
                    .OrderBy(record => record.ParseStatus)
                    .Select(record => record.ParseStatus)
                    .ToArrayAsync();
                Assert.Equal(["Duplicate", "Succeeded"], statuses);
            }
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
        }
    }

    [Fact]
    public async Task Concurrent_distinct_assets_share_each_logical_dimension()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"smart-facility-dimension-concurrency-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<SmartFacilityDbContext>()
                .UseSqlite($"Data Source={databasePath};Default Timeout=30;Pooling=False")
                .Options;
            await using (var setupContext = new SmartFacilityDbContext(options))
            {
                await setupContext.Database.EnsureCreatedAsync();
            }

            await using (var firstContext = new SmartFacilityDbContext(options))
            await using (var secondContext = new SmartFacilityDbContext(options))
            {
                var readerGate = new TwoParticipantGate();
                var dimensionLock = new TestImportDimensionLock();
                var firstService = CreateAssetService(
                    firstContext,
                    dimensionLock,
                    readerGate,
                    "ASSET-1");
                var secondService = CreateAssetService(
                    secondContext,
                    dimensionLock,
                    readerGate,
                    "ASSET-2");

                var results = await Task.WhenAll(
                    firstService.ImportAsync(new ImportRequest(
                        ImportProfileKeys.Asset,
                        "assets-first.xlsx")),
                    secondService.ImportAsync(new ImportRequest(
                        ImportProfileKeys.Asset,
                        "assets-second.xlsx")));

                Assert.Equal(2, results.Sum(result => result.SuccessfulRows));
                Assert.Equal(0, results.Sum(result => result.DuplicateRows));
                var buildingLockKeys = dimensionLock.AcquiredKeys
                    .Where(key => key.StartsWith("BUILDING|", StringComparison.Ordinal))
                    .ToArray();
                Assert.Equal(2, buildingLockKeys.Length);
                Assert.Single(buildingLockKeys.Distinct(StringComparer.Ordinal));
            }

            await using (var verificationContext = new SmartFacilityDbContext(options))
            {
                Assert.Equal(2, await verificationContext.Assets.CountAsync());
                Assert.Equal(1, await verificationContext.Buildings.CountAsync());
                Assert.Equal(1, await verificationContext.Locations.CountAsync());
                Assert.Equal(1, await verificationContext.AssetGroups.CountAsync());
                Assert.Equal(
                    2,
                    await verificationContext.ImportSourceRecords.CountAsync(
                        record => record.ParseStatus == "Succeeded"));
            }
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
        }
    }

    private static ExcelImportService CreateService(
        SmartFacilityDbContext context,
        IImportIdempotencyLock idempotencyLock,
        TwoParticipantGate readerGate)
    {
        var profile = TestProfiles.HistoricalWorkOrder();
        var worksheet = profile.Worksheets.Single();
        var store = new EfImportDataStore(
            context,
            idempotencyLock,
            new TestImportDimensionLock(),
            NullLogger<EfImportDataStore>.Instance);
        var rows = new RawExcelRow[]
        {
            RawRowFactory.Row(
                worksheet.Name,
                worksheet.HeaderRowNumber,
                RawRowFactory.Text("A", worksheet.ExpectedHeaders["A"])),
            RawRowFactory.Row(
                worksheet.Name,
                worksheet.FirstDataRowNumber,
                RawRowFactory.Text("A", "TIM-CONCURRENT"),
                RawRowFactory.Text("C", "A BLOCK"),
                RawRowFactory.Text("D", "PERSON ONE"),
                RawRowFactory.Text("E", "2026-08-01 10:00:00"),
                RawRowFactory.Text("K", "MECHANICAL"),
                RawRowFactory.Text("M", "WATER LEAK"),
                RawRowFactory.Text("P", "10"))
        };

        return new ExcelImportService(
            new GatedWorkbookReader(rows, readerGate),
            store,
            new ImportProfileCatalog([profile]),
            new ImportFingerprintProvider(),
            [new HistoricalWorkOrderImportProcessor()],
            NullLogger<ExcelImportService>.Instance);
    }

    private static ExcelImportService CreateAssetService(
        SmartFacilityDbContext context,
        IImportDimensionLock dimensionLock,
        TwoParticipantGate readerGate,
        string assetCode)
    {
        var profile = new AssetImportProfile(new ImportProfileOptions
        {
            SourceType = ImportSourceTypes.Asset,
            Worksheets =
            [
                new WorksheetProfileOptions
                {
                    Name = "Assets",
                    HeaderRowNumber = 1,
                    FirstDataRowNumber = 2,
                    ExpectedHeaders = new Dictionary<string, string>
                    {
                        ["B"] = "Asset Code"
                    }
                }
            ],
            Columns = new Dictionary<string, string>
            {
                ["AssetCode"] = "B",
                ["Name"] = "C",
                ["LocationName"] = "I",
                ["BuildingCode"] = "L",
                ["BuildingName"] = "M",
                ["AssetGroupCode"] = "R",
                ["AssetGroupName"] = "S"
            },
            RequiredFields = ["AssetCode"]
        });
        var rows = new RawExcelRow[]
        {
            RawRowFactory.Row(
                "Assets",
                1,
                RawRowFactory.Text("B", "Asset Code")),
            RawRowFactory.Row(
                "Assets",
                2,
                RawRowFactory.Text("B", assetCode),
                RawRowFactory.Text("C", $"{assetCode} name"),
                RawRowFactory.Text("I", "Shared location"),
                RawRowFactory.Text("L", "BLD-1"),
                RawRowFactory.Text("M", "Shared building"),
                RawRowFactory.Text("R", "GRP-1"),
                RawRowFactory.Text("S", "Shared group"))
        };
        var store = new EfImportDataStore(
            context,
            new TestImportIdempotencyLock(),
            dimensionLock,
            NullLogger<EfImportDataStore>.Instance);

        return new ExcelImportService(
            new GatedWorkbookReader(rows, readerGate),
            store,
            new ImportProfileCatalog([profile]),
            new ImportFingerprintProvider(),
            [new AssetImportProcessor(store)],
            NullLogger<ExcelImportService>.Instance);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class GatedWorkbookReader(
        IReadOnlyList<RawExcelRow> rows,
        TwoParticipantGate gate) : IExcelWorkbookReader
    {
        public async IAsyncEnumerable<RawExcelRow> ReadRowsAsync(
            ExcelReadRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await gate.SignalAndWaitAsync(cancellationToken);
            foreach (var row in rows)
            {
                yield return row;
            }
        }
    }

    private sealed class TwoParticipantGate
    {
        private readonly TaskCompletionSource _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public async Task SignalAndWaitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _release.TrySetResult();
            }

            await _release.Task.WaitAsync(cancellationToken);
        }
    }
}
