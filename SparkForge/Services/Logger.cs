using System.Diagnostics;
using System.Text;

namespace SparkForge.Services;

/// <summary>
/// File-based logger with automatic rotation (keeps last 5 logs, max 5MB each).
/// Also writes critical errors to Windows Event Log.
/// </summary>
public static class Logger
{
    private static readonly string LogDir;
    private static readonly string LogFile;
    private static readonly object _lock = new();
    private const int MaxLogSize = 5 * 1024 * 1024; // 5 MB
    private const int MaxLogFiles = 5;

    public enum Level { Debug, Info, Warn, Error, Fatal }

    static Logger()
    {
        LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SparkForge",
            "logs");
        Directory.CreateDirectory(LogDir);
        LogFile = Path.Combine(LogDir, "launcher.log");
        RotateIfNeeded();
    }

    public static void Debug(string message) => Write(Level.Debug, message, null);
    public static void Info(string message) => Write(Level.Info, message, null);
    public static void Warn(string message) => Write(Level.Warn, message, null);
    public static void Error(string message, Exception? ex = null) => Write(Level.Error, message, ex);
    public static void Fatal(string message, Exception? ex = null) => Write(Level.Fatal, message, ex);

    private static void Write(Level level, string message, Exception? ex)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelStr = level.ToString().ToUpper().PadRight(5);
        var threadId = Environment.CurrentManagedThreadId;

        var sb = new StringBuilder();
        sb.Append($"[{timestamp}] [{levelStr}] [T{threadId}] {message}");
        if (ex != null)
        {
            sb.AppendLine();
            sb.Append(ex.ToString());
        }

        var line = sb.ToString();

        lock (_lock)
        {
            try
            {
                File.AppendAllText(LogFile, line + Environment.NewLine);
                RotateIfNeeded();
            }
            catch { /* can't log the logger failure */ }
        }

        // Write fatals and errors to Windows Event Log
        if (level >= Level.Error)
        {
            try
            {
                EventLog.WriteEntry("SparkForge", line, level == Level.Fatal
                    ? EventLogEntryType.Error
                    : EventLogEntryType.Warning);
            }
            catch { /* EventLog may not be available */ }
        }

        // Also emit to debug output for live debugging
        System.Diagnostics.Debug.WriteLine(line);
    }

    private static void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(LogFile)) return;
            var info = new FileInfo(LogFile);
            if (info.Length < MaxLogSize) return;

            // Rotate: launcher.log -> launcher.1.log, etc.
            for (var i = MaxLogFiles - 1; i >= 0; i--)
            {
                var oldFile = i == 0
                    ? LogFile
                    : Path.Combine(LogDir, $"launcher.{i}.log");
                var newFile = Path.Combine(LogDir, $"launcher.{i + 1}.log");

                if (File.Exists(oldFile))
                {
                    if (i == MaxLogFiles - 1)
                        File.Delete(oldFile);
                    else
                        File.Move(oldFile, newFile, overwrite: true);
                }
            }
        }
        catch { }
    }

    public static string GetLogPath() => LogFile;

    public static void OpenLogsFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = LogDir,
                UseShellExecute = true
            });
        }
        catch { }
    }
}
