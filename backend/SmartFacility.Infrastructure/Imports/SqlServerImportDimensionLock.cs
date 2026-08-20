using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Infrastructure.Persistence;

namespace SmartFacility.Infrastructure.Imports;

public sealed class SqlServerImportDimensionLock(SmartFacilityDbContext dbContext)
    : IImportDimensionLock
{
    public async Task<IAsyncDisposable> AcquireAsync(
        string dimensionName,
        string? identityPart1,
        string? identityPart2,
        string? identityPart3,
        CancellationToken cancellationToken)
    {
        var transaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "An active row transaction is required before acquiring an import dimension lock.");
        var commandTimeoutSeconds = dbContext.Database.GetCommandTimeout() ?? 30;
        var lockTimeoutMilliseconds = (int)Math.Min(
            (long)commandTimeoutSeconds * 1000,
            int.MaxValue);

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandTimeout = commandTimeoutSeconds;
        command.CommandText =
            """
            DECLARE @canonical nvarchar(max) = CONCAT(
                CASE WHEN @dimensionName IS NULL THEN N'-1:' ELSE CONCAT(DATALENGTH(UPPER(RTRIM(@dimensionName) COLLATE DATABASE_DEFAULT)) / 2, N':', UPPER(RTRIM(@dimensionName) COLLATE DATABASE_DEFAULT)) END,
                N'|',
                CASE WHEN @identityPart1 IS NULL THEN N'-1:' ELSE CONCAT(DATALENGTH(UPPER(RTRIM(@identityPart1) COLLATE DATABASE_DEFAULT)) / 2, N':', UPPER(RTRIM(@identityPart1) COLLATE DATABASE_DEFAULT)) END,
                N'|',
                CASE WHEN @identityPart2 IS NULL THEN N'-1:' ELSE CONCAT(DATALENGTH(UPPER(RTRIM(@identityPart2) COLLATE DATABASE_DEFAULT)) / 2, N':', UPPER(RTRIM(@identityPart2) COLLATE DATABASE_DEFAULT)) END,
                N'|',
                CASE WHEN @identityPart3 IS NULL THEN N'-1:' ELSE CONCAT(DATALENGTH(UPPER(RTRIM(@identityPart3) COLLATE DATABASE_DEFAULT)) / 2, N':', UPPER(RTRIM(@identityPart3) COLLATE DATABASE_DEFAULT)) END);
            DECLARE @resource nvarchar(255) = CONCAT(
                N'smartfacility:dimension:',
                CONVERT(varchar(64), HASHBYTES('SHA2_256', @canonical), 2));
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = @lockTimeout;
            SELECT @result;
            """;

        AddStringParameter(command, "@dimensionName", dimensionName, 100);
        AddStringParameter(command, "@identityPart1", identityPart1, 1000);
        AddStringParameter(command, "@identityPart2", identityPart2, 1000);
        AddStringParameter(command, "@identityPart3", identityPart3, 1000);

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
                $"SQL Server could not acquire the import dimension lock (result {result}).");
        }

        return TransactionOwnedLease.Instance;
    }

    private static void AddStringParameter(
        System.Data.Common.DbCommand command,
        string name,
        string? value,
        int size)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.String;
        parameter.Size = size;
        parameter.Value = value is null ? DBNull.Value : value;
        command.Parameters.Add(parameter);
    }

    private sealed class TransactionOwnedLease : IAsyncDisposable
    {
        public static TransactionOwnedLease Instance { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
