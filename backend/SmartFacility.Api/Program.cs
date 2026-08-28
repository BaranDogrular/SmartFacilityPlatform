using System.Text.Json.Serialization;
using SmartFacility.Api.Endpoints;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

var canonicalCommand = args.FirstOrDefault(argument =>
    string.Equals(argument, "--canonical-work-orders-preflight", StringComparison.OrdinalIgnoreCase)
    || string.Equals(argument, "--canonical-work-orders-import", StringComparison.OrdinalIgnoreCase));
if (canonicalCommand is not null)
{
    var commandIndex = Array.IndexOf(args, canonicalCommand);
    if (commandIndex < 0 || commandIndex + 1 >= args.Length)
    {
        throw new InvalidOperationException(
            $"{canonicalCommand} requires an Excel file path argument.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var service = scope.ServiceProvider.GetRequiredService<ICanonicalWorkOrderImportService>();
    var filePath = args[commandIndex + 1];
    var importOptions = new CanonicalSnapshotImportOptions(
        args.Any(argument => string.Equals(
            argument,
            "--allow-suspicious-snapshot-shrink",
            StringComparison.OrdinalIgnoreCase)));
    var serializerOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
    var preflight = await service.PreflightAsync(filePath, options: importOptions);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
        preflight,
        serializerOptions));

    if (string.Equals(
            canonicalCommand,
            "--canonical-work-orders-import",
            StringComparison.OrdinalIgnoreCase))
    {
        if (!preflight.CanImport)
        {
            var error = preflight.Errors.FirstOrDefault()
                ?? (preflight.DuplicateIdentityCount > 0
                    ? "Canonical WorkOrder source contains duplicate canonical identities."
                    : null)
                ?? (preflight.Database.ExistingIdentityCollisions.Count > 0
                    ? "The existing database contains canonical identity collisions."
                    : null)
                ?? preflight.SafetyWarnings.FirstOrDefault()
                ?? "Canonical WorkOrder preflight did not pass; import was not started.";
            Console.Error.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    Status = "Blocked",
                    Error = error
                },
                serializerOptions));
            Environment.ExitCode = 2;
            return;
        }

        try
        {
            var result = await service.ImportAsync(filePath, options: importOptions);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, serializerOptions));
        }
        catch (CanonicalSnapshotSafetyException exception)
        {
            Console.Error.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                new { Status = "Blocked", Error = exception.Message },
                serializerOptions));
            Environment.ExitCode = 2;
        }
    }

    return;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapAnalyticsEndpoints();

app.Run();

public partial class Program;
