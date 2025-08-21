#if TOOLS
using Godot;
using Microsoft.Extensions.Logging;
using DMapImporter.Importers;
using DMapImporter.Core.Logging;
using DMapImporter.Editor;
using DMapImporter.Nodes;

[Tool]
public partial class DMapImporterPlugin : EditorPlugin
{
    private DMapImporter.Importers.DMapImporter? _dmapImporter;
    private DMapEditorDock? _editorDock;
    private readonly ILogger<DMapImporterPlugin> _logger;
    private System.Action? _selectionChangedCallback;

    public DMapImporterPlugin()
    {
        // Configure logging for Godot editor environment
        var options = new DMapLoggingOptions
        {
            MinimumLevel = LogLevel.Information,
            EnableGodotLogging = true,
            EnableFileLogging = true,
            EnableConsoleLogging = true // Disable console in Godot
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

        // Create and add editor dock
        _editorDock = new DMapEditorDock();
        AddControlToDock(DockSlot.LeftUr, _editorDock);
        _logger.LogInformation("DMap Editor Dock added");

        // Connect to scene changes to update dock
        var editorSelection = EditorInterface.Singleton.GetSelection();
        _selectionChangedCallback = OnSelectionChanged;
        editorSelection.SelectionChanged += _selectionChangedCallback;
    }

    public override void _ExitTree()
    {
        // Disconnect selection changes using stored callback
        if (_selectionChangedCallback != null)
        {
            var editorSelection = EditorInterface.Singleton.GetSelection();
            editorSelection.SelectionChanged -= _selectionChangedCallback;
            _selectionChangedCallback = null;
        }

        // Remove editor dock
        if (_editorDock != null)
        {
            RemoveControlFromDocks(_editorDock);
            _editorDock.QueueFree();
            _editorDock = null;
            _logger.LogInformation("DMap Editor Dock removed");
        }

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

    private void OnSelectionChanged()
    {
        // Update dock when selection changes
        var selection = EditorInterface.Singleton.GetSelection();
        var selectedNodes = selection.GetSelectedNodes();

        DMapRenderer? renderer = null;

        foreach (Node node in selectedNodes)
        {
            if (node is DMapRenderer dmapRenderer)
            {
                renderer = dmapRenderer;
                break;
            }
        }

        _editorDock?.SetCurrentRenderer(renderer);
    }
}
#endif
