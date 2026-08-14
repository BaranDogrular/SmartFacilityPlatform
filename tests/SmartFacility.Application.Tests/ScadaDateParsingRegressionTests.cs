using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Tests;

public sealed class ScadaDateParsingRegressionTests
{
    [Fact]
    public async Task Scada_outage_date_only_behavior_is_unchanged()
    {
        var row = RawRowFactory.Row(
            "SCADA SÜREKLLİK",
            2,
            RawRowFactory.Text("B", "Fixture reason"),
            RawRowFactory.DateTimeCell("F", new DateTime(2026, 8, 7)));

        var result = await new ScadaOutageImportProcessor()
            .ProcessAsync(row, TestProfiles.ScadaOutage(), CancellationToken.None);
        var outage = Assert.IsType<ScadaOutage>(result.Entity);

        Assert.Equal(ImportRowDisposition.Success, result.Disposition);
        Assert.Equal(new DateTime(2026, 8, 7), outage.RestoredAt);
        Assert.Equal("Started:Missing;Restored:ParsedDateOnly", outage.DateTimeParseStatus);
    }

    [Fact]
    public async Task Work_order_date_and_time_parsing_is_unchanged()
    {
        var expected = new DateTime(2026, 8, 7, 11, 45, 0);
        var row = RawRowFactory.Row(
            "WorkOrders",
            2,
            RawRowFactory.Text("D", "WO-FIXTURE"),
            RawRowFactory.DateTimeCell("E", expected.Date),
            RawRowFactory.TimeCell("F", expected.TimeOfDay));

        var result = await new WorkOrderImportProcessor(new NoLookupImportDataStore())
            .ProcessAsync(row, TestProfiles.WorkOrder(), CancellationToken.None);
        var workOrder = Assert.IsType<WorkOrder>(result.Entity);

        Assert.Equal(ImportRowDisposition.Success, result.Disposition);
        Assert.Equal(expected, workOrder.ReportedDateTime);
    }

    [Fact]
    public async Task Historical_work_order_date_parsing_is_unchanged()
    {
        var expected = new DateTime(2026, 8, 7, 12, 30, 0);
        var row = RawRowFactory.Row(
            "Toplam İş Emri",
            2,
            RawRowFactory.Text("A", "HWO-FIXTURE"),
            RawRowFactory.DateTimeCell("E", expected));

        var result = await new HistoricalWorkOrderImportProcessor()
            .ProcessAsync(row, TestProfiles.HistoricalWorkOrder(), CancellationToken.None);
        var workOrder = Assert.IsType<HistoricalWorkOrder>(result.Entity);

        Assert.Equal(ImportRowDisposition.Success, result.Disposition);
        Assert.Equal(expected, workOrder.ReportedDateTime);
    }

    private sealed class NoLookupImportDataStore : IImportDataStore
    {
        public Task<Asset?> FindAssetByCodeAsync(string assetCode, CancellationToken cancellationToken) =>
            Task.FromResult<Asset?>(null);

        public Task<Location?> FindUniqueLocationByNameAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult<Location?>(null);

        public Task<ImportBatch> CreateBatchAsync(
            string sourceType,
            string fileName,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task CompleteBatchAsync(
            long batchId,
            string status,
            int totalRows,
            int successfulRows,
            int failedRows,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RecordBatchFailureAsync(
            long batchId,
            string errorMessage,
            int totalRows,
            int successfulRows,
            int failedRows,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ISet<string>> GetSuccessfulFingerprintsAsync(
            string sourceType,
            IReadOnlyCollection<string> sheetNames,
            string? fingerprintAlgorithm,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ExecuteRowAsync(
            ImportSourceRecord sourceRecord,
            Func<CancellationToken, Task<ImportRowDecision>> operation,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Building> GetOrAddBuildingAsync(
            string? code,
            string name,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Location> GetOrAddLocationAsync(
            Building building,
            string name,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AssetGroup> GetOrAddAssetGroupAsync(
            string? code,
            string name,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
