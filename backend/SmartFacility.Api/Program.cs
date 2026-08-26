using System.Text.Json.Serialization;
using SmartFacility.Api.Endpoints;
using SmartFacility.Application.Imports.Abstractions;
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
    var preflight = await service.PreflightAsync(filePath);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
        preflight,
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

    if (string.Equals(
            canonicalCommand,
            "--canonical-work-orders-import",
            StringComparison.OrdinalIgnoreCase))
    {
        if (!preflight.CanImport)
        {
            throw new InvalidOperationException(
                "Canonical WorkOrder preflight did not pass; import was not started.");
        }

        var result = await service.ImportAsync(filePath);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
            result,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
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
