# **Task 6 Implementation Plan: Isometric TileSet Creation**

## **📋 Task Overview**
**Goal**: Implement separate CreateTerrainTileSet() and CreatePuzzleTileSet() methods with proper isometric configuration and custom data layers  
**Current Status**: Basic implementation exists but needs refactoring to match PRD specifications  
**Dependencies**: Task 5 (DMapRenderer structure) - COMPLETED

---

## **⚡ Quick Summary of Required Changes**

### **Current Issues Identified:**
1. **Single TileSet method**: Only `CreateBasicIsometricTileSet()` exists; both layers share same TileSet
2. **Wrong custom data layer names**: Uses "walkable" instead of "no_access" as specified in PRD
3. **Incorrect layer assignment**: Both background and terrain layers have custom data layers
4. **Test expectations mismatch**: Tests expect "walkable" but PRD specifies "no_access"

### **PRD Requirements (lines 322-343):**
- `CreateTerrainTileSet()` method with custom data layers: "no_access" (bool), "surface" (int), "height" (int)
- `CreatePuzzleTileSet()` method for background layer WITHOUT custom data layers
- Property mapping: `NoAccess (ushort) → no_access (bool)`, `Surface (ushort) → surface (int)`, `Height (short) → height (int)`

---

## **🎯 Implementation Steps**

### **Step 1: Create CreateTerrainTileSet() Method**
**File**: `addons/dmap_importer/Nodes/DMapRenderer.cs`  
**Action**: Replace `CreateBasicIsometricTileSet()` method (lines 137-150) with:

```csharp
private TileSet CreateTerrainTileSet()
{
    var tileSet = new TileSet();
    tileSet.TileShape = TileSet.TileShapeEnum.Isometric;
    tileSet.TileSize = new Vector2I(64, 32);
    tileSet.TileLayout = TileSet.TileLayoutEnum.Stacked;

    // Add custom data layers (from Tile.cs structure)
    tileSet.AddCustomDataLayer();
    tileSet.SetCustomDataLayerName(0, "no_access");
    tileSet.SetCustomDataLayerType(0, Variant.Type.Bool);

    tileSet.AddCustomDataLayer();
    tileSet.SetCustomDataLayerName(1, "surface");
    tileSet.SetCustomDataLayerType(1, Variant.Type.Int);

    tileSet.AddCustomDataLayer();
    tileSet.SetCustomDataLayerName(2, "height");
    tileSet.SetCustomDataLayerType(2, Variant.Type.Int);

    return tileSet;
}
```

### **Step 2: Create CreatePuzzleTileSet() Method**
**File**: `addons/dmap_importer/Nodes/DMapRenderer.cs`  
**Action**: Add new method after CreateTerrainTileSet():

```csharp
private TileSet CreatePuzzleTileSet()
{
    var tileSet = new TileSet();
    tileSet.TileShape = TileSet.TileShapeEnum.Isometric;
    tileSet.TileSize = new Vector2I(64, 32);
    tileSet.TileLayout = TileSet.TileLayoutEnum.Stacked;

    // No custom data layers for background/puzzle layer

    return tileSet;
}
```

### **Step 3: Remove AddCustomDataLayers() Method**
**File**: `addons/dmap_importer/Nodes/DMapRenderer.cs`  
**Action**: Delete `AddCustomDataLayers()` method (lines 152-168) - functionality moved to CreateTerrainTileSet()

### **Step 4: Update CreateLayers() Method**
**File**: `addons/dmap_importer/Nodes/DMapRenderer.cs`  
**Action**: Replace TileSet assignment (lines 129-135) with:

```csharp
// Create and assign separate TileSets
var puzzleTileSet = CreatePuzzleTileSet();
var terrainTileSet = CreateTerrainTileSet();

_backgroundLayer.TileSet = puzzleTileSet;
_terrainLayer.TileSet = terrainTileSet;
```

### **Step 5: Update Test Expectations**
**File**: `tests/DMapRendererStructureTests.cs`  
**Action**: Change line 75 from:
```csharp
AssertThat(tileSet.GetCustomDataLayerName(0)).IsEqual("walkable");
```
to:
```csharp
AssertThat(tileSet.GetCustomDataLayerName(0)).IsEqual("no_access");
```

---

## **📝 Detailed File Changes**

