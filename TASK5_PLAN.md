# **DMapRenderer Implementation Plan - Task 5 (FOCUSED)**

## **📋 Quick Context**
**Task 5 Goal**: Create DMapRenderer Node with three-layer isometric architecture  
**Current Status**: Basic class exists but missing proper implementation  
**Scope Boundaries**: Layer structure & coordinate system ONLY (no textures/animations)

---

## **⚠️ Important Scope Clarifications**

### **✅ IN SCOPE for Task 5**
- Three-layer Node2D hierarchy (Background, Terrain, Object)
- Basic TileMapLayer setup with isometric configuration
- CordConverter integration for coordinate transformation
- Custom data layer definitions (walkable, height, surface)
- Basic tile placement logic (without textures)
- Testing framework for structure validation

### **❌ OUT OF SCOPE (Handled by Other Tasks)**
- **Task 6**: TileSet resource creation with atlas sources
- **Task 7**: DDS texture conversion and loading
- **Task 8**: Plugin registration and editor integration
- **Task 9**: Portal system implementation
- **Task 13**: Scene and cover object rendering details
- **Task 14**: Performance optimization

---

## **🎯 Implementation Phases (Simplified)**

### **Phase 1: Fix Current Implementation Structure**

#### **Current Issues to Fix:**
```csharp
// PROBLEM 1: Using TileMapLayer directly instead of TileMap (deprecated in 4.4)
private TileMapLayer? _terrainLayer; // ✅ CORRECT for Godot 4.4

// PROBLEM 2: Missing background layer
private TileMapLayer? _backgroundLayer; // ADD THIS

// PROBLEM 3: Wrong Z-index management (should use ZIndex property)
```

#### **Action Items:**
1. Add missing `_backgroundLayer` field
2. Fix layer creation to use proper Godot 4.4 API
3. Remove any TileMap references (use TileMapLayer)

---

### **Phase 2: Three-Layer Architecture**

#### **Correct Layer Structure:**
```csharp
public partial class DMapRenderer : Node2D
{
    // Layer references
    private TileMapLayer? _backgroundLayer;  // Z: 0 - Puzzle pieces
    private TileMapLayer? _terrainLayer;     // Z: 1 - Walkable tiles
    private Node2D? _objectLayer;            // Z: 2 - Y-sorted objects
    
    private void CreateLayers()
    {
        // Background Layer (Puzzle pieces)
        _backgroundLayer = new TileMapLayer();
        _backgroundLayer.Name = "BackgroundLayer";
        _backgroundLayer.ZIndex = 0;
        _backgroundLayer.Enabled = true;
        AddChild(_backgroundLayer);
        
        // Terrain Layer (Walkable/Surface data)
        _terrainLayer = new TileMapLayer();
        _terrainLayer.Name = "TerrainLayer";
        _terrainLayer.ZIndex = 1;
        _terrainLayer.Enabled = true;
        AddChild(_terrainLayer);
        
        // Object Layer (3D objects with Y-sorting)
        _objectLayer = new Node2D();
        _objectLayer.Name = "ObjectLayer";
        _objectLayer.ZIndex = 2;
        _objectLayer.YSortEnabled = true;
        AddChild(_objectLayer);
        
        // Set owner for editor visibility
        if (Engine.IsEditorHint())
        {
            var root = GetTree()?.EditedSceneRoot;
            if (root != null)
            {
                _backgroundLayer.Owner = root;
                _terrainLayer.Owner = root;
                _objectLayer.Owner = root;
            }
        }
    }
}
```

---

### **Phase 3: Isometric Configuration**

#### **TileSet Setup (Minimal for now):**
```csharp
private TileSet CreateBasicIsometricTileSet()
{
    var tileSet = new TileSet();
    
    // Isometric configuration per PRD
    tileSet.TileShape = TileSet.TileShapeEnum.Isometric;
    tileSet.TileSize = new Vector2I(64, 32);
    tileSet.TileLayout = TileSet.TileLayoutEnum.Stacked;
    
    // Note: Atlas source will be added in Task 6
    // For now, just create empty tileset structure
    
    return tileSet;
}
```

---

### **Phase 4: Coordinate Conversion**

