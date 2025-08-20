using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DMapImporter.Core.Logging
{
    public class RotatingFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly FileLoggingOptions _options;
        private readonly object _lock = new();
        private string? _currentLogFilePath;

        public RotatingFileLogger(string categoryName, FileLoggingOptions options)
        {
            _categoryName = categoryName;
            _options = options;
            EnsureLogDirectory();
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => new LoggerScope(state);

        public bool IsEnabled(LogLevel logLevel)
            => logLevel >= _options.MinimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            lock (_lock)
            {
                try
                {
                    var logEntry = FormatLogEntry(logLevel, _categoryName, eventId, formatter(state, exception), exception);
                    WriteToFile(logEntry);
                }
                catch
                {
                    // Silent fail to prevent logging errors from breaking the application
                }
            }
        }

        private void EnsureLogDirectory()
        {
            var logDir = GetGodotUserPath(_options.LogDirectory);
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
        }

        private string GetGodotUserPath(string userPath)
        {
            if (!userPath.StartsWith("user://"))
                return userPath;

            try
            {
                // Try to get Godot's user directory
                var osType = Type.GetType("Godot.OS, GodotSharp");
                if (osType != null)
                {
                    var getUserDataDirMethod = osType.GetMethod("GetUserDataDir");
                    if (getUserDataDirMethod != null)
                    {
                        var userDataDir = getUserDataDirMethod.Invoke(null, null) as string;
                        if (!string.IsNullOrEmpty(userDataDir))
                        {
                            return Path.Combine(userDataDir, userPath.Substring(7)); // Remove "user://"
                        }
                    }
                }
            }
            catch
            {
                // Fall back to current directory if Godot not available
            }

            // Fallback to application directory + logs
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", userPath.Substring(7));
        }

        private void WriteToFile(string logEntry)
        {
            EnsureCurrentLogFile();

            if (_currentLogFilePath == null)
                return;

            File.AppendAllText(_currentLogFilePath, logEntry + Environment.NewLine, Encoding.UTF8);

            if (_options.EnableRotation && ShouldRotateLog())
            {
                RotateLogFile();
            }
        }

        private void EnsureCurrentLogFile()
        {
            if (_currentLogFilePath != null && File.Exists(_currentLogFilePath))
                return;

            var logDir = GetGodotUserPath(_options.LogDirectory);
            var fileName = string.Format(_options.LogFileNamePattern, DateTime.UtcNow);
            _currentLogFilePath = Path.Combine(logDir, fileName);
        }

        private bool ShouldRotateLog()
        {
            if (_currentLogFilePath == null || !File.Exists(_currentLogFilePath))
                return false;

            var fileInfo = new FileInfo(_currentLogFilePath);
            return fileInfo.Length > _options.MaxFileSizeBytes;
        }

        private void RotateLogFile()
        {
            var logDir = GetGodotUserPath(_options.LogDirectory);
            CleanupOldLogFiles(logDir);
            _currentLogFilePath = null; // Force creation of new log file
        }

        private void CleanupOldLogFiles(string logDir)
        {
            try
            {
                var logFiles = Directory.GetFiles(logDir, "dmap_*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .ToArray();

                if (logFiles.Length >= _options.MaxLogFiles)
                {
                    var filesToDelete = logFiles.Skip(_options.MaxLogFiles - 1);
                    foreach (var file in filesToDelete)
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch
                        {
                            // Ignore errors when deleting old log files
                        }
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private static string FormatLogEntry(LogLevel logLevel, string categoryName, EventId eventId, string message, Exception? exception)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var level = GetLogLevelString(logLevel);
            var eventIdStr = eventId.Id != 0 ? $"[{eventId.Id}]" : "";

            var logEntry = $"{timestamp} [{level}] {categoryName}{eventIdStr}: {message}";

            if (exception != null)
            {
                logEntry += Environment.NewLine + exception.ToString();
            }

            return logEntry;
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "TRCE",
                LogLevel.Debug => "DBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "FAIL",
                LogLevel.Critical => "CRIT",
                LogLevel.None => "NONE",
                _ => "UNKN"
            };
        }

        private class LoggerScope : IDisposable
        {
            public LoggerScope(object? state)
            {
                // Store scope state if needed for scope support
            }

            public void Dispose()
            {
                // Cleanup scope if needed
            }
        }
    }
}