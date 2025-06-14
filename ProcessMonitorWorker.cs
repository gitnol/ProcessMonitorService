using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Management.Infrastructure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class ProcessMonitorWorker : BackgroundService
{
    private readonly ILogger<ProcessMonitorWorker> _logger;
    private List<string> _processFilter;
    private readonly ConcurrentDictionary<int, string> _processSidCache = new();

    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;

    public ProcessMonitorWorker(ILogger<ProcessMonitorWorker> logger)
    {
        _logger = logger;
        _processFilter = new List<string>();
    }

    private List<string> LoadProcessFilterConfig()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "processmonitor.config.json");

        if (!File.Exists(configPath))
        {
            _logger.LogWarning("Konfigurationsdatei nicht gefunden: {ConfigPath}. Erstelle eine Standardkonfiguration.", configPath);
            var defaultConfig = new List<string> { "notepad.exe", "calc.exe", "powershell.exe", "cmd.exe" };
            try
            {
                var defaultConfigJson = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, defaultConfigJson);
                _logger.LogInformation("Standard-Konfigurationsdatei wurde erfolgreich erstellt: {ConfigPath}", configPath);
                return defaultConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Erstellen der Standard-Konfigurationsdatei unter {ConfigPath}", configPath);
                return new List<string>();
            }
        }

        try
        {
            string json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            _logger.LogInformation("Konfiguration erfolgreich geladen mit {FilterCount} Filtern: {ProcessFilter}", config.Count, string.Join(", ", config));
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden oder Parsen der Konfigurationsdatei: {ConfigPath}", configPath);
            return new List<string>();
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== ProcessMonitorWorker wird gestartet ===");
        _logger.LogInformation("Arbeitsverzeichnis: {BaseDirectory}", AppContext.BaseDirectory);
        _logger.LogInformation("Benutzerkontext: {UserName}", Environment.UserName);

        _processFilter = LoadProcessFilterConfig();
        
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            string baseQuery = "TargetInstance ISA 'Win32_Process'";
            string creationQuery = $"SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE {baseQuery}";
            string deletionQuery = $"SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE {baseQuery}";

            _startWatcher = new ManagementEventWatcher(new WqlEventQuery(creationQuery));
            _stopWatcher = new ManagementEventWatcher(new WqlEventQuery(deletionQuery));

            _startWatcher.EventArrived += (s, e) => LogProcessEvent(e, "Start");
            _stopWatcher.EventArrived += (s, e) => LogProcessEvent(e, "Stop");

            _logger.LogInformation("Starte WMI Event Watcher...");
            _startWatcher.Start();
            _stopWatcher.Start();
            _logger.LogInformation("✓ WMI Event Watcher erfolgreich gestartet.");

            while (!stoppingToken.IsCancellationRequested)
            {
                // Status-Update alle 5 Minuten
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                _logger.LogInformation("Service läuft. Aktive Prozesse im Cache: {CacheCount}", _processSidCache.Count);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ProcessMonitorWorker wird ordnungsgemäß beendet (OperationCanceledException).");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "KRITISCHER FEHLER in ProcessMonitorWorker ExecuteAsync. Der Dienst wird möglicherweise instabil.");
        }
    }

    private void LogProcessEvent(EventArrivedEventArgs e, string eventType)
    {
        try
        {
            var process = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            string name = process["Name"]?.ToString() ?? "N/A";

            if (_processFilter.Any() && !_processFilter.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return; // Prozess wird durch Filter ignoriert
            }

            int pid = Convert.ToInt32(process["ProcessId"]);
            string sid = "UNKNOWN";

            if (eventType == "Start")
            {
                sid = GetProcessOwnerSidCim(pid);
                _processSidCache[pid] = sid;
            }
            else if (eventType == "Stop")
            {
                if (_processSidCache.TryRemove(pid, out var cachedSid))
                {
                    sid = cachedSid;
                }
            }
            
            _logger.LogInformation(
                "Process Event: {EventType} | Name: {ProcessName} | PID: {ProcessId} | UserSID: {UserSid} | Path: {ExecutablePath} | CommandLine: {CommandLine}",
                eventType,
                name,
                pid,
                sid,
                process["ExecutablePath"]?.ToString(),
                process["CommandLine"]?.ToString()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei der Verarbeitung eines Prozess-Events ({EventType})", eventType);
        }
    }

    private static string GetProcessOwnerSidCim(int pid)
    {
        try
        {
            using var session = CimSession.Create(null);
            var query = $"SELECT * FROM Win32_Process WHERE ProcessId = {pid}";
            var result = session.QueryInstances(@"root\cimv2", "WQL", query).FirstOrDefault();

            if (result == null) return "UNKNOWN_PROCESS_NOT_FOUND";

            var methodResult = session.InvokeMethod(result, "GetOwnerSid", null);
            return methodResult?.OutParameters?["Sid"]?.Value?.ToString() ?? "UNKNOWN_SID_NOT_FOUND";
        }
        catch (Exception ex)
        {
            // Loggen wäre hier gut, aber wir haben keinen ILogger in einer statischen Methode.
            // Daher Rückgabe eines Fehler-Strings.
            Console.WriteLine($"Error in GetProcessOwnerSidCim for PID {pid}: {ex.Message}");
            return "ERROR_GETTING_SID";
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== ProcessMonitorWorker wird beendet ===");
        
        _startWatcher?.Stop();
        _stopWatcher?.Stop();
        _startWatcher?.Dispose();
        _stopWatcher?.Dispose();

        _logger.LogInformation("✓ WMI Event Watcher erfolgreich beendet.");
        _logger.LogInformation("✓ Service beendet um: {StopTime}. Prozesse im Cache beim Beenden: {CacheCount}", DateTime.Now, _processSidCache.Count);
        
        await base.StopAsync(cancellationToken);
    }
}