#### **CordConverter Integration:**
```csharp
public class CoordinateHelper
{
    private CordConverter _converter;
    private Vector2I _mapSize;
    
    public CoordinateHelper(DmapFile dmapFile)
    {
        var dmapSize = new System.Drawing.Size(
            (int)dmapFile.SizeTiles.Width,
            (int)dmapFile.SizeTiles.Height
        );
        
        // Background size will be calculated from puzzle file
        var bgSize = new System.Drawing.Size(256, 256); // Placeholder
        
        _converter = new CordConverter(dmapSize, bgSize);
        _mapSize = new Vector2I(dmapSize.Width, dmapSize.Height);
    }
    
    public Vector2 TileToLocal(int x, int y)
    {
        var worldPos = _converter.Cell2World(
            new System.Drawing.Point(x, y)
        );
        return new Vector2(worldPos.X, worldPos.Y);
    }
    
    public Vector2I LocalToTile(Vector2 localPos)
    {
        var cellPos = _converter.World2Cell(
            new System.Drawing.Point((int)localPos.X, (int)localPos.Y)
        );
        return new Vector2I(cellPos.X, cellPos.Y);
    }
}
```

---

### **Phase 5: Custom Data Layers**

#### **Add to TileSet Creation:**
```csharp
private void AddCustomDataLayers(TileSet tileSet)
{
    // Layer 0: Walkability (inverse of NoAccess)
    tileSet.AddCustomDataLayer();
    tileSet.SetCustomDataLayerName(0, "walkable");
    tileSet.SetCustomDataLayerType(0, Variant.Type.Bool);
    
    // Layer 1: Surface type
    tileSet.AddCustomDataLayer();
    tileSet.SetCustomDataLayerName(1, "surface");
    tileSet.SetCustomDataLayerType(1, Variant.Type.Int);
    
    // Layer 2: Height value
    tileSet.AddCustomDataLayer();
    tileSet.SetCustomDataLayerName(2, "height");
    tileSet.SetCustomDataLayerType(2, Variant.Type.Int);
}
```

---

### **Phase 6: Basic Tile Placement**

#### **Simplified LoadFromDMap:**
```csharp
public void LoadFromDMap(DmapFile dmap)
{
    if (dmap == null)
    {
        GD.PrintErr("Cannot load null DmapFile");
        return;
    }
    
    _dmapFile = dmap;
    
    // Clear existing children
    foreach (Node child in GetChildren())
    {
        child.QueueFree();
    }
    
    // Create layer structure
    CreateLayers();
    
    // Create and assign TileSets
    var tileSet = CreateBasicIsometricTileSet();
    AddCustomDataLayers(tileSet);
    
    _backgroundLayer.TileSet = tileSet;
    _terrainLayer.TileSet = tileSet;
    
    // Basic tile placement (no textures yet)
    PlaceTerrainTiles();
    PlaceObjectMarkers();
}

private void PlaceTerrainTiles()
{
    if (_dmapFile == null || _terrainLayer == null) return;
    
    for (int x = 0; x < _dmapFile.SizeTiles.Width; x++)
    {
        for (int y = 0; y < _dmapFile.SizeTiles.Height; y++)
        {
            var tile = _dmapFile.TileSet[x, y];
            
            // Only place if accessible
            if (tile.Access > 0)
            {
                var coords = new Vector2I(x, y);
                
                // Place empty tile (source_id 0 will be added in Task 6)
                _terrainLayer.SetCell(coords, -1, Vector2I.Zero, 0);
                
                // Note: Custom data will be set when we have actual tiles
            }
        }
    }
}

private void PlaceObjectMarkers()
{
    if (_dmapFile == null || _objectLayer == null) return;
    
    // Place simple markers for portals
    foreach (var portal in _dmapFile.Portals)
    {
        var marker = new Marker2D();
        marker.Name = $"Portal_{portal.Id}";
        marker.Position = new Vector2(
            portal.Position.X * 64,
            portal.Position.Y * 32
        );
        _objectLayer.AddChild(marker);
        
        if (Engine.IsEditorHint())
        {
            marker.Owner = GetTree()?.EditedSceneRoot;
        }
    }
    
    // Place markers for covers
    foreach (var cover in _dmapFile.Covers)
    {
        var marker = new Marker2D();
        marker.Name = $"Cover_{cover.AniName}";
        marker.Position = new Vector2(
            cover.Position.X * 64,
            cover.Position.Y * 32
        );
        _objectLayer.AddChild(marker);
        
        if (Engine.IsEditorHint())
        {
            marker.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}
```

---

### **Phase 7: Testing**

