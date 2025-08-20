# Task 9: Portal System Implementation Plan

## Overview
Implement a comprehensive portal system for the DMap Godot plugin based on Task 9 requirements. This system will create interactive portal nodes for map transitions using Portal.cs data, implementing Area2D inheritance, Export properties, and map transition functionality as specified in PRD Phase 5 Section 8.2 (lines 462-497).

## Research Findings

### Portal Data Structure
- **Portal.cs**: Simple readonly struct with:
  - `TilePosition Position` (uint X, Y properties)
  - `uint Id`
- **TilePosition.cs**: Simple readonly struct with uint X, Y properties

### Coordinate Conversion
- **CordConverter.cs**: Contains `Cell2World(Point cell)` method (not `TileToWorld()`)
- Conversion formula: Isometric projection from tile coordinates to world pixels
- Origin calculation: `Point(64 * (dmapSize.Width / 2), 32 / 2)`
- World position: `world.X = 32 * (cell.X - cell.Y) + origin.X`
- World position: `world.Y = 16 * (cell.X + cell.Y) + origin.Y`

### Current Portal Handling
- **DMapRenderer.cs**: Currently creates simple `Marker2D` nodes for portals
- Portal positioning: Simple multiplication `portal.Position.X * 64, portal.Position.Y * 32`
- Need to replace with proper `DMapPortal` instances using `CordConverter`

### Godot Area2D System
- **Signals**: `body_entered` for detecting player collision
- **Monitoring**: Must be enabled for detection
- **Groups**: Player detection via `IsInGroup("player")`
- **CollisionShape2D**: CircleShape2D with configurable radius

## Detailed Implementation Plan

### Step 1: Create DMapPortal Class Structure
**File**: `addons/dmap_importer/Nodes/DMapPortal.cs`

#### Class Definition
```csharp
using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;

namespace DMapImporter.Nodes
{
    [Tool]
    public partial class DMapPortal : Area2D
    {
        // Export properties for editor
        [Export] public uint PortalId { get; set; }
        [Export] public string DestinationMap { get; set; } = "";
        [Export] public Vector2I DestinationPos { get; set; }
        
        // Internal references
        private Sprite2D _sprite;
        private CollisionShape2D _collision;
        
        // Constructor options
        public DMapPortal() { }
        public DMapPortal(Portal portal, CordConverter converter) { }
    }
}
```

#### Key Features
- **Tool attribute**: Enable in-editor functionality
- **Export properties**: Editable in Godot inspector
- **Constructor overloads**: Default and Portal data initialization
- **Internal references**: Cached child node references

### Step 2: Visual Components Implementation

#### Sprite2D Configuration
- **Texture path**: `res://addons/dmap_importer/icons/portal.png`
- **Centering**: Sprite centered on portal position
- **Scaling**: Appropriate size for game scale
- **Z-index**: Proper layering

#### CollisionShape2D Configuration
- **Shape**: CircleShape2D with 32px radius
- **Positioning**: Centered on portal
- **Layer/Mask**: Proper collision configuration

#### Implementation Details
```csharp
private void SetupVisualComponents()
{
    // Create and configure sprite
    _sprite = new Sprite2D();
    _sprite.Texture = GD.Load<Texture2D>("res://addons/dmap_importer/icons/portal.png");
    AddChild(_sprite);
    
    // Create and configure collision
    _collision = new CollisionShape2D();
    var shape = new CircleShape2D();
    shape.Radius = 32;
    _collision.Shape = shape;
    AddChild(_collision);
}
```

### Step 3: Interaction Logic Implementation

#### Signal Connection
- **BodyEntered**: Connect in `_Ready()` method
- **Player Detection**: Check for "player" group membership
- **Map Transition**: Use `GetTree().ChangeSceneToFile()`

#### Scene Path Format
- **Template**: `res://maps/{DestinationMap}.tscn`
- **Validation**: Check if scene file exists before transition
- **Error Handling**: Log errors for missing destinations

