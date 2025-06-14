using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.IO;

// Serilog Bootstrap-Logger für den Startvorgang
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ProcessMonitorService",
            "logs",
            $"startup_error_{DateTime.Now:yyyyMMdd_HHmmss}.log"
        ),
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateBootstrapLogger();

try
{
    Log.Information("=== Service wird gestartet ===");

    await Host.CreateDefaultBuilder(args)
        .UseWindowsService()
        .ConfigureAppConfiguration((context, config) =>
        {
            // appsettings.json laden
            config.SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        })
        .UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
        )
        .ConfigureServices((hostContext, services) =>
        {
            services.AddHostedService<ProcessMonitorWorker>();
        })
        .Build()
        .RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Kritischer Fehler, der Host konnte nicht gestartet werden.");
}
finally
{
    // Sicherstellen, dass alle Logs geschrieben werden, bevor die Anwendung sich schließt
    await Log.CloseAndFlushAsync();
}