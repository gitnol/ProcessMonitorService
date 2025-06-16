using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Management.Infrastructure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

// Configuration Models
public class ProcessMonitorOptions
{
    public List<string> ProcessFilters { get; set; } = new();
    public int CacheExpiryMinutes { get; set; } = 30;
    public int StatusUpdateIntervalMinutes { get; set; } = 5;
    public int CacheCleanupIntervalMinutes { get; set; } = 10;
}

// Services
public interface IProcessOwnerService
{
    Task<string> GetProcessOwnerSidAsync(int processId);
}

public class ProcessOwnerService : IProcessOwnerService, IDisposable
{
    private readonly ILogger<ProcessOwnerService> _logger;
    private readonly Lazy<CimSession> _cimSession;
    private bool _disposed = false;

    public ProcessOwnerService(ILogger<ProcessOwnerService> logger)
    {
        _logger = logger;
        _cimSession = new Lazy<CimSession>(() => CimSession.Create(null));
    }

    public async Task<string> GetProcessOwnerSidAsync(int processId)
    {
        try
        {
            return await Task.Run(() =>
            {
                var query = $"SELECT * FROM Win32_Process WHERE ProcessId = {processId}";
                var result = _cimSession.Value.QueryInstances(@"root\cimv2", "WQL", query).FirstOrDefault();

                if (result == null)
                {
                    _logger.LogError("Process with ID {ProcessId} not found.", processId);
                    return "UNKNOWN_PROCESS_NOT_FOUND";
                }

                try
                {
                    var methodResult = _cimSession.Value.InvokeMethod(result, "GetOwnerSid", null);
                    return methodResult?.OutParameters?["Sid"]?.Value?.ToString() ?? "UNKNOWN_SID_NOT_FOUND";
                }
                catch (CimException cimEx)
                {
                    _logger.LogError(cimEx, "CIM method 'GetOwnerSid' failed for process {ProcessId}", processId);
                    return "ERROR_GETTING_SID";
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SID for process {ProcessId}", processId);
            return "ERROR_GETTING_SID";
        }
    }


    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            if (_cimSession.IsValueCreated)
            {
                _cimSession.Value?.Dispose();
            }
        }
        _disposed = true;
    }
}

// Cache Entry Model
public class ProcessCacheEntry
{
    public string Sid { get; set; } = string.Empty;
    public DateTime LastAccess { get; set; } = DateTime.UtcNow;

    public void UpdateAccess()
    {
        LastAccess = DateTime.UtcNow;
    }
}

// Main Worker Class
public class ProcessMonitorWorker : BackgroundService
{
    private readonly ILogger<ProcessMonitorWorker> _logger;
    private readonly IOptionsMonitor<ProcessMonitorOptions> _optionsMonitor;
    private readonly IProcessOwnerService _processOwnerService;

    private HashSet<string> _processFilterSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, ProcessCacheEntry> _processSidCache = new();

    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;
    private Timer? _cacheCleanupTimer;
    private Timer? _statusTimer;

    private ProcessMonitorOptions _currentOptions;
    private bool _disposed = false;

    public ProcessMonitorWorker(
        ILogger<ProcessMonitorWorker> logger,
        IOptionsMonitor<ProcessMonitorOptions> optionsMonitor,
        IProcessOwnerService processOwnerService)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _processOwnerService = processOwnerService;
        _currentOptions = _optionsMonitor.CurrentValue;

        UpdateProcessFilters(_currentOptions.ProcessFilters);

        // Configuration change handler
        _optionsMonitor.OnChange(options =>
        {
            _logger.LogInformation("Configuration changed, reloading settings");
            _currentOptions = options;
            UpdateProcessFilters(options.ProcessFilters);
        });
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== ProcessMonitorWorker starting ===");
        _logger.LogInformation("Base directory: {BaseDirectory}", AppContext.BaseDirectory);
        _logger.LogInformation("User context: {UserName}", Environment.UserName);
        _logger.LogInformation("Active process filters: {FilterCount}", _processFilterSet.Count);

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await InitializeWmiWatchersAsync(stoppingToken);
            InitializeTimers();

