using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Management;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Remoting;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;

public class ProcessMonitorWorker : BackgroundService
{
    private readonly ILogger<ProcessMonitorWorker> _logger;
    private List<string> _processFilter;

    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;
    private static readonly ConcurrentDictionary<int, string> processSidCache = new();

    // Log-Pfade für verschiedene Log-Typen
    private string _processLogPath = "";
    private string _errorLogPath = "";

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
            _logger.LogWarning($"Konfigurationsdatei nicht gefunden: {configPath}");

            // Erstelle eine Standard-Konfigurationsdatei
            var defaultConfig = new List<string>
            {
                "notepad.exe",
                "calc.exe",
                "powershell.exe",
                "cmd.exe"
            };

            try
            {
                File.WriteAllText(configPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
                _logger.LogInformation($"Standard-Konfigurationsdatei erstellt: {configPath}");
                return defaultConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fehler beim Erstellen der Standard-Konfigurationsdatei: {configPath}");
                return new List<string>();
            }
        }

        try
        {
            string json = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            _logger.LogInformation($"Konfiguration geladen: {string.Join(", ", config)}");
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Fehler beim Laden der Konfigurationsdatei: {configPath}");
            return new List<string>();
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("=== ProcessMonitorWorker wird gestartet ===");
            _logger.LogInformation($"Startzeit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _logger.LogInformation($"Arbeitsverzeichnis: {AppContext.BaseDirectory}");
            _logger.LogInformation($"Benutzerkontext: {Environment.UserName}");
            _logger.LogInformation($"Maschinenname: {Environment.MachineName}");

            // Log-Pfade initialisieren
            InitializeLogPaths();

            // Config laden
            _processFilter = LoadProcessFilterConfig();
            _logger.LogInformation($"Konfigurationsdatei geladen mit {_processFilter.Count} Einträgen");

            // Test der Log-Funktionalität
            await TestLoggingFunctionality();

            await base.StartAsync(cancellationToken);

            _logger.LogInformation("ProcessMonitorWorker erfolgreich gestartet");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KRITISCHER FEHLER beim Starten des ProcessMonitorWorker");
            throw;
        }
    }

