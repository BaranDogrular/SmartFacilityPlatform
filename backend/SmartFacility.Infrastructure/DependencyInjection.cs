using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartFacility.Infrastructure.Persistence;

namespace SmartFacility.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SmartFacilityDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'SmartFacilityDatabase' was not found.");

        services.AddDbContext<SmartFacilityDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlServerOptions => sqlServerOptions.MigrationsAssembly(
                    typeof(SmartFacilityDbContext).Assembly.FullName)));

        return services;
    }
}
