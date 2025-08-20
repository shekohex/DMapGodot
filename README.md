# DMapGodot - DMAP to Godot 4.4 Integration

A comprehensive Godot 4.4 plugin that enables direct loading, rendering, and editing of Conquer Online DMAP files, preserving all unique features while leveraging Godot's powerful editor and C# support.

## Overview

DMapGodot bridges the gap between Conquer Online's DMAP format and modern game development with Godot 4.4. This plugin provides native editor integration for importing, rendering, and editing DMAP files without requiring conversion through intermediate formats like Tiled.

### Key Features

- **Direct DMAP Import**: Native support for .dmap, .7z, and .zmap files
- **Isometric Rendering**: High-performance rendering using Godot's TileMap system
- **Live Editing**: Real-time tile property editing within Godot editor
- **Complete Feature Preservation**: Portals, covers, scenes, and all DMAP features
- **Bidirectional Save/Load**: Export back to DMAP format with full fidelity

### Technical Approach

- **Maximum Code Reuse**: ~70% of existing C# parsing code used directly
- **Native Integration**: Seamless Godot editor workflow
- **Type Safety**: Full C# 8.0+ features with .NET 8
- **Performance**: Optimized for real-time editing (60+ FPS target)

## Architecture

### Technology Stack

- **Godot 4.4** with C# support
- **.NET 8.0** runtime
- **GdUnit4Net** for comprehensive testing
- **SharpCompress** for archive handling (replaces SevenZipSharp)
- **BCnEncoder.Net** for DDS texture conversion
- **Original C# DMAP Code** from Tiled2Dmap as reference

### Project Structure

```
dmapgodot/
├── project.godot
├── DMapGodot.csproj
├── addons/
│   └── dmap_importer/           # Main plugin
│       ├── Core/                # Direct copy from Tiled2Dmap
│       ├── Importers/           # EditorImportPlugin implementation
│       ├── Nodes/               # DMapRenderer and custom nodes
│       └── Editor/              # Editor integration
├── tests/                       # GdUnit4Net test suite
├── Game/5017/                   # Game client assets for testing
└── Tiled2Dmap/                  # Original C# reference implementation
```

## Getting Started

### Prerequisites

- Godot 4.4+ with .NET support
- .NET 8.0 SDK
- NuGet package manager

### Installation

1. Clone the repository
2. Open in Godot 4.4+
3. Build the project (`dotnet build`)
4. Enable the DMAP Importer plugin in Project Settings

### Quick Start

1. Import a DMAP file by dragging it into the FileSystem dock
2. The importer automatically creates a scene with DMapRenderer node
3. Use the DMAP Editor dock to modify tile properties
4. Save changes back to DMAP format using the export tools

## Testing

Run the test suite using GdUnit4Net:

```bash
# Run all tests
dotnet test

# Run specific test
dotnet test --filter "TestName"
```

## Performance Targets

- **Map Loading**: < 2 seconds for 1000x1000 tiles
- **Rendering**: 60+ FPS with full map visible
- **Memory Usage**: < 500MB for large maps

## Contributing

This project uses Task Master for development workflow. See the task dashboard below for current development status and priorities.

## License

[Add license information]


