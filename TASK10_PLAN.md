# Task 10: Create Editor Dock UI - Implementation Plan

## Overview
Build editor dock interface for live tile property editing following PRD Phase 5 Section 8.1 (lines 400-459). This implements the complete DMapEditorDock class with Control inheritance, comprehensive UI elements, and real-time tile property editing capabilities.

## Technical Requirements

### Core Features
- **Control Inheritance**: Extend Godot's Control class with `[Tool]` attribute
- **Minimum Size**: CustomMinimumSize of 250x0 pixels
- **Layout**: VBoxContainer for organized vertical layout
- **Property Editors**:
  - SpinBox for height editing (-100 to 100 range)
  - OptionButton for surface types (Grass/Stone/Water)
  - CheckBox for walkability toggle
- **Real-time Updates**: Live property synchronization with selected tiles
- **Editor Integration**: Proper dock registration with EditorPlugin

### Dependencies
- Task 8 (Update Plugin Entry Point) - **COMPLETED**
- Existing DMapRenderer for tile display
- Core/Dmap/Tile.cs for tile data structure
- DMapImporterPlugin for dock registration

## Detailed Implementation Steps

### Step 1: Create Editor Directory Structure
```
addons/dmap_importer/
└── Editor/
    └── DMapEditorDock.cs
```

### Step 2: Implement DMapEditorDock Class

#### 2.1 Base Structure
```csharp
#if TOOLS
using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Nodes;
using System;

namespace DMapImporter.Editor
{
    [Tool]
    public partial class DMapEditorDock : Control
    {
        // Current map reference
        private DMapRenderer? _currentRenderer;
        private Vector2I _selectedTile = new Vector2I(-1, -1);
        
        // UI Controls
        private VBoxContainer? _mainContainer;
        private Label? _titleLabel;
        private Label? _coordinatesLabel;
        private SpinBox? _heightEditor;
        private OptionButton? _surfaceSelector;
        private CheckBox? _walkableToggle;
        private Button? _applyButton;
        
        // Tile data cache
        private Tile? _currentTileData;
        private bool _isUpdating = false;
    }
}
#endif
```

#### 2.2 UI Setup in _Ready()
```csharp
public override void _Ready()
{
    // Set minimum size for dock
    CustomMinimumSize = new Vector2(250, 0);
    
    // Create main container
    _mainContainer = new VBoxContainer();
    _mainContainer.AddThemeConstantOverride("separation", 8);
    AddChild(_mainContainer);
    
    // Title section
    _titleLabel = new Label() { Text = "Tile Properties" };
    _titleLabel.AddThemeStyleboxOverride("normal", new StyleBoxFlat());
    _mainContainer.AddChild(_titleLabel);
    
    // Add separator
    _mainContainer.AddChild(new HSeparator());
    
    // Coordinates display
    _coordinatesLabel = new Label() { Text = "No tile selected" };
    _coordinatesLabel.AddThemeColorOverride("font_color", Colors.Gray);
    _mainContainer.AddChild(_coordinatesLabel);
    
    // Height editor section
    var heightLabel = new Label() { Text = "Height:" };
    _mainContainer.AddChild(heightLabel);
    
    _heightEditor = new SpinBox();
    _heightEditor.MinValue = -100;
    _heightEditor.MaxValue = 100;
    _heightEditor.Step = 1;
    _heightEditor.Value = 0;
    _heightEditor.Editable = false;
    _heightEditor.ValueChanged += OnHeightChanged;
    _mainContainer.AddChild(_heightEditor);
    
    // Surface type section
    var surfaceLabel = new Label() { Text = "Surface Type:" };
    _mainContainer.AddChild(surfaceLabel);
    
    _surfaceSelector = new OptionButton();
    _surfaceSelector.AddItem("Grass");   // Index 0
    _surfaceSelector.AddItem("Stone");   // Index 1
    _surfaceSelector.AddItem("Water");   // Index 2
    _surfaceSelector.Disabled = true;
    _surfaceSelector.Selected += OnSurfaceChanged;
    _mainContainer.AddChild(_surfaceSelector);
    
    // Walkable toggle section
    _walkableToggle = new CheckBox();
    _walkableToggle.Text = "Walkable";
    _walkableToggle.Disabled = true;
    _walkableToggle.Toggled += OnWalkableToggled;
    _mainContainer.AddChild(_walkableToggle);
    
    // Add separator before action buttons
    _mainContainer.AddChild(new HSeparator());
    
    // Apply button (for batch operations)
    _applyButton = new Button();
    _applyButton.Text = "Apply to Selected";
    _applyButton.Disabled = true;
    _applyButton.Pressed += OnApplyPressed;
    _mainContainer.AddChild(_applyButton);
    
    // Info section
    var infoLabel = new Label();
    infoLabel.Text = "Select a tile in the viewport";
    infoLabel.AddThemeColorOverride("font_color", Colors.Gray);
    infoLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    _mainContainer.AddChild(infoLabel);
}
```

