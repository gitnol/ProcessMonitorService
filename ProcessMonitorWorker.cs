using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Management;
using Newtonsoft.Json;
using System.Collections.Concurrent;


public class ProcessMonitorWorker : BackgroundService
{
    static ConcurrentDictionary<int, string> processSidCache = new();

    private readonly ILogger<ProcessMonitorWorker> _logger;
    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;

    public ProcessMonitorWorker(ILogger<ProcessMonitorWorker> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string logFilePath = GetLogFilePath();
        string baseQuery = "TargetInstance ISA 'Win32_Process'";
        string creationQuery = $"SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE {baseQuery}";
        string deletionQuery = $"SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE {baseQuery}";

        _startWatcher = new ManagementEventWatcher(new WqlEventQuery(creationQuery));
        _stopWatcher = new ManagementEventWatcher(new WqlEventQuery(deletionQuery));

        _startWatcher.EventArrived += (s, e) => LogProcessEvent(e, logFilePath, "Start");
        _stopWatcher.EventArrived += (s, e) => LogProcessEvent(e, logFilePath, "Stop");

        _startWatcher.Start();
        _stopWatcher.Start();

        return Task.Run(() =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Thread.Sleep(100);
            }
        }, stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _startWatcher?.Stop(); _stopWatcher?.Stop();
        _startWatcher?.Dispose(); _stopWatcher?.Dispose();
        return base.StopAsync(cancellationToken);
    }

    private void LogProcessEvent(EventArrivedEventArgs e, string logFilePath, string eventType)
    {
        try
        {
            var process = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            int pid = Convert.ToInt32(process["ProcessId"]);
            string sid = eventType == "Start"
                ? GetProcessOwnerSid(process)
                : processSidCache.TryGetValue(pid, out var cachedSid) ? cachedSid : "UNKNOWN";

            if (eventType == "Start") processSidCache[pid] = sid;
            if (eventType == "Stop") processSidCache.TryRemove(pid, out _);

            var data = new
            {
                UserSid = sid,
                EventType = eventType,
                ProcessId = process["ProcessId"],
                Name = process["Name"],
                ExecutablePath = process["ExecutablePath"],
                CommandLine = process["CommandLine"],
                TimeGenerated = DateTime.Now.ToString("o")
            };

            File.AppendAllText(logFilePath, JsonConvert.SerializeObject(data) + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Loggen.");
        }
    }

    private static string GetProcessOwnerSid(ManagementBaseObject process)
    {
        try
        {
            using var classInstance = new ManagementObject($"Win32_Process.Handle='{process["ProcessId"]}'");
            var outParams = classInstance.InvokeMethod("GetOwnerSid", null, null);
            return outParams?["Sid"]?.ToString() ?? "UNKNOWN";
        }
        catch
        {
            return "ERROR";
        }
    }

    private static string GetLogFilePath()
    {
        string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ProcessMonitorService");
        string date = DateTime.Now.ToString("yyyyMMdd");

        if (!Directory.Exists(basePath))
            Directory.CreateDirectory(basePath);

        return Path.Combine(basePath, $"{date}_processes.json");
    }


}
