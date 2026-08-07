using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
//唉wchaoy，这个日志格式改了好几遍巨难看给我气笑了，最后改 - 了
namespace NetCraft.Logging;

public enum LogLevel
{
    Debug, Info, Warning, Error, Critical, None
}

public static class LogColors
{
    public const string Debug = "\u001b[36m";
    public const string Info = "\u001b[32m";
    public const string Warning = "\u001b[33m";
    public const string Error = "\u001b[31m";
    public const string Critical = "\u001b[35m";
    public const string Reset = "\u001b[0m";
}

public static class LogPlaceholders
{
    public const string Timestamp = "{timestamp}";
    public const string Level = "{level}";
    public const string Source = "{source}";
    public const string File = "{file}";
    public const string Line = "{line}";
    public const string Member = "{member}";
    public const string Message = "{message}";
    public const string NewLine = "{newline}";
    public const string Tab = "{tab}";
    public const string Space = "{space}";
}

public static class Log
{
    private static readonly Stack<string> _logSourceStack = new();
    private static string _defaultLogSource = "Unknown";
    private static readonly Dictionary<Type, string> _classSourceCache = new();

    public static void SetClassSource<T>() => SetClassSource(typeof(T));

    public static void SetClassSource(Type type)
    {
        if (type == null) return;
        var fullName = type.FullName ?? type.Name;
        lock (_classSourceCache)
        {
            if (!_classSourceCache.ContainsKey(type))
                _classSourceCache[type] = fullName;
            _defaultLogSource = fullName;
            lock (_logSourceStack)
            {
                if (_logSourceStack.Count == 0)
                    _logSourceStack.Push(fullName);
                else
                {
                    _logSourceStack.Pop();
                    _logSourceStack.Push(fullName);
                }
            }
        }
    }

    public static IDisposable PushMethodSource<T>([CallerMemberName] string methodName = "")
    {
        var className = typeof(T).FullName ?? typeof(T).Name;
        return PushSource($"{className}.{methodName}");
    }

    public static IDisposable PushMethodSource([CallerMemberName] string methodName = "")
    {
        var frame = new System.Diagnostics.StackFrame(1);
        var declaringType = frame.GetMethod()?.DeclaringType;
        if (declaringType != null)
        {
            var className = declaringType.FullName ?? declaringType.Name;
            return PushSource($"{className}.{methodName}");
        }
        return PushSource(methodName);
    }

    public static void SetDefaultSource(string source) => _defaultLogSource = source;

    public static IDisposable PushSource(string source) => new LogSourceScope(source);

    private static string GetCurrentSource()
    {
        lock (_logSourceStack)
            return _logSourceStack.Count > 0 ? _logSourceStack.Peek() : _defaultLogSource;
    }

    private sealed class LogSourceScope : IDisposable
    {
        public LogSourceScope(string source)
        {
            lock (_logSourceStack)
                _logSourceStack.Push(source);
        }

        public void Dispose()
        {
            lock (_logSourceStack)
            {
                if (_logSourceStack.Count > 0)
                    _logSourceStack.Pop();
            }
        }
    }

    private static string _logDirectory = null!;
    private static LogLevel _consoleLevel = LogLevel.Debug;
    private static LogLevel _fileLevel = LogLevel.Debug;
    private static StreamWriter _fileWriter = null!;
    private static readonly object _lock = new();
    private static readonly object _consoleLock = new();

    public static event Action<string>? OnLogOutput;

    private static int _warningCount;
    private static int _errorCount;

    public static int WarningCount => _warningCount;
    public static int ErrorCount => _errorCount;

    private static string? _lastWarningMessage;
    private static string? _lastErrorMessage;

    public static string? LastWarningMessage => _lastWarningMessage;
    public static string? LastErrorMessage => _lastErrorMessage;

    private static bool _enableFileLogging = true;

    private static string _consoleFormat = $"{LogPlaceholders.Timestamp} - {LogPlaceholders.Level} - [{LogPlaceholders.Source}][{LogPlaceholders.File}:{LogPlaceholders.Line}] - {LogPlaceholders.Message}";
    private static string _fileFormat = $"{LogPlaceholders.Timestamp} - {LogPlaceholders.Level} - [{LogPlaceholders.Source}][{LogPlaceholders.File}:{LogPlaceholders.Line}] - {LogPlaceholders.Message}";
    private static string _timestampFormat = "yyyy-MM-dd HH:mm:ss,fff";