    private void InitializeLogPaths()
    {
        try
        {
            string baseLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ProcessMonitorService"
            );

            Directory.CreateDirectory(baseLogPath);

            string date = DateTime.Now.ToString("yyyyMMdd");
            _processLogPath = Path.Combine(baseLogPath, $"{date}_processes.json");
            _errorLogPath = Path.Combine(baseLogPath, $"{date}_errors.log");

            _logger.LogInformation($"Process Log Pfad: {_processLogPath}");
            _logger.LogInformation($"Error Log Pfad: {_errorLogPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Initialisieren der Log-Pfade");
            throw;
        }
    }

    private async Task TestLoggingFunctionality()
    {
        try
        {
            _logger.LogInformation("=== LOGGING FUNKTIONALITÄTS-TEST ===");

            // Test verschiedener Log-Level
            _logger.LogInformation("✓ Information Level Test");
            _logger.LogWarning("⚠ Warning Level Test");
            _logger.LogError("✗ Error Level Test (Testfehler - alles OK!)");

            // Test der Prozess-Log-Datei - ANHÄNGEN statt überschreiben
            var testData = new
            {
                TestEntry = true,
                Timestamp = DateTime.Now.ToString("o"),
                Message = "Service erfolgreich gestartet - Test-Eintrag"
            };

            // GEÄNDERT: AppendAllTextAsync statt WriteAllTextAsync
            await File.AppendAllTextAsync(_processLogPath, JsonConvert.SerializeObject(testData) + Environment.NewLine);
            _logger.LogInformation($"✓ Test-Eintrag in Prozess-Log angehangen: {_processLogPath}");

            // Test der Error-Log-Datei - auch hier anhängen
            await File.AppendAllTextAsync(_errorLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Service gestartet - Test-Eintrag{Environment.NewLine}");
            _logger.LogInformation($"✓ Test-Eintrag in Error-Log angehangen: {_errorLogPath}");

            _logger.LogInformation("=== LOGGING TEST ABGESCHLOSSEN ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Testen der Logging-Funktionalität");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("ProcessMonitorWorker ExecuteAsync gestartet");

            string baseQuery = "TargetInstance ISA 'Win32_Process'";
            string creationQuery = $"SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE {baseQuery}";
            string deletionQuery = $"SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE {baseQuery}";

            _startWatcher = new ManagementEventWatcher(new WqlEventQuery(creationQuery));
            _stopWatcher = new ManagementEventWatcher(new WqlEventQuery(deletionQuery));

            _startWatcher.EventArrived += (s, e) => LogProcessEvent(e, "Start");
            _stopWatcher.EventArrived += (s, e) => LogProcessEvent(e, "Stop");

            _logger.LogInformation("WMI Event Watcher werden gestartet...");
            _startWatcher.Start();
            _stopWatcher.Start();
            _logger.LogInformation("✓ WMI Event Watcher erfolgreich gestartet");

            // Status-Updates alle 5 Minuten
            int statusCounter = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);

                statusCounter++;
                if (statusCounter >= 300) // 5 Minuten = 300 Sekunden
                {
                    _logger.LogInformation($"Service läuft normal - Aktive Prozesse im Cache: {processSidCache.Count}");
                    statusCounter = 0;

                    // Log-Rotation prüfen (täglich)
                    CheckLogRotation();
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ProcessMonitorWorker wurde ordnungsgemäß beendet");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KRITISCHER FEHLER in ProcessMonitorWorker ExecuteAsync");

            // Kritische Fehler auch in separate Error-Datei schreiben
            try
            {
                var errorEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - KRITISCHER FEHLER: {ex}{Environment.NewLine}";
                File.AppendAllText(_errorLogPath, errorEntry);
            }
            catch { }

            throw;
        }
    }

    private void CheckLogRotation()
    {
        try
        {
            string currentDate = DateTime.Now.ToString("yyyyMMdd");
            string expectedProcessLogPath = Path.Combine(
                Path.GetDirectoryName(_processLogPath)!,
                $"{currentDate}_processes.json"
            );

            if (_processLogPath != expectedProcessLogPath)
            {
                _logger.LogInformation($"Log-Rotation: Wechsel zu neuem Log-File: {expectedProcessLogPath}");
                _processLogPath = expectedProcessLogPath;

                string expectedErrorLogPath = Path.Combine(
                    Path.GetDirectoryName(_errorLogPath)!,
                    $"{currentDate}_errors.log"
                );
                _errorLogPath = expectedErrorLogPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler bei Log-Rotation Check");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== ProcessMonitorWorker wird beendet ===");

        try
        {
            _startWatcher?.Stop();
            _stopWatcher?.Stop();
            _startWatcher?.Dispose();
            _stopWatcher?.Dispose();

            _logger.LogInformation("✓ WMI Event Watcher erfolgreich beendet");
            _logger.LogInformation($"✓ Service beendet um: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _logger.LogInformation($"✓ Prozesse im Cache beim Beenden: {processSidCache.Count}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Beenden des ProcessMonitorWorker");
        }

        await base.StopAsync(cancellationToken);
    }

    private void LogProcessEvent(EventArrivedEventArgs e, string eventType)
    {
        try
        {
            var process = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            string name = process["Name"]?.ToString() ?? "";

            // Filter anwenden, wenn vorhanden
            if (_processFilter.Any() && !_processFilter.Contains(name, StringComparer.OrdinalIgnoreCase))
                return;

            int pid = Convert.ToInt32(process["ProcessId"]);
            string sid = eventType == "Start"
                ? GetProcessOwnerSidCim(pid)
                : processSidCache.TryGetValue(pid, out var cachedSid) ? cachedSid : "UNKNOWN";

            // Cache aktualisieren
            if (eventType == "Start")
            {
                processSidCache[pid] = sid;
            }
            else if (eventType == "Stop")
            {
                processSidCache.TryRemove(pid, out _);
            }

            var data = new
            {
                UserSid = sid,
                EventType = eventType,
                ProcessId = pid,
                Name = name,
                ExecutablePath = process["ExecutablePath"]?.ToString(),
                CommandLine = process["CommandLine"]?.ToString(),
                TimeGenerated = DateTime.Now.ToString("o"),
                ServiceInfo = new
                {
                    ServiceName = "ProcessMonitorService",
                    Version = "1.0",
                    MachineName = Environment.MachineName
                }
            };

            // In Prozess-Log schreiben - hier wird korrekt angehangen
            File.AppendAllText(_processLogPath, JsonConvert.SerializeObject(data) + Environment.NewLine);

            // Service-interne Statistik loggen
            if (processSidCache.Count % 100 == 0) // Alle 100 Prozesse
            {
                _logger.LogInformation($"Prozess-Event verarbeitet: {eventType} - {name} (PID: {pid}) - Cache-Größe: {processSidCache.Count}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Fehler beim Verarbeiten von Prozess-Event ({eventType})");

            // Fehler auch in Error-Log schreiben
            try
            {
                var errorEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Prozess-Event Fehler ({eventType}): {ex.Message}{Environment.NewLine}";
                File.AppendAllText(_errorLogPath, errorEntry);
            }
            catch { }
        }
    }

    private static string GetProcessOwnerSidCim(int pid)
    {
        try
        {
            using var session = Microsoft.Management.Infrastructure.CimSession.Create(null);
            var query = $"SELECT * FROM Win32_Process WHERE ProcessId = {pid}";
            var result = session.QueryInstances(@"root\cimv2", "WQL", query).FirstOrDefault();
            if (result == null) return "UNKNOWN";

            var methodResult = session.InvokeMethod(@"root\cimv2", result, "GetOwnerSid", null);
            return methodResult?.OutParameters?["Sid"]?.Value?.ToString() ?? "UNKNOWN";
        }
        catch
        {
            return "ERROR";
        }
    }
}