#### 2.3 Signal Handlers
```csharp
private void OnHeightChanged(double value)
{
    if (_isUpdating || _currentRenderer == null || _selectedTile.X < 0)
        return;
    
    UpdateTileProperty("height", (short)value);
}

private void OnSurfaceChanged(long index)
{
    if (_isUpdating || _currentRenderer == null || _selectedTile.X < 0)
        return;
    
    UpdateTileProperty("surface", (ushort)index);
}

private void OnWalkableToggled(bool pressed)
{
    if (_isUpdating || _currentRenderer == null || _selectedTile.X < 0)
        return;
    
    // Walkable checkbox is inverse of NoAccess
    // Checked (walkable) = NoAccess: 0
    // Unchecked (not walkable) = NoAccess: 1
    UpdateTileProperty("no_access", (ushort)(pressed ? 0 : 1));
}

private void OnApplyPressed()
{
    // Apply current settings to all selected tiles
    ApplyToAllSelected();
}
```

#### 2.4 Tile Selection Methods
```csharp
public void SetCurrentRenderer(DMapRenderer? renderer)
{
    // Disconnect from previous renderer
    if (_currentRenderer != null)
    {
        if (_currentRenderer.IsConnected("TileSelected", new Callable(this, nameof(OnTileSelected))))
        {
            _currentRenderer.Disconnect("TileSelected", new Callable(this, nameof(OnTileSelected)));
        }
    }
    
    _currentRenderer = renderer;
    
    // Connect to new renderer
    if (_currentRenderer != null)
    {
        _currentRenderer.Connect("TileSelected", new Callable(this, nameof(OnTileSelected)));
        RefreshUI();
    }
    else
    {
        ClearSelection();
    }
}

private void OnTileSelected(Vector2I tileCoords, Tile tileData)
{
    _selectedTile = tileCoords;
    _currentTileData = tileData;
    UpdateUIFromTileData();
}

private void ClearSelection()
{
    _selectedTile = new Vector2I(-1, -1);
    _currentTileData = null;
    
    _isUpdating = true;
    
    // Update UI to show no selection
    _coordinatesLabel.Text = "No tile selected";
    _coordinatesLabel.AddThemeColorOverride("font_color", Colors.Gray);
    
    // Disable controls
    _heightEditor.Editable = false;
    _heightEditor.Value = 0;
    _surfaceSelector.Disabled = true;
    _surfaceSelector.Selected = 0;
    _walkableToggle.Disabled = true;
    _walkableToggle.ButtonPressed = false;
    _applyButton.Disabled = true;
    
    _isUpdating = false;
}

private void UpdateUIFromTileData()
{
    if (_currentTileData == null)
    {
        ClearSelection();
        return;
    }
    
    _isUpdating = true;
    
    var tile = _currentTileData.Value;
    
    // Update coordinates display
    _coordinatesLabel.Text = $"Tile [{_selectedTile.X}, {_selectedTile.Y}]";
    _coordinatesLabel.AddThemeColorOverride("font_color", Colors.White);
    
    // Enable and update controls
    _heightEditor.Editable = true;
    _heightEditor.Value = tile.Height;
    
    _surfaceSelector.Disabled = false;
    _surfaceSelector.Selected = Mathf.Clamp(tile.Surface, 0, 2);
    
    _walkableToggle.Disabled = false;
    _walkableToggle.ButtonPressed = (tile.NoAccess == 0);
    
    _applyButton.Disabled = false;
    
    _isUpdating = false;
}
```

