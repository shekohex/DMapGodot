# AGENTS.md - DMapGodot Development Guidelines

## Build/Test Commands
- **Build**: `dotnet build`
- **Run Tests**: `dotnet test --settings .runsettings` (requires GdUnit4Net setup with headless configuration)
- **Single Test**: `dotnet test --filter "TestName" --settings .runsettings` or use GdUnit4Net test runner
- **Test Specific Class**: `dotnet test --filter "ClassName" --settings .runsettings`

## Code Style & Formatting

### C# Conventions
- **Naming**: PascalCase for classes/methods/properties, camelCase for private fields with underscore prefix (`_fieldName`)
- **Indentation**: 4 spaces (no tabs), braces on new lines for classes/methods/properties
- **Line Length**: Maximum 120 characters per line
- **Imports**: System namespaces first, then third-party, then project-specific (separated by blank lines)
- **Comments**: XML documentation for public APIs, inline comments for complex logic only
- **File Organization**: One class per file, filename matches class name

### Godot C# Specific
- **Attributes**: Use `[Tool]` for editor classes, `[Export]` for Godot properties, `partial` keyword for Node-derived classes
- **Signals**: Use PascalCase with descriptive names, define with `[Signal]` attribute
- **Node References**: Use `GetNode<T>()` with typed access, cache references in `_Ready()`
- **Lifecycle Methods**: Override `_Ready()`, `_EnterTree()`, `_ExitTree()` as needed, call base implementations
- **Error Handling**: Use exceptions with descriptive messages, log errors with `Log.Error()` or `GD.PrintErr()`

### Best Practices
- **Resource Management**: Use `using` statements for disposables, `AutoFree()` for test nodes
- **Performance**: Cache expensive operations, use object pooling for frequent allocations
- **Testing**: Use descriptive test names, `AutoFree()` for GdUnit4Net tests with `[RequireGodotRuntime]`
- **Null Safety**: Enable nullable reference types, use null-conditional operators (`?.`, `??`)

### Formatting Commands
- **Format Code**: Use IDE auto-formatting (Ctrl+K, Ctrl+D in Visual Studio)
- **EditorConfig**: Project uses `.editorconfig` for consistent formatting across IDEs

## Project Structure
- **Core Logic**: `Core/` directory contains unchanged parsing classes from Tiled2Dmap
- **Godot Integration**: `Nodes/`, `Importers/`, `Editor/` for Godot-specific code
- **Testing**: GdUnit4Net framework with `.runsettings` configuration for headless execution
- **Test Structure**: Use `[TestSuite]`, `[TestCase]`, `[RequireGodotRuntime]` attributes as needed
- **Node Cleanup**: Use `AutoFree()` function for all Node instances in tests to prevent orphan nodes
- **Test Organization**: Separate test files per class, descriptive test method names
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

## GdUnit4Net Testing Guidelines
- **Always run tests with**: `dotnet test --settings .runsettings` for proper headless execution
- **Node Memory Management**: Use `AutoFree()` for all Node-derived objects in tests to prevent orphan nodes
- **Test Attributes**: Use `[RequireGodotRuntime]` for tests that use Godot classes (Node, Resource, etc.)
- **Test Structure**: Import `using static GdUnit4.Utils;` for `AutoFree()` access
- **Example Test Pattern**:
  ```csharp
  [TestSuite]
  [RequireGodotRuntime]
  public class MyNodeTests
  {
      [TestCase]
      public void TestNodeCreation()
      {
          var node = AutoFree(new MyNode())!;
          AssertThat(node).IsNotNull();
      }
  }
  ```

## Task Master AI Instructions
**Import Task Master's development workflow commands and guidelines, treat as if import is in the main CLAUDE.md file.**
@./.taskmaster/CLAUDE.md
