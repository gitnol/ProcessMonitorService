using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.IO;
using System.Threading;

namespace ProcessMonitorService
{
    class Program
    {
        static async Task Main(string[] args)
        {
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
                Log.Information("=== Process Monitor Service starting ===");

                await Host.CreateDefaultBuilder(args)
                    .UseWindowsService()
                    .ConfigureAppConfiguration((context, config) =>
                    {
                        config.SetBasePath(AppContext.BaseDirectory)
                            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                            .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    })
                    .UseSerilog((context, services, configuration) => configuration
                        .ReadFrom.Configuration(context.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext()
                        .Enrich.WithProperty("MachineName", Environment.MachineName)
                        .Enrich.WithProperty("ThreadId", Thread.CurrentThread.ManagedThreadId)
                    )
                    .ConfigureServices((hostContext, services) =>
                    {
                        // Configuration
                        services.Configure<ProcessMonitorOptions>(
                            hostContext.Configuration.GetSection("ProcessMonitor"));

                        // Services
                        services.AddSingleton<IProcessOwnerService, ProcessOwnerService>();
                        services.AddHostedService<ProcessMonitorWorker>();

                        // Health checks (optional)
                        services.AddHealthChecks();
                    })
                    .Build()
                    .RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error: Host could not be started");
                Environment.ExitCode = 1;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }
    }
}
