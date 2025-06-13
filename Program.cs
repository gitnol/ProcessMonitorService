using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

await Host.CreateDefaultBuilder(args)
    .UseWindowsService() // Automatisch Dienst oder Konsole je nach Umgebung
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHostedService<ProcessMonitorWorker>();
    })
    .Build()
    .RunAsync();
