#if TOOLS
using Godot;
using Microsoft.Extensions.Logging;
using DMapImporter.Importers;
using DMapImporter.Core.Logging;

[Tool]
public partial class DMapImporterPlugin : EditorPlugin
{
    private DMapImporter.Importers.DMapImporter? _dmapImporter;
    private readonly ILogger<DMapImporterPlugin> _logger;

    public DMapImporterPlugin()
    {
        // Configure logging for Godot editor environment
        var options = new DMapLoggingOptions
        {
            MinimumLevel = LogLevel.Information,
            EnableGodotLogging = true,
            EnableFileLogging = true,
            EnableConsoleLogging = false // Disable console in Godot
        };

        var loggerFactory = DMapLoggerFactory.Create(options);
        _logger = loggerFactory.CreateLogger<DMapImporterPlugin>();
    }

    public override void _EnterTree()
    {
        // Register DMap importer
        _dmapImporter = new DMapImporter.Importers.DMapImporter();
        AddImportPlugin(_dmapImporter);
        _logger.LogInformation("DMAP Importer plugin registered");

        // Add custom node type for DMapRenderer
        var script = GD.Load<Script>("res://addons/dmap_importer/Nodes/DMapRenderer.cs");
        var icon = GD.Load<Texture2D>("res://addons/dmap_importer/icons/dmap.svg");
        AddCustomType("DMapRenderer", "Node2D", script, icon);
        _logger.LogInformation("DMapRenderer custom type registered");
    }

    public override void _ExitTree()
    {
        if (_dmapImporter != null)
        {
            RemoveImportPlugin(_dmapImporter);
            _dmapImporter = null;
            _logger.LogInformation("DMAP Importer plugin unregistered");
        }

        // Remove custom node type
        RemoveCustomType("DMapRenderer");
        _logger.LogInformation("DMapRenderer custom type unregistered");
    }
}
#endif
