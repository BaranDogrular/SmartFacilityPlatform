using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Infrastructure.Persistence;

namespace SmartFacility.Infrastructure.Imports;

public sealed class SqlServerImportIdempotencyLock(SmartFacilityDbContext dbContext)
    : IImportIdempotencyLock
{
    public async Task<IAsyncDisposable> AcquireAsync(
        string sourceType,
        string sourceSheet,
        string? fingerprintAlgorithm,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var transaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "An active row transaction is required before acquiring an import idempotency lock.");
        var resource = CreateResourceName(
            sourceType,
            sourceSheet,
            fingerprintAlgorithm,
            fingerprint);
        var commandTimeoutSeconds = dbContext.Database.GetCommandTimeout() ?? 30;
        var lockTimeoutMilliseconds = (int)Math.Min(
            (long)commandTimeoutSeconds * 1000,
            int.MaxValue);

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandTimeout = commandTimeoutSeconds;
        command.CommandText =
            """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = @lockTimeout;
            SELECT @result;
            """;

        var resourceParameter = command.CreateParameter();
        resourceParameter.ParameterName = "@resource";
        resourceParameter.DbType = DbType.String;
        resourceParameter.Size = 255;
        resourceParameter.Value = resource;
        command.Parameters.Add(resourceParameter);

        var timeoutParameter = command.CreateParameter();
        timeoutParameter.ParameterName = "@lockTimeout";
        timeoutParameter.DbType = DbType.Int32;
        timeoutParameter.Value = lockTimeoutMilliseconds;
        command.Parameters.Add(timeoutParameter);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        var result = scalar is null or DBNull
            ? -999
            : Convert.ToInt32(scalar, System.Globalization.CultureInfo.InvariantCulture);
        if (result < 0)
        {
            throw new ImportPipelineException(
                $"SQL Server could not acquire the import idempotency lock (result {result}).");
        }

        return TransactionOwnedLease.Instance;
    }

    private static string CreateResourceName(
        string sourceType,
        string sourceSheet,
        string? fingerprintAlgorithm,
        string fingerprint)
    {
        var canonical = string.Join(
            '|',
            ImportValueNormalizer.NormalizeForComparison(sourceType),
            ImportValueNormalizer.NormalizeForComparison(sourceSheet),
            ImportValueNormalizer.NormalizeForComparison(fingerprintAlgorithm) ?? "ROW",
            ImportValueNormalizer.NormalizeForComparison(fingerprint));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return $"smartfacility:import:{hash}";
    }

    private sealed class TransactionOwnedLease : IAsyncDisposable
    {
        public static TransactionOwnedLease Instance { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