#### 2.5 Property Update Methods
```csharp
private void UpdateTileProperty(string property, object value)
{
    if (_currentRenderer == null || _selectedTile.X < 0)
        return;
    
    // Call renderer's update method
    _currentRenderer.UpdateTileProperty(_selectedTile, property, value);
    
    // Update local cache
    if (_currentTileData.HasValue)
    {
        var tile = _currentTileData.Value;
        
        switch (property)
        {
            case "height":
                tile = new Tile(tile.NoAccess, tile.Surface, (short)value);
                break;
            case "surface":
                tile = new Tile(tile.NoAccess, (ushort)value, tile.Height);
                break;
            case "no_access":
                tile = new Tile((ushort)value, tile.Surface, tile.Height);
                break;
        }
        
        _currentTileData = tile;
    }
}

private void ApplyToAllSelected()
{
    if (_currentRenderer == null || !_currentTileData.HasValue)
        return;
    
    // Get all selected tiles from renderer
    var selectedTiles = _currentRenderer.GetSelectedTiles();
    
    foreach (var tileCoord in selectedTiles)
    {
        _currentRenderer.UpdateTileProperty(tileCoord, "height", _currentTileData.Value.Height);
        _currentRenderer.UpdateTileProperty(tileCoord, "surface", _currentTileData.Value.Surface);
        _currentRenderer.UpdateTileProperty(tileCoord, "no_access", _currentTileData.Value.NoAccess);
    }
    
    GD.Print($"Applied properties to {selectedTiles.Count} tiles");
}

private void RefreshUI()
{
    if (_currentRenderer != null && _selectedTile.X >= 0)
    {
        var tileData = _currentRenderer.GetTileData(_selectedTile);
        if (tileData.HasValue)
        {
            _currentTileData = tileData;
            UpdateUIFromTileData();
        }
    }
    else
    {
        ClearSelection();
    }
}
```

### Step 3: Update DMapRenderer for Selection Support

#### 3.1 Add Selection Signals and State
```csharp
// In DMapRenderer.cs
[Signal]
public delegate void TileSelectedEventHandler(Vector2I tileCoords, Tile tileData);

[Signal]
public delegate void TileHoveredEventHandler(Vector2I tileCoords);

private Vector2I _selectedTile = new Vector2I(-1, -1);
private HashSet<Vector2I> _selectedTiles = new HashSet<Vector2I>();
private TileMapLayer? _selectionLayer;
```

#### 3.2 Add Selection Methods
```csharp
public void UpdateTileProperty(Vector2I tileCoords, string property, object value)
{
    if (_dmapFile == null || !IsValidTileCoordinate(tileCoords))
        return;
    
    var tile = _dmapFile.TileSet[tileCoords.X, tileCoords.Y];
    
    // Update tile based on property
    switch (property)
    {
        case "height":
            tile = new Tile(tile.NoAccess, tile.Surface, (short)value);
            break;
        case "surface":
            tile = new Tile(tile.NoAccess, (ushort)value, tile.Height);
            break;
        case "no_access":
            tile = new Tile((ushort)value, tile.Surface, tile.Height);
            break;
    }
    
    // Update in DMAP file
    _dmapFile.TileSet[tileCoords.X, tileCoords.Y] = tile;
    
    // Update visual representation
    RefreshTileVisual(tileCoords);
}

public Tile? GetTileData(Vector2I tileCoords)
{
    if (_dmapFile == null || !IsValidTileCoordinate(tileCoords))
        return null;
    
    return _dmapFile.TileSet[tileCoords.X, tileCoords.Y];
}

public List<Vector2I> GetSelectedTiles()
{
    return new List<Vector2I>(_selectedTiles);
}

private bool IsValidTileCoordinate(Vector2I coords)
{
    return coords.X >= 0 && coords.X < MapSize.X &&
           coords.Y >= 0 && coords.Y < MapSize.Y;
}

private void RefreshTileVisual(Vector2I tileCoords)
{
    // Update the visual representation of the tile
    // This would update the TileMapLayer cell data
    if (_terrainLayer != null)
    {
        var tile = _dmapFile.TileSet[tileCoords.X, tileCoords.Y];
        // Update custom data for the tile
        // Implementation depends on how tiles are rendered
    }
}
```

