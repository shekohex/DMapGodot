using Microsoft.Extensions.Logging;
using System;

namespace DMapImporter.Core.Logging
{
    public static class DMapLoggerFactory
    {
        private static ILoggerFactory? _instance;
        private static readonly object _lock = new();

        public static ILoggerFactory Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= CreateDefault();
                    }
                }
                return _instance;
            }
        }

        public static ILogger<T> CreateLogger<T>()
            => Instance.CreateLogger<T>();

        public static ILogger CreateLogger(string categoryName)
            => Instance.CreateLogger(categoryName);

        public static ILogger CreateLogger(Type type)
            => Instance.CreateLogger(type);

        public static ILoggerFactory CreateDefault()
            => Create(new DMapLoggingOptions());

        public static ILoggerFactory Create(DMapLoggingOptions options)
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(options.MinimumLevel);

                if (options.EnableConsoleLogging)
                {
                    builder.AddConsole();
                }

                if (options.EnableGodotLogging)
                {
                    builder.AddProvider(new GodotLoggerProvider(options.Godot));
                }

                if (options.EnableFileLogging)
                {
                    builder.AddProvider(new RotatingFileLoggerProvider(options.File));
                }

                // Add filters for common noisy categories
                builder.AddFilter("System", LogLevel.Warning);
                builder.AddFilter("Microsoft", LogLevel.Warning);
            });

            return factory;
        }

        public static void Configure(DMapLoggingOptions options)
        {
            lock (_lock)
            {
                _instance?.Dispose();
                _instance = Create(options);
            }
        }

        public static void Configure(Action<DMapLoggingOptions> configureOptions)
        {
            var options = new DMapLoggingOptions();
            configureOptions(options);
            Configure(options);
        }

        public static void Reset()
        {
            lock (_lock)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }

        public static bool IsGodotAvailable()
        {
            try
            {
                var gdType = Type.GetType("Godot.GD, GodotSharp");
                return gdType != null;
            }
            catch
            {
                return false;
            }
        }

        public static DMapLoggingOptions CreateGodotOptimizedOptions()
        {
            var isGodot = IsGodotAvailable();
            
            return new DMapLoggingOptions
            {
                MinimumLevel = LogLevel.Information,
                EnableConsoleLogging = !isGodot, // Only use console when not in Godot
                EnableGodotLogging = isGodot,
                EnableFileLogging = true,
                File = new FileLoggingOptions
                {
                    MinimumLevel = LogLevel.Information,
                    LogDirectory = isGodot ? "user://logs/" : "logs/",
                    MaxFileSizeBytes = 5 * 1024 * 1024, // 5MB
                    MaxLogFiles = 3,
                    EnableRotation = true
                },
                Godot = new GodotLoggingOptions
                {
                    MinimumLevel = LogLevel.Information,
                    UseGodotColors = true,
                    UseRichText = false,
                    IncludeScopes = true
                }
            };
        }

        public static DMapLoggingOptions CreateDevelopmentOptions()
        {
            var baseOptions = CreateGodotOptimizedOptions();
            baseOptions.MinimumLevel = LogLevel.Debug;
            baseOptions.File.MinimumLevel = LogLevel.Debug;
            baseOptions.Godot.MinimumLevel = LogLevel.Debug;
            return baseOptions;
        }

        public static DMapLoggingOptions CreateProductionOptions()
        {
            var baseOptions = CreateGodotOptimizedOptions();
            baseOptions.MinimumLevel = LogLevel.Information;
            baseOptions.EnableConsoleLogging = false; // Disable console in production
            baseOptions.File.MaxLogFiles = 10;
            baseOptions.File.MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB
            return baseOptions;
        }
    }
}