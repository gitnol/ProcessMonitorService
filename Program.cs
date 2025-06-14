using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;

try
{
    await Host.CreateDefaultBuilder(args)
        .UseWindowsService()
        .ConfigureServices((hostContext, services) =>
        {
            services.AddHostedService<ProcessMonitorWorker>();
        })
        .ConfigureLogging((context, logging) =>
        {
            logging.ClearProviders();

            // Console Logging für Debugging (nur wenn nicht als Service läuft)
            if (Environment.UserInteractive)
            {
                logging.AddConsole();
            }

            // File Logging Setup
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ProcessMonitorService",
                "logs"
            );

            try
            {
                Directory.CreateDirectory(logDirectory);

                // Strukturiertes JSON Logging für detaillierte Analyse
                var jsonLogPath = Path.Combine(logDirectory, $"service_{DateTime.Now:yyyyMMdd}.json");
                logging.AddProvider(new JsonFileLoggerProvider(jsonLogPath));

                // Einfaches Text Logging für schnelle Übersicht
                var textLogPath = Path.Combine(logDirectory, $"service_{DateTime.Now:yyyyMMdd}.log");
                logging.AddProvider(new TextFileLoggerProvider(textLogPath));

                Console.WriteLine($"Logging konfiguriert:");
                Console.WriteLine($"  JSON: {jsonLogPath}");
                Console.WriteLine($"  Text: {textLogPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Konfigurieren des File-Logging: {ex.Message}");
                // Fallback auf Console only
                logging.AddConsole();
            }

            // Log Level konfiguration
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddFilter("Microsoft", LogLevel.Warning);
            logging.AddFilter("System", LogLevel.Warning);
        })
        .Build()
        .RunAsync();
}
catch (Exception ex)
{
    var errorMessage = $"Kritischer Fehler beim Starten des Services: {ex}";
    Console.WriteLine(errorMessage);

    // Versuch, den Fehler in eine Datei zu schreiben
    try
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ProcessMonitorService",
            "logs"
        );
        Directory.CreateDirectory(logDirectory);

        var errorLogPath = Path.Combine(logDirectory, $"startup_error_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        File.WriteAllText(errorLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - STARTUP ERROR\n{errorMessage}\n");

        Console.WriteLine($"Startup-Fehler geschrieben nach: {errorLogPath}");
    }
    catch
    {
        Console.WriteLine("Konnte Startup-Fehler nicht in Datei schreiben");
    }

    throw;
}

// JSON File Logger Provider
public class JsonFileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly object _lock = new object();

    public JsonFileLoggerProvider(string filePath)
    {
        _filePath = filePath;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new JsonFileLogger(_filePath, categoryName, _lock);
    }

    public void Dispose() { }
}

public class JsonFileLogger : ILogger
{
    private readonly string _filePath;
    private readonly string _categoryName;
    private readonly object _lock;

    public JsonFileLogger(string filePath, string categoryName, object lockObject)
    {
        _filePath = filePath;
        _categoryName = categoryName;
        _lock = lockObject;
    }

    // Explizite Implementierung für BeginScope - Constraints werden vom Interface übernommen
    IDisposable? ILogger.BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    // Korrekte Signatur mit nullable Exception-Parameter
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                           Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var logEntry = new
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Level = logLevel.ToString(),
            Category = _categoryName,
            EventId = eventId.Id,
            Message = formatter(state, exception),
            Exception = exception?.ToString()
        };

        var jsonLine = System.Text.Json.JsonSerializer.Serialize(logEntry);

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_filePath, jsonLine + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Fallback: Console output wenn File-Schreibung fehlschlägt
                Console.WriteLine($"Log write error: {ex.Message}");
                Console.WriteLine($"Original message: {logEntry.Message}");
            }
        }
    }
}

// Text File Logger Provider  
public class TextFileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly object _lock = new object();

    public TextFileLoggerProvider(string filePath)
    {
        _filePath = filePath;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TextFileLogger(_filePath, categoryName, _lock);
    }

    public void Dispose() { }
}

public class TextFileLogger : ILogger
{
    private readonly string _filePath;
    private readonly string _categoryName;
    private readonly object _lock;

    public TextFileLogger(string filePath, string categoryName, object lockObject)
    {
        _filePath = filePath;
        _categoryName = categoryName;
        _lock = lockObject;
    }

    // Explizite Implementierung für BeginScope - Constraints werden vom Interface übernommen
    IDisposable? ILogger.BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    // Korrekte Signatur mit nullable Exception-Parameter
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                           Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {_categoryName}: {formatter(state, exception)}";

        if (exception != null)
        {
            message += Environment.NewLine + "Exception: " + exception.ToString();
        }

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_filePath, message + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Log write error: {ex.Message}");
                Console.WriteLine($"Original message: {message}");
            }
        }
    }
}