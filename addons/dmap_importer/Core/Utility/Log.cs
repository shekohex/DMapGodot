using System;
using System.Reflection;

namespace DMapImporter.Core.Utility
{
    /// <summary>
    /// Comprehensive logging utility that auto-detects Godot and provides appropriate logging
    /// Supports Debug, Info, Warn, and Error levels with fallback to Console
    /// </summary>
    public static class Log
    {
        private static bool? _isGodotAvailable;
        private static MethodInfo? _gdPrintMethod;
        private static MethodInfo? _gdPrintErrMethod;
        private static MethodInfo? _gdPrintRichMethod;
        
        /// <summary>
        /// Log level enumeration for filtering output
        /// </summary>
        public enum LogLevel
        {
            Debug = 0,
            Info = 1,
            Warn = 2,
            Error = 3
        }
        
        /// <summary>
        /// Current minimum log level (Debug messages disabled by default for performance)
        /// </summary>
        public static LogLevel MinimumLogLevel { get; set; } = LogLevel.Info;
        
        /// <summary>
        /// Detects if Godot engine is available and caches reflection methods for performance
        /// </summary>
        private static bool IsGodotAvailable
        {
            get
            {
                if (_isGodotAvailable.HasValue)
                    return _isGodotAvailable.Value;

                try
                {
                    // Try to get Godot.GD type from GodotSharp assembly
                    var gdType = Type.GetType("Godot.GD, GodotSharp");
                    if (gdType != null)
                    {
                        // Cache the methods for better performance
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
                    // Godot not available - this is normal in unit tests and standalone environments
                }
                
                _isGodotAvailable = false;
                return false;
            }
        }
        
        /// <summary>
        /// Logs a debug message (lowest priority, for detailed diagnostics)
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Debug(string message)
        {
            if (MinimumLogLevel > LogLevel.Debug)
                return;
                
            LogInternal(LogLevel.Debug, message, ConsoleColor.Gray);
        }
        
        /// <summary>
        /// Logs an informational message
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Info(string message)
        {
            if (MinimumLogLevel > LogLevel.Info)
                return;
                
            LogInternal(LogLevel.Info, message, ConsoleColor.White);
        }
        
        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Warn(string message)
        {
            if (MinimumLogLevel > LogLevel.Warn)
                return;
                
            LogInternal(LogLevel.Warn, message, ConsoleColor.Yellow);
        }
        
        /// <summary>
        /// Logs an error message (highest priority)
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Error(string message)
        {
            LogInternal(LogLevel.Error, message, ConsoleColor.Red, true);
        }
        
        /// <summary>
        /// Logs an error message with exception details
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="exception">The exception to log</param>
        public static void Error(string message, Exception exception)
        {
            var fullMessage = $"{message}: {exception.Message}";
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                fullMessage += $"\nStack Trace:\n{exception.StackTrace}";
            }
            
            LogInternal(LogLevel.Error, fullMessage, ConsoleColor.Red, true);
        }
        
        /// <summary>
        /// Internal logging implementation that handles both Godot and Console output
        /// </summary>
        /// <param name="level">The log level</param>
        /// <param name="message">The message to log</param>
        /// <param name="consoleColor">Console color for non-Godot environments</param>
        /// <param name="isError">Whether this is an error message (uses PrintErr in Godot)</param>
        private static void LogInternal(LogLevel level, string message, ConsoleColor consoleColor, bool isError = false)
        {
            var levelPrefix = level switch
            {
                LogLevel.Debug => "[DEBUG]",
                LogLevel.Info => "[INFO]",
                LogLevel.Warn => "[WARN]",
                LogLevel.Error => "[ERROR]",
                _ => "[LOG]"
            };
            
            var formattedMessage = $"{levelPrefix} {message}";
            
            if (IsGodotAvailable)
            {
                try
                {
                    // Use Godot's logging system
                    if (isError && _gdPrintErrMethod != null)
                    {
                        _gdPrintErrMethod.Invoke(null, new object[] { formattedMessage });
                    }
                    else if (_gdPrintMethod != null)
                    {
                        _gdPrintMethod.Invoke(null, new object[] { formattedMessage });
                    }
                    return;
                }
                catch
                {
                    // Fall through to console if Godot call fails
                }
            }
            
            // Fallback to console output with colors
            try
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = consoleColor;
                
                if (isError)
                {
                    Console.Error.WriteLine(formattedMessage);
                }
                else
                {
                    Console.WriteLine(formattedMessage);
                }
                
                Console.ForegroundColor = originalColor;
            }
            catch
            {
                // Ultimate fallback - basic console output without colors
                if (isError)
                {
                    Console.Error.WriteLine(formattedMessage);
                }
                else
                {
                    Console.WriteLine(formattedMessage);
                }
            }
        }
        
        /// <summary>
        /// Legacy method for backwards compatibility
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="backColor">Background color (ignored in new implementation)</param>
        /// <param name="textColor">Text color (ignored in new implementation)</param>
        [Obsolete("Use Error(string message) instead")]
        public static void Error(string message, ConsoleColor backColor, ConsoleColor textColor)
        {
            Error(message);
        }
        
        /// <summary>
        /// Legacy method for backwards compatibility  
        /// </summary>
        /// <param name="message">The warning message</param>
        /// <param name="backColor">Background color (ignored in new implementation)</param>
        /// <param name="textColor">Text color (ignored in new implementation)</param>
        [Obsolete("Use Warn(string message) instead")]
        public static void Warn(string message, ConsoleColor backColor, ConsoleColor textColor)
        {
            Warn(message);
        }
        
        /// <summary>
        /// Sets the minimum log level for filtering output
        /// </summary>
        /// <param name="level">Minimum level to log</param>
        public static void SetMinimumLogLevel(LogLevel level)
        {
            MinimumLogLevel = level;
        }
        
        /// <summary>
        /// Enables debug logging (sets minimum level to Debug)
        /// </summary>
        public static void EnableDebugLogging()
        {
            MinimumLogLevel = LogLevel.Debug;
        }
        
        /// <summary>
        /// Disables all logging except errors
        /// </summary>
        public static void SetQuietMode()
        {
            MinimumLogLevel = LogLevel.Error;
        }
        
        /// <summary>
        /// Gets information about the current logging environment
        /// </summary>
        /// <returns>Environment info string</returns>
        public static string GetEnvironmentInfo()
        {
            var environment = IsGodotAvailable ? "Godot Engine" : "Standalone Console";
            return $"Logging Environment: {environment}, Min Level: {MinimumLogLevel}";
        }
    }
}