using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Imports;

namespace SmartFacility.Application.Tests;

public sealed class EfImportDataStoreTransactionTests
{
    [Fact]
    public async Task Successful_row_operation_commits_source_record_and_entity()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var batchId = await CreateBatchAsync(database);
        var sourceRecord = CreateSourceRecord(batchId);

        await database.Store.ExecuteRowAsync(
            "HistoricalWorkOrder",
            sourceRecord,
            enforceIdempotency: true,
            _ => Task.FromResult(ImportRowDecision.Success(new HistoricalWorkOrder
            {
                SourceReference = "TIM-100",
                CreatedAt = DateTimeOffset.UtcNow
            })),
            CancellationToken.None);

        Assert.Equal(1, await database.Context.ImportSourceRecords.CountAsync());
        Assert.Equal(1, await database.Context.HistoricalWorkOrders.CountAsync());
        Assert.Equal("Succeeded", (await database.Context.ImportSourceRecords.SingleAsync()).ParseStatus);
    }

    [Fact]
    public async Task Operation_exception_rolls_back_changes_and_preserves_original_exception()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var batchId = await CreateBatchAsync(database);
        var expected = new TestImportException("original operation failure");

        var actual = await Assert.ThrowsAsync<TestImportException>(() =>
            database.Store.ExecuteRowAsync(
                "HistoricalWorkOrder",
                CreateSourceRecord(batchId),
                enforceIdempotency: true,
                async token =>
                {
                    database.Context.HistoricalWorkOrders.Add(new HistoricalWorkOrder
                    {
                        SourceReference = "ROLLBACK-ME",
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                    await database.Context.SaveChangesAsync(token);
                    throw expected;
                },
                CancellationToken.None));

        Assert.Same(expected, actual);
        Assert.Equal(0, await database.Context.HistoricalWorkOrders.CountAsync());
        Assert.Equal(0, await database.Context.ImportSourceRecords.CountAsync());
    }

    [Fact]
    public async Task Completed_transaction_rollback_failure_does_not_mask_original_exception()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var batchId = await CreateBatchAsync(database);
        var expected = new TestImportException("original failure after transaction completion");
        var logger = new RecordingLogger<EfImportDataStore>();
        var store = new EfImportDataStore(
            database.Context,
            new TestImportIdempotencyLock(),
            new TestImportDimensionLock(),
            logger);

        var actual = await Assert.ThrowsAsync<TestImportException>(() =>
            store.ExecuteRowAsync(
                "HistoricalWorkOrder",
                CreateSourceRecord(batchId),
                enforceIdempotency: true,
                async token =>
                {
                    var transaction = database.Context.Database.CurrentTransaction
                        ?? throw new InvalidOperationException("Expected an active transaction.");
                    await transaction.CommitAsync(token);
                    throw expected;
                },
                CancellationToken.None));

        Assert.Same(expected, actual);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning &&
            entry.Exception is InvalidOperationException &&
            entry.Message.Contains(nameof(TestImportException), StringComparison.Ordinal));
        Assert.Equal(0, await database.Context.ImportSourceRecords.CountAsync());
    }

    private static async Task<long> CreateBatchAsync(SqliteTestDatabase database)
    {
        var batch = new ImportBatch
        {
            SourceType = "HistoricalWorkOrder",
            FileName = "transaction-test.xlsx",
            StartedAt = DateTimeOffset.UtcNow,
            Status = "InProgress"
        };
        database.Context.ImportBatches.Add(batch);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        return batch.Id;
    }

    private static ImportSourceRecord CreateSourceRecord(long batchId) => new()
    {
        ImportBatchId = batchId,
        SourceSheet = "Toplam İş Emri",
        SourceRowNumber = 2,
        RowFingerprint = new string('A', 64),
        RawData = "{}",
        ParseStatus = "Processing",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private sealed class TestImportException(string message) : Exception(message);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
    }

    private sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);
}