#### Implementation Details
```csharp
public override void _Ready()
{
    SetupVisualComponents();
    BodyEntered += OnBodyEntered;
    
    if (Engine.IsEditorHint())
    {
        SetOwnerRecursive(GetTree()?.EditedSceneRoot);
    }
}

private void OnBodyEntered(Node2D body)
{
    if (!body.IsInGroup("player")) return;
    
    if (string.IsNullOrEmpty(DestinationMap))
    {
        GD.PrintErr($"Portal {PortalId}: No destination map specified");
        return;
    }
    
    string scenePath = $"res://maps/{DestinationMap}.tscn";
    
    if (!FileAccess.FileExists(scenePath))
    {
        GD.PrintErr($"Portal {PortalId}: Destination scene not found: {scenePath}");
        return;
    }
    
    GetTree().ChangeSceneToFile(scenePath);
}
```

### Step 4: Portal Positioning System

#### Coordinate Conversion
- **Input**: Portal.Position (TilePosition with uint X, Y)
- **Converter**: CordConverter.Cell2World() method
- **Output**: Godot world coordinates (Vector2)

#### Implementation Strategy
```csharp
public void SetPortalPosition(Portal portal, CordConverter converter)
{
    // Convert TilePosition to Point for CordConverter
    var cellPoint = new System.Drawing.Point((int)portal.Position.X, (int)portal.Position.Y);
    
    // Use CordConverter for proper isometric positioning
    var worldPoint = converter.Cell2World(cellPoint);
    
    // Set Area2D position
    Position = new Vector2(worldPoint.X, worldPoint.Y);
}
```

#### Integration Points
- **DMapRenderer**: Update PlaceObjectMarkers() method
- **Portal Creation**: Replace Marker2D with DMapPortal
- **Batch Processing**: Handle multiple portals efficiently

### Step 5: Portal Data Integration

#### Portal Constructor
```csharp
public DMapPortal(Portal portal, CordConverter converter)
{
    PortalId = portal.Id;
    Name = $"Portal_{portal.Id}";
    
    // Set position using proper coordinate conversion
    SetPortalPosition(portal, converter);
    
    // DestinationMap and DestinationPos would come from game data
    // For now, these are set via export properties or external configuration
}
```

#### Data Source Considerations
- **Portal.cs**: Contains only Id and Position
- **Destination Data**: May need additional data source or configuration
- **Map Metadata**: Could be embedded in DMAP files or external config

### Step 6: Portal Icon Resource Creation

#### Icon Requirements
- **Path**: `res://addons/dmap_importer/icons/portal.png`
- **Size**: 64x64 pixels (or appropriate for game scale)
- **Style**: Consistent with game art style
- **Format**: PNG with transparency support

#### Directory Structure
```
addons/dmap_importer/
├── icons/
│   ├── portal.png
│   └── portal.png.import
└── ...
```

#### Icon Design Guidelines
- **Visibility**: Clear and recognizable
- **Scale**: Readable at various zoom levels
- **Color**: Contrasts with typical map backgrounds
- **Animation**: Consider animated portal effect (future enhancement)

### Step 7: DMapRenderer Integration

#### Update PlaceObjectMarkers Method
```csharp
private void PlaceObjectMarkers()
{
    if (_dmapFile == null || _objectLayer == null) return;

    // Replace Marker2D creation with DMapPortal
    foreach (var portal in _dmapFile.Portals)
    {
        var portalNode = new DMapPortal(portal, _cordConverter);
        _objectLayer.AddChild(portalNode);

        if (Engine.IsEditorHint())
        {
            portalNode.Owner = GetTree()?.EditedSceneRoot;
        }
    }

    // ... existing cover handling
}
```

#### Requirements
- **CordConverter Access**: Ensure _cordConverter is available
- **Error Handling**: Handle portal creation failures gracefully
- **Performance**: Efficient batch creation for many portals

### Step 8: Comprehensive Testing Strategy

#### Test File: `tests/DMapPortalTests.cs`

##### Unit Tests
```csharp
[TestSuite]
[RequireGodotRuntime]
public class DMapPortalTests
{
    [TestCase]
    public void TestPortalCreation()
    {
        var portal = AutoFree(new DMapPortal())!;
        AssertThat(portal).IsNotNull();
        AssertThat(portal.PortalId).IsEqual(0u);
    }
    
    [TestCase]
    public void TestPortalWithData()
    {
        var tilePos = new TilePosition(5, 10);
        var portalData = new Portal(tilePos, 123);
        var converter = new CordConverter(new Size(100, 100), new Size(800, 600));
        
        var portal = AutoFree(new DMapPortal(portalData, converter))!;
        AssertThat(portal.PortalId).IsEqual(123u);
    }
}
```

