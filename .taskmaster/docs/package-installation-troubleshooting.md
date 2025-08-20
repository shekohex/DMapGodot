# Package Installation Troubleshooting Guide

This document covers common issues encountered during NuGet package installation for the DMapGodot project.

## Successfully Installed Packages

- **SharpCompress v0.38.0** - Archive handling (replaces SevenZipSharp)
- **BCnEncoder.Net v2.1.0** - DDS texture conversion
- **GdUnit4Net packages** - Testing framework for Godot C# projects
  - gdUnit4.api v5.0.0
  - gdUnit4.test.adapter v3.0.0  
  - gdUnit4.analyzers v1.0.0

## Common Issues & Solutions

### 1. Build Errors with Tiled2Dmap Directory

**Problem**: Build fails with multiple dependency errors from `Tiled2Dmap/` directory.

**Solution**: The Tiled2Dmap directory contains the original C# implementation with different dependencies. It's excluded from the build using:

```xml
<ItemGroup>
  <!-- Exclude Tiled2Dmap directory from build for now -->
  <Compile Remove="Tiled2Dmap/**/*.cs" />
</ItemGroup>
```

### 2. Package Version Conflicts

**Problem**: NuGet reports version conflicts between packages.

**Solution**: The current package versions are tested and compatible:
- Use .NET 8.0 target framework
- Use Godot.NET.Sdk/4.4.0
- Specific versions listed above are verified to work together

### 3. GdUnit4Net Test Discovery Issues

**Problem**: `dotnet test --list-tests` shows no tests or tests don't run.

**Solution**: GdUnit4Net tests may require the Godot engine to be running. This is normal behavior. The framework is properly installed if:
- Test classes compile with `[TestSuite]` and `[TestCase]` attributes
- `Assertions.AssertThat()` methods are accessible
- `dotnet build` succeeds without errors

### 4. Missing Assembly References

**Problem**: Compiler errors about missing namespaces like `SharpCompress.Archives` or `BCnEncoder.Decoder`.

**Solution**: 
- Ensure `dotnet restore` completed successfully
- Check that packages appear in `.godot/mono/temp/obj/project.assets.json`
- Verify correct namespace usage:
  - SharpCompress: `using SharpCompress.Common;`
  - BCnEncoder.Net: `using BCnEncoder.Decoder;`
  - GdUnit4: `using GdUnit4;`

### 5. Godot.NET.Sdk Version Issues

**Problem**: Compilation fails with Godot SDK version mismatches.

**Solution**: Use Godot.NET.Sdk/4.4.0 as specified in the project file. This version is compatible with Godot 4.4 and the selected NuGet packages.

## Verification Commands

To verify packages are installed correctly:

```bash
# 1. Restore packages
dotnet restore

# 2. Build project (should succeed)
dotnet build

# 3. Check installed packages
cat .godot/mono/temp/obj/project.assets.json | grep -E "(SharpCompress|BCnEncoder|gdUnit)"
```

## Next Steps

After successful package installation:
1. Begin implementing Core classes from Tiled2Dmap
2. Update DmapFile.cs to use SharpCompress instead of SevenZipSharp
3. Implement Godot-specific integration layers
4. Create unit tests using GdUnit4Net framework