#### 3.3 Add Input Handling
```csharp
public override void _UnhandledInput(InputEvent @event)
{
    if (!Engine.IsEditorHint())
        return;
    
    if (@event is InputEventMouseButton mouseButton)
    {
        if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
        {
            var localPos = ToLocal(mouseButton.GlobalPosition);
            var tileCoords = _terrainLayer?.LocalToMap(localPos) ?? new Vector2I(-1, -1);
            
            if (IsValidTileCoordinate(tileCoords))
            {
                SelectTile(tileCoords);
            }
        }
    }
}

private void SelectTile(Vector2I tileCoords)
{
    _selectedTile = tileCoords;
    _selectedTiles.Clear();
    _selectedTiles.Add(tileCoords);
    
    var tileData = GetTileData(tileCoords);
    if (tileData.HasValue)
    {
        EmitSignal(SignalName.TileSelected, tileCoords, tileData.Value);
    }
    
    // Update selection visual
    UpdateSelectionVisual();
}

private void UpdateSelectionVisual()
{
    if (_selectionLayer != null)
    {
        // Clear previous selection
        _selectionLayer.Clear();
        
        // Draw selection for all selected tiles
        foreach (var tile in _selectedTiles)
        {
            _selectionLayer.SetCell(tile, 0, Vector2I.Zero);
        }
    }
}
```

### Step 4: Update DMapImporterPlugin

#### 4.1 Add Dock Management
```csharp
// In DMapImporterPlugin.cs
private DMapEditorDock? _editorDock;

public override void _EnterTree()
{
    // Existing code...
    
    // Create and add editor dock
    _editorDock = new DMapEditorDock();
    AddControlToDock(DockSlot.LeftUr, _editorDock);
    _logger.LogInformation("DMap Editor Dock added");
    
    // Connect to scene changes to update dock
    var editorSelection = GetEditorInterface().GetSelection();
    editorSelection.SelectionChanged += OnSelectionChanged;
}

public override void _ExitTree()
{
    // Remove editor dock
    if (_editorDock != null)
    {
        RemoveControlFromDocks(_editorDock);
        _editorDock.QueueFree();
        _editorDock = null;
        _logger.LogInformation("DMap Editor Dock removed");
    }
    
    // Existing cleanup code...
}

private void OnSelectionChanged()
{
    // Update dock when selection changes
    var selection = GetEditorInterface().GetSelection();
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
```

## Testing Strategy

### 1. Unit Tests
```csharp
[TestSuite]
[RequireGodotRuntime]
public class DMapEditorDockTests
{
    [TestCase]
    public void TestDockCreation()
    {
        var dock = AutoFree(new DMapEditorDock())!;
        AssertThat(dock).IsNotNull();
        AssertThat(dock.CustomMinimumSize).IsEqual(new Vector2(250, 0));
    }
    
    [TestCase]
    public void TestPropertyControls()
    {
        var dock = AutoFree(new DMapEditorDock())!;
        dock._Ready();
        
        // Verify controls are created
        var heightEditor = dock.GetNode<SpinBox>("HeightEditor");
        AssertThat(heightEditor.MinValue).IsEqual(-100);
        AssertThat(heightEditor.MaxValue).IsEqual(100);
    }
}
```

### 2. Integration Tests
1. **Enable Plugin**: Verify dock appears in editor
2. **Select DMapRenderer**: Confirm dock connects to renderer
3. **Click Tile**: Check coordinates display updates
4. **Modify Properties**: Verify changes apply to tile
5. **Multi-selection**: Test batch property updates

### 3. Manual Testing Checklist
- [ ] Plugin loads without errors
- [ ] Dock appears in correct position (LEFT_UR)
- [ ] Minimum width of 250px is maintained
- [ ] Controls are properly laid out vertically
- [ ] Tile selection updates UI
- [ ] Height spinner accepts values -100 to 100
- [ ] Surface dropdown shows 3 options
- [ ] Walkable checkbox toggles correctly
- [ ] Changes apply immediately to selected tile
- [ ] Multiple tile selection works
- [ ] Apply button updates all selected tiles
- [ ] Dock disconnects properly when switching scenes

## Performance Considerations

