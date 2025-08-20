# Task 7: DDS Texture Conversion - Implementation Plan

## Overview
Implement DDS texture conversion for DMAP files using Godot 4.4's native DDS support. This eliminates the need for external libraries like BCnEncoder.Net, resulting in a simpler, more reliable solution.

## Key Findings
- **Godot 4.4 Native Support**: `Image.LoadFromFile()` supports DDS files directly
- **No External Dependencies**: BCnEncoder.Net is unnecessary
- **Simpler Implementation**: Reduced complexity and maintenance overhead
- **Better Performance**: Native Godot implementation is optimized

## Implementation Strategy

### 1. TextureConverter Class
**Location**: `addons/dmap_importer/Importers/TextureConverter.cs`

**Core Features**:
- Static class with texture conversion methods
- Texture caching to prevent redundant conversions
- Comprehensive error handling and logging
- Support for both file paths and byte arrays

**Key Methods**:
```csharp
public static ImageTexture? ConvertDDSToTexture(string ddsPath)
public static ImageTexture? ConvertDDSToTexture(byte[] ddsData)
public static void ClearCache()
public static ImageTexture? GetCachedTexture(string cacheKey)
```

### 2. Implementation Approach

#### Primary Method: Direct File Loading
```csharp
var image = Image.LoadFromFile(ddsPath);
if (image != null && !image.IsEmpty())
{
    var texture = ImageTexture.CreateFromImage(image);
    _textureCache[ddsPath] = texture;
    return texture;
}
```

#### Byte Array Support
```csharp
// Save to temporary file, load with Godot, then cleanup
string tempPath = Path.GetTempFileName() + ".dds";
File.WriteAllBytes(tempPath, ddsData);
var result = ConvertDDSToTexture(tempPath);
File.Delete(tempPath);
return result;
```

#### Texture Caching
- Use `Dictionary<string, ImageTexture>` for caching
- Cache key: file path or hash of byte array
- Prevent redundant conversions for performance

### 3. Error Handling Strategy
- Return `null` for invalid/unsupported files
- Log detailed error messages using Microsoft.Extensions.Logging
- Graceful handling of file I/O exceptions
- Validation of input parameters

### 4. Testing Strategy

**Test File**: `tests/TextureConverterTests.cs`

**Test Coverage**:
- ✅ Valid DDS file conversion
- ✅ Invalid file handling (non-existent, corrupted)
- ✅ Null and empty parameter validation
- ✅ Cache functionality (store, retrieve, clear)
- ✅ Byte array conversion
- ✅ Error logging verification
- ✅ Memory management (texture disposal)

**Test Data**: Use actual DDS files from `Game/5017/data/ItemMinIcon/`

### 5. Integration Points

#### With DMapImporter
```csharp
// Usage in DMapImporter
var texture = TextureConverter.ConvertDDSToTexture(ddsFilePath);
if (texture != null)
{
    // Apply texture to sprite or material
    sprite.Texture = texture;
}
```

#### With Tile System
```csharp
// Tile texture loading
foreach (var tileData in tiles)
{
    if (!string.IsNullOrEmpty(tileData.TexturePath))
    {
        var texture = TextureConverter.ConvertDDSToTexture(tileData.TexturePath);
        tileData.Texture = texture;
    }
}
```

### 6. Project Cleanup

#### Remove BCnEncoder.Net Dependency
```xml
<!-- Remove from DMapGodot.csproj -->
<PackageReference Include="BCnEncoder.Net" Version="2.1.0" />
```

#### Update Using Statements
```csharp
// Remove these imports
using BCnEncoder.Decoder;
using BCnEncoder.ImageSharp;

// Keep these
using Godot;
using Microsoft.Extensions.Logging;
```

### 7. Performance Considerations

#### Caching Strategy
- Cache converted textures to avoid redundant processing
- Use weak references for large textures to prevent memory leaks
- Implement cache size limits if needed

#### Memory Management
- Proper disposal of Image objects
- Clear cache when appropriate (scene changes, memory pressure)
- Use `using` statements for temporary resources

### 8. Verification Steps

#### Build and Test
```bash
# Build project
dotnet build

# Run all tests
dotnet test --settings .runsettings

# Run specific texture tests
dotnet test --filter "TextureConverter" --settings .runsettings
```

#### Manual Testing
1. Load actual DDS files from game data
2. Verify textures display correctly in Godot editor
3. Test with various DDS compression formats (DXT1, DXT3, DXT5)
4. Performance testing with multiple texture loads

#### Integration Testing
1. Test within DMapImporter workflow
2. Verify texture appears on imported map tiles
3. Test memory usage with large texture sets
4. Validate error handling with corrupted files

## Benefits of Native Approach

### Simplified Architecture
- **Reduced Dependencies**: No external texture libraries
- **Cleaner Code**: ~75 lines vs ~150 lines with BCnEncoder.Net
- **Better Maintenance**: Updates come with Godot engine updates

### Performance Improvements
- **Native Optimization**: Godot's built-in DDS loader is optimized
- **Faster Loading**: No intermediate conversion steps
- **Lower Memory**: Direct texture creation without pixel buffer copies

### Compatibility
- **Format Support**: Automatic support for all DDS formats Godot supports
- **Future-Proof**: New DDS features added to Godot are automatically available
- **Platform Support**: Works on all platforms Godot supports

## Implementation Timeline

### Phase 1: Core Implementation (1-2 hours)
- [x] Research Godot DDS support
- [ ] Create TextureConverter.cs
- [ ] Implement basic conversion methods
- [ ] Add error handling and logging

### Phase 2: Testing (1 hour)
- [ ] Create comprehensive test suite
- [ ] Test with actual game DDS files
- [ ] Verify caching functionality
- [ ] Performance benchmarking

### Phase 3: Integration & Cleanup (30 minutes)
- [ ] Remove BCnEncoder.Net dependency
- [ ] Update project references
- [ ] Build verification
- [ ] Documentation updates

## Success Criteria

✅ **Functionality**
- DDS files load correctly as ImageTexture
- Texture caching works properly
- Error handling is robust

✅ **Performance**
- Faster than BCnEncoder.Net approach
- Memory usage is reasonable
- No memory leaks

✅ **Integration**
- Compatible with existing DMapImporter
- Works with game's DDS files
- Proper Godot editor integration

✅ **Quality**
- 100% test coverage
- Clean, maintainable code
- Comprehensive error logging

## Next Steps After Completion
1. Integrate with DMapImporter for tile texture loading
2. Update documentation and examples
3. Consider texture atlas optimization for performance
4. Implement texture streaming for large maps

---

**Note**: This plan leverages Godot 4.4's native DDS support, discovered during research phase, eliminating the complexity of external texture libraries while providing better performance and maintainability.