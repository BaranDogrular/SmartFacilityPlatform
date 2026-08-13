using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartFacility.Infrastructure;
using SmartFacility.Infrastructure.Configuration;
using SmartFacility.Infrastructure.Persistence;

namespace SmartFacility.Application.Tests;

public sealed class SqlServerConfigurationTests
{
    [Fact]
    public void AddInfrastructure_applies_configured_command_timeout()
    {
        const int expectedTimeoutSeconds = 180;
        var configuration = CreateConfiguration(expectedTimeoutSeconds);
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SmartFacilityDbContext>();
        Assert.Equal(expectedTimeoutSeconds, dbContext.Database.GetCommandTimeout());
    }

    [Fact]
    public void AddInfrastructure_rejects_non_positive_command_timeout()
    {
        var configuration = CreateConfiguration(0);
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration));

        Assert.Contains(
            $"{SqlServerOptions.SectionName}:{nameof(SqlServerOptions.CommandTimeoutSeconds)}",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static IConfiguration CreateConfiguration(int commandTimeoutSeconds)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:SmartFacilityDatabase"] =
                "Server=localhost;Database=SmartFacilityDb;Trusted_Connection=True;TrustServerCertificate=True",
            [$"{SqlServerOptions.SectionName}:{nameof(SqlServerOptions.CommandTimeoutSeconds)}"] =
                commandTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        foreach (var sourceType in new[]
                 {
                     "Asset",
                     "WorkOrder",
                     "HistoricalWorkOrder",
                     "ScadaAlarm",
                     "ScadaOutage"
                 })
        {
            settings[$"ImportProfiles:{sourceType}:SourceType"] = sourceType;
            settings[$"ImportProfiles:{sourceType}:Worksheets:0:Name"] = "Data";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }
}
