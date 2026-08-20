using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartFacility.Application.Analytics.Models;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Analytics;
using SmartFacility.Infrastructure.Persistence;

namespace SmartFacility.Application.Tests;

public sealed class ScadaClearanceSqlServerIntegrationTests
{
    private const string ConnectionEnvironmentVariable =
        "SMARTFACILITY_SQLSERVER_TEST_CONNECTION";
    private const string TestDatabasePrefix = "SmartFacilityP2AnalyticsTests_";

    [SqlServerIntegrationFact]
    public async Task Clearance_interval_uses_sql_server_percentiles_and_quality_rules()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(
            ConnectionEnvironmentVariable)!;
        var databaseName = $"{TestDatabasePrefix}{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        var databaseCreated = false;

        try
        {
            await ExecuteMasterCommandAsync(
                masterConnectionString,
                $"CREATE DATABASE [{databaseName}]");
            databaseCreated = true;

            var options = new DbContextOptionsBuilder<SmartFacilityDbContext>()
                .UseSqlServer(testConnectionString)
                .Options;
            await using var context = new SmartFacilityDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var baseDate = DateTime.Today.AddDays(-30);
            context.ScadaAlarmEvents.AddRange(
                Eligible(baseDate.AddHours(1), 0),
                Eligible(baseDate.AddHours(2), 10),
                Eligible(baseDate.AddHours(3), 20),
                Eligible(baseDate.AddHours(4), 100),
                Event(null, baseDate.AddHours(5), "Received:Missing;Cleared:Parsed"),
                Event(baseDate.AddHours(6), null, "Received:Parsed;Cleared:Missing"),
                Event(
                    baseDate.AddHours(7),
                    baseDate.AddHours(7).AddMinutes(10),
                    "Received:InvalidTime;Cleared:Parsed"),
                Event(
                    baseDate.AddHours(7).AddMinutes(30),
                    baseDate.AddHours(7).AddMinutes(40),
                    "Received:InvalidDate;Cleared:Parsed"),
                Event(
                    baseDate.AddHours(8),
                    baseDate.AddHours(8).AddMinutes(10),
                    "Received:DateOnlySource;Cleared:Parsed"),
                Event(
                    baseDate.AddHours(9),
                    baseDate.AddHours(9).AddMinutes(10),
                    "Received:Parsed;Cleared:PlaceholderX"),
                Event(
                    baseDate.AddHours(10),
                    baseDate.AddHours(10).AddMinutes(10),
                    "Received:SuspiciousYear;Cleared:Parsed"),
                Event(
                    baseDate.AddHours(11),
                    baseDate.AddHours(11).AddMinutes(10),
                    "Received:Parsed;Cleared:Parsed;Flags:FutureDate"),
                Event(
                    DateTime.Today.AddDays(2),
                    DateTime.Today.AddDays(2).AddMinutes(10),
                    "Received:Parsed;Cleared:Parsed"),
                Event(
                    baseDate.AddHours(13),
                    baseDate.AddHours(13).AddMinutes(-10),
                    "Received:Parsed;Cleared:Parsed;Flags:ClearedBeforeReceived"),
                Event(
                    baseDate.AddHours(14),
                    baseDate.AddHours(14).AddMinutes(-10),
                    "Received:Parsed;Cleared:Parsed"),
                Event(
                    baseDate.AddHours(15),
                    baseDate.AddHours(15).AddMinutes(30),
                    "Received:Parsed;Cleared:Parsed",
                    sourceSheet: "OTHER"));
            await context.SaveChangesAsync();

            var service = new EfAnalyticsQueryService(context);
            var query = new ScadaClearanceIntervalQuery
            {
                SourceSheet = "MECHANICAL",
                AlarmType = "Cooling",
                InterventionLevel = "Normal",
                Section = "Section",
                LocationRaw = "Location"
            };
            var response = await service.GetClearanceIntervalAsync(query);

            Assert.Equal(15, response.TotalMatchedOccurrences);
            Assert.Equal(4, response.EligibleOccurrences);
            Assert.Equal(11, response.ExcludedOccurrences);
            Assert.Equal(
                response.TotalMatchedOccurrences,
                response.EligibleOccurrences + response.ExcludedOccurrences);
            Assert.Equal(26.67m, response.EligibilityPercent);
            Assert.Equal(15m, response.MedianMinutes);
            Assert.Equal(76m, response.P90Minutes);
            Assert.Equal("MECHANICAL", response.AppliedFilters.SourceSheet);
            Assert.Equal(KpiReliability.Yellow, response.Metadata.Reliability);

            var dateFiltered = await service.GetClearanceIntervalAsync(query with
            {
                DateFrom = DateOnly.FromDateTime(baseDate),
                DateTo = DateOnly.FromDateTime(baseDate)
            });
            Assert.Equal(13, dateFiltered.TotalMatchedOccurrences);
            Assert.Equal(4, dateFiltered.EligibleOccurrences);

            var empty = await service.GetClearanceIntervalAsync(query with
            {
                SourceSheet = "NO_MATCH"
            });
            Assert.Equal(0, empty.TotalMatchedOccurrences);
            Assert.Equal(0, empty.EligibleOccurrences);
            Assert.Equal(0, empty.ExcludedOccurrences);
            Assert.Null(empty.EligibilityPercent);
            Assert.Null(empty.MedianMinutes);
            Assert.Null(empty.P90Minutes);

            using var cancellationSource = new CancellationTokenSource();
            await cancellationSource.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.GetClearanceIntervalAsync(query, cancellationSource.Token));
        }
        finally
        {
            if (databaseCreated)
            {
                Assert.StartsWith(TestDatabasePrefix, databaseName, StringComparison.Ordinal);
                await ExecuteMasterCommandAsync(
                    masterConnectionString,
                    $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                    $"DROP DATABASE [{databaseName}]");
            }
        }
    }

    private static ScadaAlarmEvent Eligible(DateTime receivedAt, int durationMinutes) =>
        Event(
            receivedAt,
            receivedAt.AddMinutes(durationMinutes),
            "Received:Parsed;Cleared:Parsed");

    private static ScadaAlarmEvent Event(
        DateTime? receivedAt,
        DateTime? clearedAt,
        string parseStatus,
        string sourceSheet = "MECHANICAL") =>
        new()
        {
            SourceSheet = sourceSheet,
            SectionRaw = "Section",
            LocationRaw = "Location",
            AlarmType = "Cooling",
            InterventionLevel = "Normal",
            Description = "Integration test occurrence",
            ReceivedAt = receivedAt,
            ClearedAt = clearedAt,
            DateTimeParseStatus = parseStatus,
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName
        };
        return builder.ConnectionString;
    }

    private static async Task ExecuteMasterCommandAsync(
        string connectionString,
        string commandText)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private sealed class SqlServerIntegrationFactAttribute : FactAttribute
    {
        public SqlServerIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable)))
            {
                Skip = $"Set {ConnectionEnvironmentVariable} to run SQL Server integration tests.";
            }
        }
    }
}
