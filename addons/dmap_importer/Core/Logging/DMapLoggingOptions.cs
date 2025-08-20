using Microsoft.Extensions.Logging;

namespace DMapImporter.Core.Logging
{
    public class DMapLoggingOptions
    {
        public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
        
        public bool EnableConsoleLogging { get; set; } = true;
        
        public bool EnableGodotLogging { get; set; } = true;
        
        public bool EnableFileLogging { get; set; } = false;
        
        public FileLoggingOptions File { get; set; } = new();
        
        public GodotLoggingOptions Godot { get; set; } = new();
    }
    
    public class FileLoggingOptions
    {
        public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
        
        public string LogDirectory { get; set; } = "user://logs/";
        
        public string LogFileNamePattern { get; set; } = "dmap_{0:yyyy-MM-dd_HH-mm-ss}.log";
        
        public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB
        
        public int MaxLogFiles { get; set; } = 5;
        
        public bool IncludeScopes { get; set; } = true;
        
        public bool EnableRotation { get; set; } = true;
    }
    
    public class GodotLoggingOptions
    {
        public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
        
        public bool UseGodotColors { get; set; } = true;
        
        public bool IncludeScopes { get; set; } = true;
        
        public bool UseRichText { get; set; } = false;
    }
}