<!-- TASKMASTER_EXPORT_START -->
> 🎯 **Taskmaster Export** - 2025-08-20 19:46:11 UTC
> 📋 Export: with subtasks • Status filter: none
> 🔗 Powered by [Task Master](https://task-master.dev?utm_source=github-readme&utm_medium=readme-export&utm_campaign=dmapgodot&utm_content=task-export-link)

| Project Dashboard |  |
| :-                |:-|
| Task Progress     | ███████████████████░ 93% |
| Done | 14 |
| In Progress | 0 |
| Pending | 1 |
| Deferred | 0 |
| Cancelled | 0 |
|-|-|
| Subtask Progress | ██████████████████░░ 91% |
| Completed | 77 |
| In Progress | 0 |
| Pending | 8 |


| ID | Title | Status | Priority | Dependencies | Complexity |
| :- | :-    | :-     | :-       | :-           | :-         |
| 1 | Install Required NuGet Packages | ✓&nbsp;done | high | None | N/A |
| 1.1 | Update DMapGodot.csproj with complete package references | ✓&nbsp;done | -            | None | N/A |
| 1.2 | Run dotnet restore to install packages | ✓&nbsp;done | -            | None | N/A |
| 1.3 | Verify SharpCompress installation | ✓&nbsp;done | -            | None | N/A |
| 1.4 | Verify BCnEncoder.Net installation | ✓&nbsp;done | -            | None | N/A |
| 1.5 | Verify GdUnit4Net testing framework | ✓&nbsp;done | -            | None | N/A |
| 1.6 | Build project to verify compilation | ✓&nbsp;done | -            | None | N/A |
| 1.7 | Document common installation issues | ✓&nbsp;done | -            | None | N/A |
| 2 | Copy Core DMAP Parsing Classes | ✓&nbsp;done | high | 1 | N/A |
| 2.1 | Create Core directory structure | ✓&nbsp;done | -            | None | N/A |
| 2.2 | Copy core DMAP parsing classes | ✓&nbsp;done | -            | None | N/A |
| 2.3 | Copy Extensions classes | ✓&nbsp;done | -            | None | N/A |
| 2.4 | Copy Utility classes | ✓&nbsp;done | -            | None | N/A |
| 2.5 | Copy additional scene classes | ✓&nbsp;done | -            | None | N/A |
| 2.6 | Update namespaces | ✓&nbsp;done | -            | None | N/A |
| 2.7 | Verify compilation | ✓&nbsp;done | -            | None | N/A |
| 2.8 | Create basic unit tests | ✓&nbsp;done | -            | None | N/A |
| 3 | Adapt DmapFile Class for SharpCompress | ✓&nbsp;done | high | 2 | N/A |
| 3.1 | Copy DmapFile.cs to Core/Dmap Directory | ✓&nbsp;done | -            | None | N/A |
| 3.2 | Update Using Statements for SharpCompress | ✓&nbsp;done | -            | None | N/A |
| 3.3 | Replace Archive Extraction Logic | ✓&nbsp;done | -            | None | N/A |
| 3.4 | Add Error Handling for Archive Operations | ✓&nbsp;done | -            | None | N/A |
| 3.5 | Verify Existing API Compatibility | ✓&nbsp;done | -            | None | N/A |
| 3.6 | Test with Game Client DMAP Files | ✓&nbsp;done | -            | None | N/A |
| 3.7 | Validate Data Integrity | ✓&nbsp;done | -            | None | N/A |
| 3.8 | Update Project Dependencies | ✓&nbsp;done | -            | None | N/A |
| 4 | Implement DMapImporter EditorImportPlugin | ✓&nbsp;done | high | 3 | N/A |
| 4.1 | Implement core EditorImportPlugin methods | ✓&nbsp;done | -            | None | N/A |
| 4.2 | Implement import options and settings | ✓&nbsp;done | -            | None | N/A |
| 4.3 | Implement core _Import method logic | ✓&nbsp;done | -            | None | N/A |
| 4.4 | Implement PackedScene creation and configuration | ✓&nbsp;done | -            | None | N/A |
| 4.5 | Implement comprehensive error handling | ✓&nbsp;done | -            | None | N/A |
| 4.6 | Test editor integration and file format support | ✓&nbsp;done | -            | None | N/A |
| 5 | Create DMapRenderer Node | ✓&nbsp;done | high | 4 | N/A |
| 5.1 | Create DMapRenderer class structure | ✓&nbsp;done | -            | None | N/A |
| 5.2 | Initialize three-layer architecture | ✓&nbsp;done | -            | None | N/A |
| 5.3 | Configure isometric TileMap settings | ✓&nbsp;done | -            | None | N/A |
| 5.4 | Implement coordinate conversion system | ✓&nbsp;done | -            | None | N/A |
| 5.5 | Add custom data layers for tile properties | ✓&nbsp;done | -            | None | N/A |
| 5.6 | Implement LoadFromDMap method | ✓&nbsp;done | -            | None | N/A |
| 5.7 | Test with Game/5017/map/ data | ✓&nbsp;done | -            | None | N/A |
| 6 | Implement Isometric TileSet Creation | ✓&nbsp;done | medium | 5 | N/A |
| 6.1 | Create DMapRenderer Node Class Structure | ✓&nbsp;done | -            | None | N/A |
| 6.2 | Implement CreateTerrainTileSet Method with Isometric Configuration | ✓&nbsp;done | -            | 1 | N/A |
| 6.3 | Add Custom Data Layers for Tile Properties | ✓&nbsp;done | -            | 2 | N/A |
| 6.4 | Implement CreatePuzzleTileSet Method for Background Layer | ✓&nbsp;done | -            | 2 | N/A |
| 6.5 | Integrate TileSet Creation with Layer System and Texture Sources | ✓&nbsp;done | -            | 3, 4 | N/A |
| 7 | Implement DDS Texture Conversion | ✓&nbsp;done | medium | 6 | N/A |
| 8 | Update Plugin Entry Point | ✓&nbsp;done | medium | 4, 5 | N/A |
| 8.1 | Create plugin.cfg configuration file | ✓&nbsp;done | -            | None | N/A |
| 8.2 | Implement DMapImporterPlugin class structure | ✓&nbsp;done | -            | None | N/A |
| 8.3 | Implement _EnterTree() method with component registration | ✓&nbsp;done | -            | None | N/A |
| 8.4 | Implement _ExitTree() method with cleanup procedures | ✓&nbsp;done | -            | None | N/A |
| 8.5 | Add plugin icon and resource references | ✓&nbsp;done | -            | None | N/A |
| 9 | Implement Portal System | ✓&nbsp;done | medium | 5 | N/A |
| 9.1 | Create DMapPortal class structure | ✓&nbsp;done | -            | None | N/A |
| 9.2 | Implement visual components | ✓&nbsp;done | -            | None | N/A |
| 9.3 | Implement interaction logic | ✓&nbsp;done | -            | None | N/A |
| 9.4 | Implement portal positioning system | ✓&nbsp;done | -            | None | N/A |
| 9.5 | Add Portal.cs integration | ✓&nbsp;done | -            | None | N/A |
| 9.6 | Create portal icon resource | ✓&nbsp;done | -            | None | N/A |
| 10 | Create Editor Dock UI | ✓&nbsp;done | low | 8 | N/A |
| 11 | Setup GdUnit4Net Testing Framework | ✓&nbsp;done | medium | 1 | N/A |
| 11.1 | Install GdUnit4Net NuGet packages | ✓&nbsp;done | -            | None | N/A |
| 11.2 | Create .runsettings configuration file | ✓&nbsp;done | -            | None | N/A |
| 11.3 | Setup test project structure | ✓&nbsp;done | -            | None | N/A |
| 11.4 | Configure GODOT_BIN environment variable | ✓&nbsp;done | -            | None | N/A |
| 11.5 | Implement sample logic tests | ✓&nbsp;done | -            | None | N/A |
| 11.6 | Implement Godot-dependent tests | ✓&nbsp;done | -            | None | N/A |
| 11.7 | Setup data-driven coordinate tests | ✓&nbsp;done | -            | None | N/A |
| 11.8 | Create integration test framework | ✓&nbsp;done | -            | None | N/A |
| 11.9 | Validate test discovery and execution | ✓&nbsp;done | -            | None | N/A |
| 12 | Implement Save/Export Functionality | ✓&nbsp;done | low | 3, 5 | N/A |
| 12.1 | Implement Core Save/Export Methods | ✓&nbsp;done | -            | None | N/A |
| 12.2 | Implement Binary DMAP Writing | ✓&nbsp;done | -            | None | N/A |
| 12.3 | Implement SharpCompress Archive Creation | ✓&nbsp;done | -            | None | N/A |
| 12.4 | Implement Coordinate Conversion for Export | ✓&nbsp;done | -            | None | N/A |
| 12.5 | Implement Data Integrity Validation | ✓&nbsp;done | -            | None | N/A |
| 12.6 | Implement Comprehensive Error Handling | ✓&nbsp;done | -            | None | N/A |
| 12.7 | Implement Individual Data Section Writers | ✓&nbsp;done | -            | None | N/A |
| 12.8 | Add File Format Options and Configuration | ✓&nbsp;done | -            | None | N/A |
| 13 | Implement Scene and Cover Object Rendering | ✓&nbsp;done | low | 5, 7 | N/A |
| 13.1 | Parse SceneFile and ScenePart Data | ✓&nbsp;done | -            | None | N/A |
| 13.2 | Implement Cover Object Rendering | ✓&nbsp;done | -            | None | N/A |
| 13.3 | Create Scene Layer Management System | ✓&nbsp;done | -            | None | N/A |
| 13.4 | Implement Y-Sorting for Depth Ordering | ✓&nbsp;done | -            | None | N/A |
| 13.5 | Integrate 3D Object Placement | ✓&nbsp;done | -            | None | N/A |
| 13.6 | Create ObjectLayer Integration | ✓&nbsp;done | -            | None | N/A |
| 13.7 | Implement Scene Object Texture Loading | ✓&nbsp;done | -            | None | N/A |
| 13.8 | Test with Game Assets | ✓&nbsp;done | -            | None | N/A |
| 14 | Optimize Rendering Performance | ○&nbsp;pending | low | 5, 13 | N/A |
| 14.1 | Implement Chunk Loading System | ○&nbsp;pending | -            | None | N/A |
| 14.2 | Develop Texture Atlasing System | ○&nbsp;pending | -            | None | N/A |
| 14.3 | Implement LOD (Level of Detail) System | ○&nbsp;pending | -            | None | N/A |
| 14.4 | Create Object Pooling System | ○&nbsp;pending | -            | None | N/A |
| 14.5 | Add Viewport Culling | ○&nbsp;pending | -            | None | N/A |
| 14.6 | Performance Testing and Benchmarking | ○&nbsp;pending | -            | None | N/A |
| 14.7 | Integration with Existing Systems | ○&nbsp;pending | -            | None | N/A |
| 14.8 | Performance Documentation and Monitoring | ○&nbsp;pending | -            | None | N/A |
| 15 | Create Comprehensive Test Suite | ✓&nbsp;done | medium | 11 | N/A |

> 📋 **End of Taskmaster Export** - Tasks are synced from your project using the `sync-readme` command.
<!-- TASKMASTER_EXPORT_END -->













