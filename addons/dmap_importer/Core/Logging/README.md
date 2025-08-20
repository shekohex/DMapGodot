# DMapImporter Logging System

## Overview

The DMapImporter now uses a modern, configurable logging system based on Microsoft.Extensions.Logging, following .NET best practices. The system provides:

- **Godot Integration**: Automatic detection and integration with Godot's logging system
- **Rotating File Logging**: Configurable file logging with automatic rotation to `user://logs/`
- **Multiple Log Levels**: Trace, Debug, Information, Warning, Error, Critical
- **Structured Logging**: Support for structured logging with parameters
- **Backward Compatibility**: Legacy static `Log` class still works

## Quick Start

### Basic Usage (Backward Compatible)

```csharp
using DMapImporter.Core.Utility;

// Static methods still work
Log.Info("Information message");
Log.Error("Error message", exception);
Log.Debug("Debug message with parameter: {param}", "value");
```

### Modern Usage (Recommended)

```csharp
using Microsoft.Extensions.Logging;
using DMapImporter.Core.Utility;

public class MyClass
{
    private readonly ILogger _logger = Log.CreateLogger<MyClass>();
    
    public void DoWork()
    {
        _logger.LogInformation("Starting work");
        
        try
        {
            // Do work
            _logger.LogDebug("Work completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Work failed");
        }
    }
}
```

## Configuration

### Development Configuration

```csharp
Log.ConfigureForDevelopment(); // Enables debug logging, file logging
```

### Production Configuration

```csharp
Log.ConfigureForProduction(); // Information level, optimized for performance
```

### Custom Configuration

```csharp
Log.Configure(options =>
{
    options.MinimumLevel = LogLevel.Information;
    options.EnableGodotLogging = true;
    options.EnableFileLogging = true;
    options.EnableConsoleLogging = false;
    
    // Configure file logging
    options.File.LogDirectory = "user://custom_logs/";
    options.File.MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB
    options.File.MaxLogFiles = 10;
    
    // Configure Godot logging
    options.Godot.UseRichText = true;
    options.Godot.UseGodotColors = true;
});
```

## Features

### Automatic Environment Detection

The logging system automatically detects:
- **Godot Environment**: Uses `GD.Print()` and `GD.PrintErr()`
- **Standalone Environment**: Uses console logging with colors

### Rotating File Logging

- **Location**: `user://logs/` (Godot) or `logs/` (standalone)
- **Rotation**: Automatic when files exceed size limit
- **Cleanup**: Maintains maximum number of log files
- **Format**: Timestamped entries with structured data

### Log Levels

| Level | Usage |
|-------|-------|
| `Trace` | Most detailed, typically disabled |
| `Debug` | Development debugging |
| `Information` | General operational messages |
| `Warning` | Potential issues that don't stop execution |
| `Error` | Errors that stop current operation |
| `Critical` | Critical failures requiring immediate attention |

### Structured Logging

```csharp
_logger.LogInformation("User {UserId} performed action {Action} at {Timestamp}", 
    userId, actionName, DateTime.UtcNow);
```

### Scoped Logging

```csharp
using (_logger.BeginScope("ImportOperation"))
{
    _logger.LogInformation("Starting import");
    // All logs within this scope will include the scope information
}
```

## Architecture

### Components

- **DMapLoggerFactory**: Central factory for creating loggers
- **GodotLoggerProvider**: Integrates with Godot's logging system
- **RotatingFileLoggerProvider**: File logging with rotation
- **DMapLoggingOptions**: Configuration options
- **Log**: Backward-compatible static facade

### File Structure

```
Core/
├── Logging/
│   ├── DMapLoggerFactory.cs      # Main factory
│   ├── DMapLoggingOptions.cs     # Configuration classes
│   ├── GodotLogger.cs            # Godot integration
│   ├── GodotLoggerProvider.cs    # Godot provider
│   ├── RotatingFileLogger.cs     # File logging implementation
│   ├── RotatingFileLoggerProvider.cs # File provider
│   └── Log.cs                    # Backward-compatible facade
```

## Migration Guide

### From Old System

Old code using the legacy system continues to work:

```csharp
// This still works
Log.Error("Error message");
Log.Warn("Warning message");
```

### To New System

For new code, prefer the modern approach:

```csharp
// Create logger once per class
private readonly ILogger _logger = Log.CreateLogger<MyClass>();

// Use throughout the class
_logger.LogError("Error occurred");
_logger.LogWarning("Warning message");
```

## Performance

- **Lazy Initialization**: Loggers created on-demand
- **Efficient Filtering**: Messages filtered before formatting
- **Minimal Allocation**: Structured logging avoids string concatenation
- **Async-Safe**: Thread-safe implementation

## Best Practices

1. **Use structured logging** with parameters instead of string concatenation
2. **Create one logger per class** using `Log.CreateLogger<T>()`
3. **Use appropriate log levels** - avoid Debug/Trace in production
4. **Configure logging early** in application startup
5. **Use scopes** for grouping related operations
6. **Handle exceptions** properly with `LogError(exception, message)`

## Troubleshooting

### File Logging Not Working

Check that the log directory has write permissions and sufficient disk space.

### Godot Logging Not Appearing

Verify that `options.EnableGodotLogging = true` and Godot environment is detected.

### Performance Issues

- Reduce log level in production
- Disable Debug/Trace logging
- Adjust file rotation settings