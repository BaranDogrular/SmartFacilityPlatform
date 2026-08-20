using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFacility.Infrastructure.Imports;
using SmartFacility.Infrastructure.Persistence;

namespace SmartFacility.Application.Tests.TestData;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestDatabase(SqliteConnection connection, SmartFacilityDbContext context)
    {
        _connection = connection;
        Context = context;
        Store = new EfImportDataStore(
            context,
            new TestImportIdempotencyLock(),
            new TestImportDimensionLock(),
            NullLogger<EfImportDataStore>.Instance);
    }

    public SmartFacilityDbContext Context { get; }
    public EfImportDataStore Store { get; }

    public static async Task<SqliteTestDatabase> CreateAsync(params IInterceptor[] interceptors)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<SmartFacilityDbContext>()
            .UseSqlite(connection);
        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        var options = optionsBuilder.Options;
        var context = new SmartFacilityDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new SqliteTestDatabase(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