    private static int _retentionDays = 30;

    static Log()
    {
        EnableVirtualTerminalSupport();
        SetLogDirectory(null);
    }

    public static void SetRetentionDays(int days)
    {
        if (days < 0) days = 0;
        _retentionDays = days;
    }

    public static int GetRetentionDays() => _retentionDays;

    private static void CleanupOldLogs()
    {
        if (_retentionDays <= 0 || string.IsNullOrEmpty(_logDirectory))
            return;
        try
        {
            if (!Directory.Exists(_logDirectory))
                return;
            var logFiles = Directory.GetFiles(_logDirectory, "*.log")
                .Select(f => new FileInfo(f))
                .ToList();
            if (logFiles.Count == 0)
                return;
            var cutoffDate = DateTime.Now.AddDays(-_retentionDays);
            var deletedCount = 0;
            foreach (var file in logFiles)
            {
                if (file.CreationTime < cutoffDate)
                {
                    try
                    {
                        file.Delete();
                        deletedCount++;
                    }
                    catch
                    {
                    }
                }
            }
            if (deletedCount > 0)
                WriteCleanupLog($"[Log Cleanup] Deleted {deletedCount} old log file(s) (retention: {_retentionDays} days)");
        }
        catch
        {
        }
    }

    private static void WriteCleanupLog(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString(_timestampFormat);
            var logMessage = $"{timestamp} - INFO - [LogSystem] - {message}";
            lock (_consoleLock)
                Console.WriteLine($"{LogColors.Info}{logMessage}{LogColors.Reset}");
            if (_enableFileLogging && _fileWriter != null)
            {
                lock (_lock)
                {
                    try
                    {
                        _fileWriter.WriteLine(logMessage);
                    }
                    catch
                    {
                    }
                }
            }
        }
        catch
        {
        }
    }

    public static void ManualCleanup() => CleanupOldLogs();

    public static void SetConsoleFormat(string format) => _consoleFormat = format;
    public static void SetFileFormat(string format) => _fileFormat = format;
    public static void SetTimestampFormat(string format) => _timestampFormat = format;

    public static void ResetToDefaultFormats()
    {
        _consoleFormat = $"{LogPlaceholders.Timestamp} - {LogPlaceholders.Level} - [{LogPlaceholders.Source}][{LogPlaceholders.File}:{LogPlaceholders.Line}] - {LogPlaceholders.Message}";
        _fileFormat = $"{LogPlaceholders.Timestamp} - {LogPlaceholders.Level} - [{LogPlaceholders.Source}][{LogPlaceholders.File}:{LogPlaceholders.Line}] - {LogPlaceholders.Message}";
        _timestampFormat = "yyyy-MM-dd HH:mm:ss,fff";
    }

    public static void SetLogDirectory(string? directory)
    {
        lock (_lock)
        {
            CloseFileWriter();
            _logDirectory = string.IsNullOrEmpty(directory)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs")
                : directory;
            if (_enableFileLogging)
                InitializeFileWriter();
        }
    }

    private static void InitializeFileWriter()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
                Directory.CreateDirectory(_logDirectory);
            CleanupOldLogs();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var logFile = Path.Combine(_logDirectory, $"{timestamp}.log");
            _fileWriter = new StreamWriter(logFile, true, System.Text.Encoding.UTF8);
            _fileWriter.AutoFlush = true;
        }
        catch
        {
            _enableFileLogging = false;
            _fileWriter = null!;
        }
    }

    private static void CloseFileWriter()
    {
        if (_fileWriter == null) return;
        try
        {
            _fileWriter.Flush();
            _fileWriter.Close();
            _fileWriter.Dispose();
        }
        catch
        {
        }
        finally
        {
            _fileWriter = null!;
        }
    }

    public static void EnableFileLogging(bool enable)
    {
        lock (_lock)
        {
            if (_enableFileLogging == enable) return;
            _enableFileLogging = enable;
            if (enable)
            {
                if (!string.IsNullOrEmpty(_logDirectory))
                    InitializeFileWriter();
            }
            else
                CloseFileWriter();
        }
    }

    public static bool IsFileLoggingEnabled => _enableFileLogging && _fileWriter != null;

    public static void SetConsoleLevel(LogLevel level) => _consoleLevel = level;
    public static void SetFileLevel(LogLevel level) => _fileLevel = level;

    private static string GetAnsiColor(LogLevel level) => level switch
    {
        LogLevel.Debug => LogColors.Debug,
        LogLevel.Info => LogColors.Info,
        LogLevel.Warning => LogColors.Warning,
        LogLevel.Error => LogColors.Error,
        LogLevel.Critical => LogColors.Critical,
        _ => LogColors.Reset
    };

    private static void WriteColoredLine(LogLevel level, string message)
    {
        var color = GetAnsiColor(level);
        var coloredMessage = $"{color}{message}{LogColors.Reset}";
        OnLogOutput?.Invoke(coloredMessage);
        lock (_consoleLock)
            Console.WriteLine(coloredMessage);
    }

    private static string ExtractFileName(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return "unknown";
        var lastSeparator = filePath.LastIndexOfAny(new[] { '\\', '/' });
        return lastSeparator >= 0 && lastSeparator < filePath.Length - 1
            ? filePath.Substring(lastSeparator + 1)
            : filePath;
    }

    private static string FormatLogMessage(string format, LogLevel level, string message, string filePath, int lineNumber, string memberName)
    {
        var fileName = ExtractFileName(filePath);
        var timestamp = DateTime.Now.ToString(_timestampFormat);
        var levelName = level.ToString().ToUpper();
        var source = GetCurrentSource();
        return format
            .Replace(LogPlaceholders.Timestamp, timestamp)
            .Replace(LogPlaceholders.Level, levelName)
            .Replace(LogPlaceholders.Source, source)
            .Replace(LogPlaceholders.File, fileName)
            .Replace(LogPlaceholders.Line, lineNumber.ToString())
            .Replace(LogPlaceholders.Member, memberName ?? "unknown")
            .Replace(LogPlaceholders.Message, message)
            .Replace(LogPlaceholders.NewLine, Environment.NewLine)
            .Replace(LogPlaceholders.Tab, "\t")
            .Replace(LogPlaceholders.Space, " ");
    }

    private static void WriteLog(LogLevel level, string message, string filePath, int lineNumber, string memberName)
    {
        if (level == LogLevel.Warning) System.Threading.Interlocked.Increment(ref _warningCount);
        if (level == LogLevel.Error || level == LogLevel.Critical) System.Threading.Interlocked.Increment(ref _errorCount);

        if (level == LogLevel.Warning)
            _lastWarningMessage = message;
        else if (level == LogLevel.Error || level == LogLevel.Critical)
            _lastErrorMessage = message;

        if (_consoleLevel != LogLevel.None && level >= _consoleLevel)
        {
            var consoleMessage = FormatLogMessage(_consoleFormat, level, message, filePath, lineNumber, memberName);
            WriteColoredLine(level, consoleMessage);
        }

        if (_enableFileLogging && _fileWriter != null && _fileLevel != LogLevel.None && level >= _fileLevel)
        {
            var fileMessage = FormatLogMessage(_fileFormat, level, message, filePath, lineNumber, memberName);
            lock (_lock)
            {
                try
                {
                    _fileWriter.WriteLine(fileMessage);
                }
                catch
                {
                    _enableFileLogging = false;
                    CloseFileWriter();
                }
            }
        }
    }

    public static void Debug(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
        => WriteLog(LogLevel.Debug, message, filePath, lineNumber, memberName);

    public static void Info(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
        => WriteLog(LogLevel.Info, message, filePath, lineNumber, memberName);

    public static void Warning(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
        => WriteLog(LogLevel.Warning, message, filePath, lineNumber, memberName);

    public static void Error(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
        => WriteLog(LogLevel.Error, message, filePath, lineNumber, memberName);

    public static void Critical(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
        => WriteLog(LogLevel.Critical, message, filePath, lineNumber, memberName);

    public static void Exception(Exception ex, string? message = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
    {
        var msg = message == null ? ex.ToString() : $"{message}: {ex.Message}\n{ex.StackTrace}";
        WriteLog(LogLevel.Error, msg, filePath, lineNumber, memberName);
    }

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessingFlag = 0x0004;

    private static void EnableVirtualTerminalSupport()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var handle = GetStdHandle(StdOutputHandle);
            if (handle == IntPtr.Zero || handle == (IntPtr)(-1)) return;
            if (!GetConsoleMode(handle, out var mode)) return;
            var newMode = mode | EnableVirtualTerminalProcessingFlag;
            if (newMode != mode) SetConsoleMode(handle, newMode);
        }
        catch
        {
        }
    }
}