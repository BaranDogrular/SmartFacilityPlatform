using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartFacility.Application.Analytics.Abstractions;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Infrastructure.Configuration;
using SmartFacility.Infrastructure.Analytics;
using SmartFacility.Infrastructure.Imports;
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
        var sqlServerOptions = configuration
            .GetSection(SqlServerOptions.SectionName)
            .Get<SqlServerOptions>() ?? new SqlServerOptions();

        if (sqlServerOptions.CommandTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"{SqlServerOptions.SectionName}:{nameof(SqlServerOptions.CommandTimeoutSeconds)} " +
                "must be greater than zero.");
        }

        services.AddDbContext<SmartFacilityDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlOptions =>
                {
                    sqlOptions.CommandTimeout(sqlServerOptions.CommandTimeoutSeconds);
                    sqlOptions.MigrationsAssembly(typeof(SmartFacilityDbContext).Assembly.FullName);
                }));

        services.AddScoped<EfAnalyticsQueryService>();
        services.AddScoped<IAssetAnalyticsService>(provider =>
            provider.GetRequiredService<EfAnalyticsQueryService>());
        services.AddScoped<IWorkOrderAnalyticsService>(provider =>
            provider.GetRequiredService<EfAnalyticsQueryService>());
        services.AddScoped<IWorkOrderActivityService>(provider =>
            provider.GetRequiredService<EfAnalyticsQueryService>());
        services.AddScoped<IScadaAnalyticsService>(provider =>
            provider.GetRequiredService<EfAnalyticsQueryService>());
        services.AddScoped<IImportQualityAnalyticsService>(provider =>
            provider.GetRequiredService<EfAnalyticsQueryService>());

        var profiles = configuration
            .GetSection("ImportProfiles")
            .Get<Dictionary<string, ImportProfileOptions>>()
            ?? throw new InvalidOperationException("ImportProfiles configuration was not found.");

        services.AddSingleton<IImportSourceProfile>(
            new AssetImportProfile(GetProfileOptions(profiles, ImportProfileKeys.Asset)));
        services.AddSingleton<IImportSourceProfile>(
            new WorkOrderImportProfile(GetProfileOptions(profiles, ImportProfileKeys.WorkOrder)));
        services.AddSingleton<IImportSourceProfile>(
            new HistoricalWorkOrderImportProfile(
                GetProfileOptions(profiles, ImportProfileKeys.HistoricalWorkOrder)));
        services.AddSingleton<IImportSourceProfile>(
            new ScadaAlarmImportProfile(GetProfileOptions(profiles, ImportProfileKeys.ScadaAlarm)));
        services.AddSingleton<IImportSourceProfile>(
            new ScadaOutageImportProfile(GetProfileOptions(profiles, ImportProfileKeys.ScadaOutage)));

        services.AddSingleton<IImportProfileCatalog, ImportProfileCatalog>();
        services.AddSingleton<IImportFingerprintProvider, ImportFingerprintProvider>();
        services.AddScoped<IExcelWorkbookReader, ClosedXmlWorkbookReader>();
        services.AddScoped<IImportIdempotencyLock, SqlServerImportIdempotencyLock>();
        services.AddScoped<IImportDimensionLock, SqlServerImportDimensionLock>();
        services.AddScoped<IImportDataStore, EfImportDataStore>();
        services.AddScoped<ICanonicalWorkOrderSnapshotStore, EfCanonicalWorkOrderSnapshotStore>();
        services.AddScoped<ICanonicalWorkOrderImportService, CanonicalWorkOrderImportService>();
        services.AddScoped<IHistoricalInterventionSourceReader, BeamHistoricalInterventionSourceReader>();
        services.AddScoped<IHistoricalInterventionStore, EfHistoricalInterventionStore>();
        services.AddScoped<IHistoricalInterventionImportService, HistoricalInterventionImportService>();
        services.AddScoped<IImportRowProcessor, AssetImportProcessor>();
        services.AddScoped<IImportRowProcessor, WorkOrderImportProcessor>();
        services.AddScoped<IImportRowProcessor, HistoricalWorkOrderImportProcessor>();
        services.AddScoped<IImportRowProcessor, ScadaAlarmImportProcessor>();
        services.AddScoped<IImportRowProcessor, ScadaOutageImportProcessor>();
        services.AddScoped<IImportService, ExcelImportService>();

        return services;
    }

    private static ImportProfileOptions GetProfileOptions(
        IReadOnlyDictionary<string, ImportProfileOptions> profiles,
        string profileKey) =>
        profiles.GetValueOrDefault(profileKey)
        ?? throw new InvalidOperationException(
            $"Import profile configuration '{profileKey}' was not found.");
}