#### **Test Structure Validation:**
```csharp
[TestSuite]
public class DMapRendererStructureTests
{
    [TestCase]
    public void TestThreeLayerCreation()
    {
        var renderer = new DMapRenderer();
        var testDmap = new DmapFile(TestMapPath);
        
        renderer.LoadFromDMap(testDmap);
        
        // Verify three children
        AssertThat(renderer.GetChildCount()).IsEqual(3);
        
        // Verify layer types and names
        var bg = renderer.GetNode("BackgroundLayer");
        var terrain = renderer.GetNode("TerrainLayer");
        var objects = renderer.GetNode("ObjectLayer");
        
        AssertThat(bg).IsNotNull();
        AssertThat(terrain).IsNotNull();
        AssertThat(objects).IsNotNull();
        
        // Verify Z-ordering
        AssertThat(bg.GetIndex()).IsEqual(0);
        AssertThat(terrain.GetIndex()).IsEqual(1);
        AssertThat(objects.GetIndex()).IsEqual(2);
    }
    
    [TestCase]
    public void TestIsometricConfiguration()
    {
        var renderer = new DMapRenderer();
        var testDmap = new DmapFile(TestMapPath);
        
        renderer.LoadFromDMap(testDmap);
        
        var terrainLayer = renderer.GetNode<TileMapLayer>("TerrainLayer");
        var tileSet = terrainLayer.TileSet;
        
        AssertThat(tileSet.TileShape)
            .IsEqual(TileSet.TileShapeEnum.Isometric);
        AssertThat(tileSet.TileSize)
            .IsEqual(new Vector2I(64, 32));
    }
    
    [TestCase]
    public void TestCustomDataLayers()
    {
        var renderer = new DMapRenderer();
        var testDmap = new DmapFile(TestMapPath);
        
        renderer.LoadFromDMap(testDmap);
        
        var terrainLayer = renderer.GetNode<TileMapLayer>("TerrainLayer");
        var tileSet = terrainLayer.TileSet;
        
        // Verify custom data layers exist
        AssertThat(tileSet.GetCustomDataLayersCount()).IsEqual(3);
        AssertThat(tileSet.GetCustomDataLayerName(0)).IsEqual("walkable");
        AssertThat(tileSet.GetCustomDataLayerName(1)).IsEqual("surface");
        AssertThat(tileSet.GetCustomDataLayerName(2)).IsEqual("height");
    }
}
```

---

## **📝 Key Implementation Notes**

### **Critical Points:**
1. **Use TileMapLayer** (not TileMap) - Godot 4.4 requirement
2. **Empty TileSet OK** - Task 6 will add atlas sources
3. **Markers for Objects** - Textures come in Task 7
4. **Basic Structure Focus** - Don't implement full rendering yet

### **Common Pitfalls to Avoid:**
- ❌ Don't try to load textures (Task 7)
- ❌ Don't implement full TileSetAtlasSource (Task 6)
- ❌ Don't optimize performance (Task 14)
- ❌ Don't implement animations (Task 13)

### **Dependencies:**
- ✅ Task 1-4: Complete (packages, core classes, importer)
- ⏳ Task 6: Will add TileSet atlas sources
- ⏳ Task 7: Will add texture loading
- ⏳ Task 13: Will add proper object rendering

---

## **✅ Success Criteria**

### **Must Complete:**
1. Three-layer structure with correct Z-ordering
2. TileMapLayer nodes configured for isometric
3. Custom data layers defined (even if not populated)
4. Basic coordinate conversion working
5. Tests pass for structure validation

### **Acceptable Limitations:**
- No visible tiles (no atlas source yet)
- Markers instead of sprites for objects
- Approximate coordinate positioning
- No texture loading
- No animation support

---

## **🚀 Quick Start Commands**

```bash
# Build the project
dotnet build

# Run structure tests
dotnet test --filter "DMapRendererStructureTests"

# Test with sample DMAP
# Use Game/5017/map/map/Dsquare.DMap for testing
```

---

## **📊 Progress Tracking**

### **Phase Status:**
- [ ] Phase 1: Fix Current Implementation Structure
- [ ] Phase 2: Three-Layer Architecture  
- [ ] Phase 3: Isometric Configuration
- [ ] Phase 4: Coordinate Conversion
- [ ] Phase 5: Custom Data Layers
- [ ] Phase 6: Basic Tile Placement
- [ ] Phase 7: Testing

### **Completion Checklist:**
- [ ] Three TileMapLayer + Node2D structure created
- [ ] Z-indexing working correctly (0, 1, 2)
- [ ] Isometric TileSet configuration applied
- [ ] CordConverter integration functional
- [ ] Custom data layers defined (walkable, surface, height)
- [ ] LoadFromDMap creates proper structure
- [ ] Object markers placed correctly
- [ ] Tests validate all structure requirements
- [ ] Build passes without errors
- [ ] Ready for Task 6 (TileSet atlas sources)

This focused plan keeps Task 5 properly scoped, acknowledges what other tasks will handle, and provides clear implementation guidance without overreach.