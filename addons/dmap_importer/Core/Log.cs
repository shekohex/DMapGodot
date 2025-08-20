using System;
using System.Reflection;

namespace DMapImporter.Core
{
    /// <summary>
    /// Cross-platform logging utility that works in both Godot and standalone .NET environments
    /// </summary>
    public static class Log
    {
        private static bool? _isGodotAvailable;
        private static MethodInfo? _gdPrintMethod;
        private static MethodInfo? _gdPrintErrMethod;
        
        /// <summary>
        /// Detects if Godot engine is available and caches reflection methods
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
                        
                        if (_gdPrintMethod != null && _gdPrintErrMethod != null)
                        {
                            _isGodotAvailable = true;
                            return true;
                        }
                    }
                }
                catch
                {
                    // Godot not available - this is normal in unit tests
                }
                
                _isGodotAvailable = false;
                return false;
            }
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        public static void Error(string message)
        {
            if (IsGodotAvailable)
            {
                try
                {
                    _gdPrintErrMethod!.Invoke(null, new object[] { $"[ERROR] {message}" });
                    return;
                }
                catch
                {
                    // Fall through to console if Godot call fails
                }
            }
            Console.WriteLine($"[ERROR] {message}");
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public static void Warn(string message)
        {
            if (IsGodotAvailable)
            {
                try
                {
                    _gdPrintMethod!.Invoke(null, new object[] { $"[WARN] {message}" });
                    return;
                }
                catch
                {
                    // Fall through to console if Godot call fails
                }
            }
            Console.WriteLine($"[WARN] {message}");
        }

        /// <summary>
        /// Logs an info message
        /// </summary>
        public static void Info(string message)
        {
            if (IsGodotAvailable)
            {
                try
                {
                    _gdPrintMethod!.Invoke(null, new object[] { $"[INFO] {message}" });
                    return;
                }
                catch
                {
                    // Fall through to console if Godot call fails
                }
            }
            Console.WriteLine($"[INFO] {message}");
        }
    }
}