### Optimization Points
1. **Batch Updates**: Queue property changes and apply in batches
2. **Caching**: Cache tile data to avoid repeated lookups
3. **Lazy Loading**: Only update visible tiles in viewport
4. **Signal Throttling**: Limit update frequency during rapid changes

### Memory Management
- Properly disconnect signals when changing scenes
- Clear tile data cache when renderer changes
- Use weak references where appropriate
- Implement proper cleanup in _ExitTree()

## Error Handling

### Common Issues and Solutions

1. **Null Reference on Tile Selection**
   - Check: Renderer exists and is initialized
   - Solution: Add null checks and early returns

2. **Signal Connection Errors**
   - Check: Signals properly declared with [Signal] attribute
   - Solution: Verify signal names match exactly

3. **UI Not Updating**
   - Check: _isUpdating flag not stuck
   - Solution: Ensure flag is properly reset in all code paths

4. **Property Changes Not Persisting**
   - Check: DMAP file reference is valid
   - Solution: Verify file is loaded and writable

## Code Style Guidelines

### Naming Conventions
- Private fields: `_camelCase` with underscore prefix
- Public properties: `PascalCase`
- Methods: `PascalCase`
- Local variables: `camelCase`
- Constants: `UPPER_SNAKE_CASE`

### Documentation
- XML comments for public members
- Inline comments for complex logic
- TODO comments for pending features

### Error Handling
- Use try-catch for file operations
- Log errors with appropriate severity
- Provide user-friendly error messages

## Future Enhancements

### Phase 2 Features
1. **Undo/Redo Support**: Integrate with EditorUndoRedoManager
2. **Property Presets**: Save/load common tile configurations
3. **Bulk Operations**: Paint mode for applying properties
4. **Visual Feedback**: Highlight modified tiles
5. **Export**: Save changes back to DMAP file

### Phase 3 Features
1. **Advanced Properties**: Edit portal connections, effects
2. **Tile Templates**: Create reusable tile configurations
3. **Search/Filter**: Find tiles by property values
4. **Statistics**: Show map-wide property distribution
5. **Validation**: Check for invalid tile configurations

## Dependencies and References

### External Documentation
- [Godot EditorPlugin Documentation](https://docs.godotengine.org/en/stable/classes/class_editorplugin.html)
- [Godot Control Node](https://docs.godotengine.org/en/stable/classes/class_control.html)
- [Godot TileMapLayer](https://docs.godotengine.org/en/stable/classes/class_tilemaplayer.html)

### Project Files
- `Core/Dmap/Tile.cs`: Tile data structure
- `Nodes/DMapRenderer.cs`: Main renderer class
- `DMapImporterPlugin.cs`: Plugin entry point
- `PRD.txt`: Lines 400-459 for requirements

### NuGet Packages
- No additional packages required

## Success Criteria

### Minimum Viable Product
- [x] Dock appears in editor
- [x] Shows tile coordinates when selected
- [x] Height editor works (-100 to 100)
- [x] Surface selector has 3 options
- [x] Walkable checkbox toggles
- [x] Changes apply to tiles immediately

### Complete Implementation
- [x] All MVP features
- [x] Multi-tile selection
- [x] Batch apply functionality
- [x] Proper error handling
- [x] Performance optimizations
- [x] Full test coverage

## Timeline

### Estimated Hours: 6-8 hours

1. **Hour 1-2**: Create base dock structure and UI
2. **Hour 3-4**: Implement selection system in renderer
3. **Hour 5**: Connect dock to renderer, add signals
4. **Hour 6**: Implement property update methods
5. **Hour 7**: Testing and debugging
6. **Hour 8**: Documentation and cleanup

## Risk Assessment

### High Risk
- **Signal Connection Issues**: Godot's signal system can be finicky
  - Mitigation: Use Callable constructor, verify with IsConnected()

### Medium Risk
- **Coordinate System Mismatch**: Isometric conversion complexities
  - Mitigation: Use existing CoordinateHelper, add validation

### Low Risk
- **UI Layout Issues**: Controls not displaying correctly
  - Mitigation: Test in multiple editor configurations

## Conclusion

This implementation plan provides a complete roadmap for creating the DMap Editor Dock UI. The design follows Godot best practices while integrating seamlessly with the existing DMapGodot architecture. The modular approach allows for incremental development and testing, ensuring a stable and functional editor tool.