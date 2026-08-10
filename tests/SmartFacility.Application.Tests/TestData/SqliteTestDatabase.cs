using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
        Store = new EfImportDataStore(context);
    }

    public SmartFacilityDbContext Context { get; }
    public EfImportDataStore Store { get; }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SmartFacilityDbContext>()
            .UseSqlite(connection)
            .Options;
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