### **DMapRenderer.cs Changes:**

#### **Lines to Replace:**
- **Lines 130-134**: Update TileSet creation and assignment
- **Lines 137-150**: Replace `CreateBasicIsometricTileSet()` with `CreateTerrainTileSet()`
- **Lines 152-168**: Remove `AddCustomDataLayers()` method
- **Add after CreateTerrainTileSet()**: New `CreatePuzzleTileSet()` method

#### **New Method Structure:**
```csharp
private void CreateLayers()
{
    // [Existing layer creation code...]
    
    // Create and assign separate TileSets
    var puzzleTileSet = CreatePuzzleTileSet();
    var terrainTileSet = CreateTerrainTileSet();

    _backgroundLayer.TileSet = puzzleTileSet;
    _terrainLayer.TileSet = terrainTileSet;
}

private TileSet CreateTerrainTileSet()
{
    // [Implementation from Step 1]
}

private TileSet CreatePuzzleTileSet()
{
    // [Implementation from Step 2]
}
```

### **DMapRendererStructureTests.cs Changes:**

#### **Line 75**: 
```csharp
// OLD:
AssertThat(tileSet.GetCustomDataLayerName(0)).IsEqual("walkable");

// NEW:
AssertThat(tileSet.GetCustomDataLayerName(0)).IsEqual("no_access");
```

---

## **🔧 Property Mapping Specifications**

### **From Tile.cs to Custom Data Layers:**
- `NoAccess` (ushort) → `"no_access"` (bool): `tile.NoAccess == 1` converts to `true`
- `Surface` (ushort) → `"surface"` (int): Direct cast `(int)tile.Surface`
- `Height` (short) → `"height"` (int): Direct cast `(int)tile.Height`

### **TileSet Configuration:**
- **Shape**: `TileSet.TileShapeEnum.Isometric`
- **Size**: `Vector2I(64, 32)`
- **Layout**: `TileSet.TileLayoutEnum.Stacked`

---

## **✅ Verification Steps**

### **Build Test:**
```bash
dotnet build
```

### **Run Specific Tests:**
```bash
dotnet test --filter "DMapRendererStructureTests" --settings .runsettings
```

### **Expected Test Results:**
1. **TestThreeLayerCreation**: ✅ PASS (no changes needed)
2. **TestIsometricConfiguration**: ✅ PASS (no changes needed)
3. **TestCustomDataLayers**: ✅ PASS (after "walkable" → "no_access" fix)
4. **TestLayerConfiguration**: ✅ PASS (no changes needed)
5. **TestNullDMapHandling**: ✅ PASS (no changes needed)

---

## **📊 Success Criteria**

### **Must Complete:**
- [x] Analyze current implementation vs PRD requirements
- [ ] Create separate `CreateTerrainTileSet()` method with custom data layers
- [ ] Create separate `CreatePuzzleTileSet()` method without custom data layers
- [ ] Update layer assignment to use correct TileSet for each layer
- [ ] Fix custom data layer names: "walkable" → "no_access"
- [ ] Update tests to match new expectations
- [ ] All tests pass without errors
- [ ] Project builds successfully

### **Validation Points:**
1. Background layer uses TileSet without custom data layers
2. Terrain layer uses TileSet with 3 custom data layers: "no_access", "surface", "height"
3. Both TileSets have isometric configuration (64x32 tiles)
4. Tests verify correct custom data layer names and count
5. No compilation errors or warnings

---

## **🚨 Common Pitfalls to Avoid**

### **Don't Do:**
- ❌ Don't add texture sources or atlas data (that's Task 7)
- ❌ Don't implement tile population with custom data (that's for later tasks)
- ❌ Don't change the layer structure or coordinate system
- ❌ Don't modify TileMap vs TileMapLayer usage (already correct for Godot 4.4)

### **Focus On:**
- ✅ Separate TileSet creation methods only
- ✅ Correct custom data layer names and types
- ✅ Proper assignment of TileSets to layers
- ✅ Test compatibility

---

## **⏭️ Next Steps After Completion**

This task prepares the foundation for:
- **Task 7**: Texture loading and TileSetAtlasSource creation
- **Task 8**: DDS conversion integration
- **Task 9**: Tile population with custom data

The separate TileSet structure established here will support different texture and data requirements for background vs terrain layers in future tasks.