##### Integration Tests
- **DMapRenderer Integration**: Verify portal creation in renderer
- **Coordinate Accuracy**: Test positioning matches expected coordinates
- **Signal Functionality**: Test body detection and signal emission
- **Scene Transitions**: Mock scene changes for testing

##### Visual Tests
- **Icon Display**: Verify portal icon loads and displays correctly
- **Collision Shape**: Confirm collision area is properly sized
- **Inspector Properties**: Test export properties are editable

#### Test Data Requirements
- **Test DMAP Files**: Use existing Game/5017/map/ resources
- **Mock Player**: Create test player node with "player" group
- **Test Scenes**: Create simple destination scenes for transition testing

### Step 9: Error Handling and Edge Cases

#### Portal Data Validation
- **Invalid IDs**: Handle duplicate or invalid portal IDs
- **Position Bounds**: Validate portal positions within map bounds
- **Missing Destinations**: Handle missing destination maps gracefully

#### Runtime Error Handling
- **Scene Loading Failures**: Provide user feedback for failed transitions
- **Collision Issues**: Handle collision detection edge cases
- **Memory Management**: Proper cleanup of portal resources

#### Editor Integration
- **Tool Mode**: Ensure proper behavior in editor
- **Property Validation**: Validate export properties in editor
- **Visual Feedback**: Provide clear visual indicators in editor

### Step 10: Documentation and Integration

#### Code Documentation
- **XML Comments**: Document all public methods and properties
- **Usage Examples**: Provide clear usage examples
- **Integration Guide**: Document integration with DMapRenderer

#### Plugin Integration
- **Registration**: Ensure portal system is properly registered
- **Compatibility**: Maintain compatibility with existing systems
- **Performance**: Document performance characteristics and limitations

## Implementation Order

1. **Create basic DMapPortal class structure** - Core class definition
2. **Implement visual components setup** - Sprite2D and CollisionShape2D
3. **Add interaction logic** - Signal handling and player detection
4. **Implement positioning system** - CordConverter integration
5. **Create portal icon resource** - Asset creation and integration
6. **Write unit tests** - Basic functionality testing
7. **Update DMapRenderer integration** - Replace Marker2D with DMapPortal
8. **Add comprehensive testing** - Integration and edge case testing
9. **Error handling and validation** - Robustness improvements
10. **Documentation and cleanup** - Final polish and documentation

## Success Criteria

### Functional Requirements
- ✅ Portal nodes extend Area2D with Export properties
- ✅ Visual components (Sprite2D, CollisionShape2D) properly configured
- ✅ Player detection via "player" group membership
- ✅ Map transitions using GetTree().ChangeSceneToFile()
- ✅ Proper positioning using CordConverter.Cell2World()
- ✅ Integration with DMapRenderer system

### Technical Requirements
- ✅ Comprehensive unit and integration tests
- ✅ Proper error handling and validation
- ✅ Editor integration with export properties
- ✅ Performance suitable for multiple portals
- ✅ Memory management and resource cleanup

### Quality Requirements
- ✅ Code follows project style guidelines
- ✅ Comprehensive documentation and comments
- ✅ No regression in existing functionality
- ✅ Passes all existing and new tests

## Potential Challenges and Solutions

### Challenge 1: Destination Map Data
**Problem**: Portal.cs only contains Id and Position, no destination information
**Solution**: Use export properties for manual configuration, consider external data source

### Challenge 2: Coordinate System Differences
**Problem**: Converting between DMAP tile coordinates and Godot world coordinates
**Solution**: Leverage existing CordConverter.Cell2World() method with proper Point conversion

### Challenge 3: Performance with Many Portals
**Problem**: Large maps may have numerous portals affecting performance
**Solution**: Implement efficient batch creation, consider culling for off-screen portals

### Challenge 4: Scene Path Management
**Problem**: Managing scene paths and ensuring destination scenes exist
**Solution**: Implement validation, provide clear error messages, consider scene registry

This comprehensive plan provides a roadmap for implementing a robust portal system that integrates seamlessly with the existing DMap Godot plugin architecture while providing the functionality specified in the PRD requirements.