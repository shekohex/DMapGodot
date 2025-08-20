using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;

namespace DMapImporter.Core.Logging
{
    [ProviderAlias("Godot")]
    public class GodotLoggerProvider : ILoggerProvider
    {
        private readonly GodotLoggingOptions _options;
        private readonly ConcurrentDictionary<string, GodotLogger> _loggers = new();
        private bool _disposed = false;

        public GodotLoggerProvider(GodotLoggingOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public ILogger CreateLogger(string categoryName)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GodotLoggerProvider));

            return _loggers.GetOrAdd(categoryName, name => new GodotLogger(name, _options));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _loggers.Clear();
                _disposed = true;
            }
        }
    }
}