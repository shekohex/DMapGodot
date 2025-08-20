# AGENTS.md - DMapGodot Development Guidelines

## Build/Test Commands
- **Build**: `dotnet build`
- **Run Tests**: `dotnet test` (requires GdUnit4Net setup)
- **Single Test**: `dotnet test --filter "TestName"` or use GdUnit4Net test runner

## Code Style
- **Naming**: PascalCase for classes/methods/properties, camelCase for private fields
- **Indentation**: 4 spaces, braces on new lines for classes/methods
- **Imports**: System namespaces first, then third-party, then project-specific
- **Error Handling**: Use exceptions with descriptive messages, log errors with `Log.Error()`
- **Attributes**: Use `[Tool]` for editor classes, `[Export]` for Godot properties

## Project Structure
- **Core Logic**: `Core/` directory contains unchanged parsing classes from Tiled2Dmap
- **Godot Integration**: `Nodes/`, `Importers/`, `Editor/` for Godot-specific code
- **Testing**: GdUnit4Net framework, separate test project structure
- **Archives**: SharpCompress for .7z/.dmap files (replaces SevenZipSharp)

## Key Technologies
- **Godot 4.4** with C# (.NET 8.0) - Main game engine
- **GdUnit4Net** - Testing framework for Godot C# projects
- **SharpCompress** - Archive handling (replaces SevenZipSharp from original)
- **BCnEncoder.Net** - DDS texture conversion
- **Original C# DMAP Code** - Reference implementation in `Tiled2Dmap/` directory

## Resource Locations
- **Original C# Implementation**: `Tiled2Dmap/` directory (use as reference, ~70% reusable)
- **Game Client Assets**: `Game/5017/` directory contains DMAP files and resources
- **Test Maps**: `Game/5017/map/` for testing import functionality

## Architecture Notes
- Preserve existing DMAP parsing logic from Tiled2Dmap, focus on Godot integration
- Copy Core classes unchanged: `Tile.cs`, `Portal.cs`, `Cover.cs`, `CordConverter.cs`, Extensions
- Update only archive handling (SevenZipSharp → SharpCompress) in `DmapFile.cs`