            _logger.LogInformation("✓ ProcessMonitorWorker successfully started");

            // Keep service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ProcessMonitorWorker stopping gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "CRITICAL ERROR in ProcessMonitorWorker. Service may become unstable");
            throw; // Re-throw to trigger service restart
        }
    }

    private async Task InitializeWmiWatchersAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            try
            {
                // Dynamische WQL-Query basierend auf den konfigurierten Filtern
                string processFilter = BuildProcessFilter();

                string baseQuery = string.IsNullOrEmpty(processFilter)
                    ? "TargetInstance ISA 'Win32_Process'"
                    : $"TargetInstance ISA 'Win32_Process' AND ({processFilter})";

                string creationQuery = $"SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE {baseQuery}";
                string deletionQuery = $"SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE {baseQuery}";

                _startWatcher = new ManagementEventWatcher(new WqlEventQuery(creationQuery));
                _stopWatcher = new ManagementEventWatcher(new WqlEventQuery(deletionQuery));

                _startWatcher.EventArrived += OnProcessStarted;
                _stopWatcher.EventArrived += OnProcessStopped;

                _logger.LogInformation("Starting WMI event watchers with filter: {ProcessFilter}",
                    string.IsNullOrEmpty(processFilter) ? "ALL PROCESSES" : processFilter);
                _startWatcher.Start();
                _stopWatcher.Start();
                _logger.LogInformation("✓ WMI event watchers started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize WMI watchers");
                throw;
            }
        }, cancellationToken);
    }

    private string BuildProcessFilter()
    {
        if (!_processFilterSet.Any())
        {
            return string.Empty; // Keine Filter = alle Prozesse überwachen
        }

        // WQL OR-Bedingungen für alle gefilterten Prozesse erstellen
        var conditions = _processFilterSet
            .Select(processName =>
            {
                // Prüfen, ob der Prozessname Wildcards (* oder ?) enthält
                if (processName.Contains('*') || processName.Contains('?'))
                {
                    // WQL LIKE-Operator für Wildcard-Unterstützung verwenden
                    // Asterisk (*) entspricht % in WQL, Fragezeichen (?) entspricht _ in WQL
                    var wqlPattern = processName.Replace("*", "%").Replace("?", "_");
                    return $"TargetInstance.Name LIKE '{wqlPattern}'";
                }
                else
                {
                    // Exakte Übereinstimmung für Filter ohne Wildcards
                    return $"TargetInstance.Name = '{processName}'";
                }
            })
            .ToArray();

        return string.Join(" OR ", conditions);
    }

    private void UpdateProcessFilters(List<string> filters)
    {
        _processFilterSet = new HashSet<string>(filters ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        _logger.LogInformation("Process filters updated: {FilterCount} filters loaded", _processFilterSet.Count);
        _logger.LogDebug("Active filters: {@ProcessFilters}", _processFilterSet.ToArray());

        // WMI-Watcher neu initialisieren, wenn sie bereits laufen
        if (_startWatcher != null || _stopWatcher != null)
        {
            _logger.LogInformation("Reinitializing WMI watchers due to filter change");
            RestartWatchers();
        }
    }

    private void RestartWatchers()
    {
        try
        {
            // Alte Watcher stoppen und dispose
            _startWatcher?.Stop();
            _stopWatcher?.Stop();
            _startWatcher?.Dispose();
            _stopWatcher?.Dispose();

            // Neu initialisieren
            InitializeWmiWatchersAsync(CancellationToken.None).Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting WMI watchers");
        }
    }
    private void InitializeTimers()
    {
        // Cache cleanup timer
        var cleanupInterval = TimeSpan.FromMinutes(_currentOptions.CacheCleanupIntervalMinutes);
        _cacheCleanupTimer = new Timer(CleanupExpiredCacheEntries, null, cleanupInterval, cleanupInterval);

        // Status update timer
        var statusInterval = TimeSpan.FromMinutes(_currentOptions.StatusUpdateIntervalMinutes);
        _statusTimer = new Timer(LogStatusUpdate, null, statusInterval, statusInterval);

        _logger.LogInformation("Timers initialized - Cache cleanup: {CleanupInterval}min, Status: {StatusInterval}min",
            _currentOptions.CacheCleanupIntervalMinutes, _currentOptions.StatusUpdateIntervalMinutes);
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            _ = Task.Run(async () => await HandleProcessEventAsync(e, "Start"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in process start event handler");
        }
    }

    private void OnProcessStopped(object sender, EventArrivedEventArgs e)
    {
        try
        {
            _ = Task.Run(async () => await HandleProcessEventAsync(e, "Stop"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in process stop event handler");
        }
    }

    private async Task HandleProcessEventAsync(EventArrivedEventArgs e, string eventType)
    {
        try
        {
            var process = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            var name = process["Name"]?.ToString() ?? "N/A";

            // Filter check - early return if not monitored
            if (_processFilterSet.Any() && !_processFilterSet.Contains(name))
            {
                return;
            }

            var pid = Convert.ToInt32(process["ProcessId"]);
            var sid = "UNKNOWN";

            if (eventType == "Start")
            {
                sid = await _processOwnerService.GetProcessOwnerSidAsync(pid);
                _processSidCache[pid] = new ProcessCacheEntry { Sid = sid };
            }
            else if (eventType == "Stop")
            {
                if (_processSidCache.TryRemove(pid, out var cachedEntry))
                {
                    sid = cachedEntry.Sid;
                }
            }

            LogProcessEvent(eventType, name, pid, sid, process);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling process event: {EventType}", eventType);
        }
    }

    private void LogProcessEvent(string eventType, string name, int pid, string sid, ManagementBaseObject process)
    {
        _logger.LogInformation(
            "Process {EventType}: {ProcessName} (PID: {ProcessId}) User: {UserSid} Path: {ExecutablePath} Command: {CommandLine}",
            eventType,
            name,
            pid,
            sid,
            process["ExecutablePath"]?.ToString() ?? "N/A",
            process["CommandLine"]?.ToString() ?? "N/A"
        );
    }

    private void CleanupExpiredCacheEntries(object? state)
    {
        try
        {
            var expiryTime = TimeSpan.FromMinutes(_currentOptions.CacheExpiryMinutes);
            var cutoffTime = DateTime.UtcNow - expiryTime;

            var expiredKeys = _processSidCache
                .Where(kvp => kvp.Value.LastAccess < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            var removedCount = 0;
            foreach (var key in expiredKeys)
            {
                if (_processSidCache.TryRemove(key, out _))
                {
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                _logger.LogDebug("Cache cleanup: removed {RemovedCount} expired entries, {RemainingCount} entries remaining",
                    removedCount, _processSidCache.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
        }
    }

    private void LogStatusUpdate(object? state)
    {
        try
        {
            _logger.LogInformation("Service status: Running. Cache entries: {CacheCount}, Active filters: {FilterCount}",
                _processSidCache.Count, _processFilterSet.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during status update");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== ProcessMonitorWorker stopping ===");

        try
        {
            // Stop timers
            _cacheCleanupTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _statusTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            // Stop WMI watchers
            _startWatcher?.Stop();
            _stopWatcher?.Stop();

            _logger.LogInformation("✓ WMI event watchers stopped successfully");
            _logger.LogInformation("✓ Service stopped at: {StopTime}. Final cache count: {CacheCount}",
                DateTime.Now, _processSidCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during service shutdown");
        }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                _startWatcher?.Dispose();
                _stopWatcher?.Dispose();
                _cacheCleanupTimer?.Dispose();
                _statusTimer?.Dispose();
                (_processOwnerService as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during disposal");
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
