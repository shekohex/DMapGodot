using Microsoft.Extensions.Logging;
using System;
using System.Reflection;

namespace DMapImporter.Core.Logging
{
    public class GodotLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly GodotLoggingOptions _options;
        private static bool? _isGodotAvailable;
        private static MethodInfo? _gdPrintMethod;
        private static MethodInfo? _gdPrintErrMethod;
        private static MethodInfo? _gdPrintRichMethod;

        public GodotLogger(string categoryName, GodotLoggingOptions options)
        {
            _categoryName = categoryName;
            _options = options;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => new LoggerScope(state);

        public bool IsEnabled(LogLevel logLevel)
            => IsGodotAvailable && logLevel >= _options.MinimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var formattedMessage = FormatMessage(logLevel, _categoryName, eventId, message, exception);

            try
            {
                if (logLevel >= LogLevel.Error && _gdPrintErrMethod != null)
                {
                    _gdPrintErrMethod.Invoke(null, new object[] { formattedMessage });
                }
                else if (_options.UseRichText && _gdPrintRichMethod != null)
                {
                    var richMessage = ApplyRichTextFormatting(formattedMessage, logLevel);
                    _gdPrintRichMethod.Invoke(null, new object[] { richMessage });
                }
                else if (_gdPrintMethod != null)
                {
                    _gdPrintMethod.Invoke(null, new object[] { formattedMessage });
                }
            }
            catch
            {
                // Silent fail to prevent logging errors from breaking the application
            }
        }

        private static bool IsGodotAvailable
        {
            get
            {
                if (_isGodotAvailable.HasValue)
                    return _isGodotAvailable.Value;

                try
                {
                    var gdType = Type.GetType("Godot.GD, GodotSharp");
                    if (gdType != null)
                    {
                        _gdPrintMethod = gdType.GetMethod("Print", new[] { typeof(object) });
                        _gdPrintErrMethod = gdType.GetMethod("PrintErr", new[] { typeof(object) });
                        _gdPrintRichMethod = gdType.GetMethod("PrintRich", new[] { typeof(object) });

                        if (_gdPrintMethod != null && _gdPrintErrMethod != null)
                        {
                            _isGodotAvailable = true;
                            return true;
                        }
                    }
                }
                catch
                {
                    // Godot not available
                }

                _isGodotAvailable = false;
                return false;
            }
        }

        private string FormatMessage(LogLevel logLevel, string categoryName, EventId eventId, string message, Exception? exception)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var level = GetLogLevelString(logLevel);
            var eventIdStr = eventId.Id != 0 ? $"[{eventId.Id}]" : "";
            
            var formattedMessage = $"{timestamp} [{level}] {categoryName}{eventIdStr}: {message}";
            
            if (exception != null)
            {
                formattedMessage += $"\nException: {exception.Message}";
                if (!string.IsNullOrEmpty(exception.StackTrace))
                {
                    formattedMessage += $"\nStack Trace:\n{exception.StackTrace}";
                }
            }
            
            return formattedMessage;
        }

        private string ApplyRichTextFormatting(string message, LogLevel logLevel)
        {
            if (!_options.UseRichText || !_options.UseGodotColors)
                return message;

            var color = logLevel switch
            {
                LogLevel.Trace => "gray",
                LogLevel.Debug => "lightgray", 
                LogLevel.Information => "white",
                LogLevel.Warning => "yellow",
                LogLevel.Error => "red",
                LogLevel.Critical => "purple",
                _ => "white"
            };

            return $"[color={color}]{message}[/color]";
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