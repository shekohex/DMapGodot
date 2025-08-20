using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;

namespace DMapImporter.Core.Logging
{
    [ProviderAlias("RotatingFile")]
    public class RotatingFileLoggerProvider : ILoggerProvider
    {
        private readonly FileLoggingOptions _options;
        private readonly ConcurrentDictionary<string, RotatingFileLogger> _loggers = new();
        private bool _disposed = false;

        public RotatingFileLoggerProvider(FileLoggingOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public ILogger CreateLogger(string categoryName)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RotatingFileLoggerProvider));

            return _loggers.GetOrAdd(categoryName, name => new RotatingFileLogger(name, _options));
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