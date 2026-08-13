using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
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

        services.AddDbContext<SmartFacilityDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlServerOptions => sqlServerOptions.MigrationsAssembly(
                    typeof(SmartFacilityDbContext).Assembly.FullName)));

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
        services.AddScoped<IImportDataStore, EfImportDataStore>();
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
