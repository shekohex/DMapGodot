#if TOOLS
using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Logging;
using DMapImporter.Nodes;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace DMapImporter.Importers
{
    [Tool]
    public partial class DMapImporter : EditorImportPlugin
    {
        private readonly ILogger<DMapImporter> _logger;

        public DMapImporter()
        {
            var loggerFactory = DMapLoggerFactory.CreateDevelopmentOptions(); // Use debug level for better diagnostics
            var factory = DMapLoggerFactory.Create(loggerFactory);
            _logger = factory.CreateLogger<DMapImporter>();
            
            // Log startup to verify logging is working
            _logger.LogInformation("DMapImporter initialized with development logging");
            GD.Print("[DMAP] DMapImporter initialized - logging active");
        }
        public override string _GetImporterName()
        {
            return "dmap.importer";
        }

        public override string _GetVisibleName()
        {
            return "DMAP Map File";
        }

        public override string[] _GetRecognizedExtensions()
        {
            return new[] { "dmap", "7z", "zmap" };
        }

        public override string _GetSaveExtension()
        {
            return "tscn";
        }

        public override string _GetResourceType()
        {
            return "PackedScene";
        }

        public override int _GetPresetCount()
        {
            return 2;
        }

        public override string _GetPresetName(int presetIndex)
        {
            return presetIndex switch
            {
                0 => "Default",
                1 => "High Quality",
                _ => "Unknown"
            };
        }

        public override float _GetPriority()
        {
            return 1.0f;
        }

        public override int _GetImportOrder()
        {
            return 0;
        }

        public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetImportOptions(string path, int presetIndex)
        {
            var options = new Godot.Collections.Array<Godot.Collections.Dictionary>();

            options.Add(new Godot.Collections.Dictionary()
            {
                { "name", "tile_size" },
                { "default_value", 32 },
                { "property_hint", (int)PropertyHint.Range },
                { "hint_string", "16,64,2" }
            });

            options.Add(new Godot.Collections.Dictionary()
            {
                { "name", "enable_terrain" },
                { "default_value", true }
            });

            options.Add(new Godot.Collections.Dictionary()
            {
                { "name", "enable_portals" },
                { "default_value", true }
            });

            options.Add(new Godot.Collections.Dictionary()
            {
                { "name", "enable_objects" },
                { "default_value", true }
            });

            options.Add(new Godot.Collections.Dictionary()
            {
                { "name", "coordinate_system" },
                { "default_value", 0 },
                { "property_hint", (int)PropertyHint.Enum },
                { "hint_string", "Godot Standard,DMAP Native" }
            });

            if (presetIndex == 1) // High Quality preset
            {
                options.Add(new Godot.Collections.Dictionary()
                {
                    { "name", "texture_quality" },
                    { "default_value", 1.0f },
                    { "property_hint", (int)PropertyHint.Range },
                    { "hint_string", "0.1,2.0,0.1" }
                });

                options.Add(new Godot.Collections.Dictionary()
                {
                    { "name", "enable_compression" },
                    { "default_value", false }
                });
            }
            else
            {
                options.Add(new Godot.Collections.Dictionary()
                {
                    { "name", "texture_quality" },
                    { "default_value", 0.8f },
                    { "property_hint", (int)PropertyHint.Range },
                    { "hint_string", "0.1,2.0,0.1" }
                });

                options.Add(new Godot.Collections.Dictionary()
                {
                    { "name", "enable_compression" },
                    { "default_value", true }
                });
            }

            return options;
        }

        public override bool _GetOptionVisibility(string path, StringName optionName, Godot.Collections.Dictionary options)
        {
            if (optionName == "texture_quality" && options.ContainsKey("enable_compression"))
            {
                return (bool)options["enable_compression"];
            }

            return true;
        }

        public override Error _Import(string sourceFile, string savePath,
            Godot.Collections.Dictionary options,
            Godot.Collections.Array<string> platformVariants,
            Godot.Collections.Array<string> genFiles)
        {
            try
            {
                // Enhanced logging with both structured and Godot console output
                _logger.LogInformation("=== DMAP IMPORT STARTED ===");
                _logger.LogInformation("Source: {sourceFile}", sourceFile);
                _logger.LogInformation("Save path: {savePath}", savePath);
                
                GD.Print($"[DMAP] Starting import of: {sourceFile}");
                GD.Print($"[DMAP] Target save path: {savePath}");

                // Convert Godot resource path to filesystem path
                string absoluteSourceFile = ProjectSettings.GlobalizePath(sourceFile);
                GD.Print($"[DMAP] Converted to absolute path: {absoluteSourceFile}");

                // Validate source file exists
                if (!File.Exists(absoluteSourceFile))
                {
                    var error = $"Source file not found: {sourceFile} (absolute: {absoluteSourceFile})";
                    _logger.LogError(error);
                    GD.PrintErr($"[DMAP ERROR] {error}");
                    
                    // Check if it's a symlink issue
                    if (sourceFile.Contains("Game/5017"))
                    {
                        GD.Print("[DMAP] Checking symlink resolution...");
                        var projectDir = ProjectSettings.GlobalizePath("res://");
                        var gameDir = Path.Combine(projectDir, "Game", "5017");
                        GD.Print($"[DMAP] Game directory: {gameDir}");
                        GD.Print($"[DMAP] Game directory exists: {Directory.Exists(gameDir)}");
                        if (Directory.Exists(gameDir))
                        {
                            var mapDir = Path.Combine(gameDir, "map", "map");
                            GD.Print($"[DMAP] Map directory: {mapDir}");
                            GD.Print($"[DMAP] Map directory exists: {Directory.Exists(mapDir)}");
                        }
                    }
                    
                    return Error.FileNotFound;
                }

                GD.Print($"[DMAP] File exists: {absoluteSourceFile}");

                // Extract import options with detailed logging
                int tileSize = options.ContainsKey("tile_size") ? options["tile_size"].AsInt32() : 32;
                bool enableTerrain = options.ContainsKey("enable_terrain") ? options["enable_terrain"].AsBool() : true;
                bool enablePortals = options.ContainsKey("enable_portals") ? options["enable_portals"].AsBool() : true;
                bool enableObjects = options.ContainsKey("enable_objects") ? options["enable_objects"].AsBool() : true;
                int coordinateSystem = options.ContainsKey("coordinate_system") ? options["coordinate_system"].AsInt32() : 0;
                float textureQuality = options.ContainsKey("texture_quality") ? options["texture_quality"].AsSingle() : 0.8f;
                bool enableCompression = options.ContainsKey("enable_compression") ? options["enable_compression"].AsBool() : true;

                var optionsInfo = $"TileSize: {tileSize}, Terrain: {enableTerrain}, Portals: {enablePortals}, Objects: {enableObjects}, CoordSystem: {coordinateSystem}, TextureQuality: {textureQuality}, Compression: {enableCompression}";
                _logger.LogInformation("Import options - {optionsInfo}", optionsInfo);
                GD.Print($"[DMAP] Import options: {optionsInfo}");

                // Load DMAP file using existing parser with enhanced error reporting
                DmapFile dmap;
                try
                {
                    GD.Print($"[DMAP] Loading DMAP file: {absoluteSourceFile}");
                    var fileInfo = new FileInfo(absoluteSourceFile);
                    GD.Print($"[DMAP] File size: {fileInfo.Length} bytes");
                    
                    dmap = new DmapFile(absoluteSourceFile);
                    var successMsg = $"Successfully loaded DMAP: {dmap.DmapName}, Size: {dmap.SizeTiles.Width}x{dmap.SizeTiles.Height}";
                    _logger.LogInformation(successMsg);
                    GD.Print($"[DMAP] {successMsg}");
                    GD.Print($"[DMAP] Portals: {dmap.Portals.Count}, TerrainScenes: {dmap.TerrainScenes.Count}, Covers: {dmap.Covers.Count}");
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to load DMAP file: {ex.Message}";
                    _logger.LogError(ex, errorMsg);
                    GD.PrintErr($"[DMAP ERROR] {errorMsg}");
                    GD.PrintErr($"[DMAP ERROR] Exception type: {ex.GetType().Name}");
                    if (ex.InnerException != null)
                    {
                        GD.PrintErr($"[DMAP ERROR] Inner exception: {ex.InnerException.Message}");
                    }
                    GD.PrintErr($"[DMAP ERROR] Stack trace: {ex.StackTrace}");
                    return Error.ParseError;
                }

                // Create DMapRenderer as root node
                GD.Print("[DMAP] Creating DMapRenderer node...");
                var renderer = new DMapRenderer();
                renderer.Name = Path.GetFileNameWithoutExtension(absoluteSourceFile);
                renderer.TileSize = tileSize;
                GD.Print($"[DMAP] Renderer created: {renderer.Name}, TileSize: {renderer.TileSize}");

                // Load DMAP data into renderer
                try
                {
                    GD.Print("[DMAP] Loading DMAP data into renderer...");
                    renderer.LoadFromDMap(dmap);
                    var successMsg = "Successfully populated renderer with DMAP data";
                    _logger.LogInformation(successMsg);
                    GD.Print($"[DMAP] {successMsg}");
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to populate renderer: {ex.Message}";
                    _logger.LogError(ex, errorMsg);
                    GD.PrintErr($"[DMAP ERROR] {errorMsg}");
                    GD.PrintErr($"[DMAP ERROR] Exception type: {ex.GetType().Name}");
                    if (ex.InnerException != null)
                    {
                        GD.PrintErr($"[DMAP ERROR] Inner exception: {ex.InnerException.Message}");
                    }
                    GD.PrintErr($"[DMAP ERROR] Stack trace: {ex.StackTrace}");
                    renderer?.QueueFree();
                    return Error.CantCreate;
                }

                // Create PackedScene
                GD.Print("[DMAP] Creating PackedScene...");
                var packedScene = new PackedScene();
                try
                {
                    GD.Print("[DMAP] Packing renderer into scene...");
                    var result = packedScene.Pack(renderer);
                    if (result != Error.Ok)
                    {
                        var errorMsg = $"Failed to pack scene: {result}";
                        _logger.LogError(errorMsg);
                        GD.PrintErr($"[DMAP ERROR] {errorMsg}");
                        renderer?.QueueFree();
                        return result;
                    }
                    var successMsg = "Successfully packed scene";
                    _logger.LogInformation(successMsg);
                    GD.Print($"[DMAP] {successMsg}");
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Exception while packing scene: {ex.Message}";
                    _logger.LogError(ex, errorMsg);
                    GD.PrintErr($"[DMAP ERROR] {errorMsg}");
                    GD.PrintErr($"[DMAP ERROR] Exception type: {ex.GetType().Name}");
                    if (ex.InnerException != null)
                    {
                        GD.PrintErr($"[DMAP ERROR] Inner exception: {ex.InnerException.Message}");
                    }
                    renderer?.QueueFree();
                    return Error.CantCreate;
                }

                // Save PackedScene
                string outputPath = $"{savePath}.{_GetSaveExtension()}";
                GD.Print($"[DMAP] Saving PackedScene to: {outputPath}");
                try
                {
                    var saveResult = ResourceSaver.Save(packedScene, outputPath);
                    if (saveResult != Error.Ok)
                    {
                        var errorMsg = $"Failed to save PackedScene: {saveResult}";
                        _logger.LogError(errorMsg);
                        GD.PrintErr($"[DMAP ERROR] {errorMsg}");
                        return saveResult;
                    }
                    var successMsg = $"Successfully saved PackedScene to: {outputPath}";
                    _logger.LogInformation(successMsg);
                    GD.Print($"[DMAP] {successMsg}");
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Exception while saving PackedScene: {ex.Message}";
                    _logger.LogError(ex, errorMsg);
                    GD.PrintErr($"[DMAP ERROR] {errorMsg}");
                    GD.PrintErr($"[DMAP ERROR] Exception type: {ex.GetType().Name}");
                    if (ex.InnerException != null)
                    {
                        GD.PrintErr($"[DMAP ERROR] Inner exception: {ex.InnerException.Message}");
                    }
                    return Error.FileCantWrite;
                }

                // Clean up temporary nodes
                renderer?.QueueFree();

                var completionMsg = "=== DMAP IMPORT COMPLETED SUCCESSFULLY ===";
                _logger.LogInformation(completionMsg);
                GD.Print($"[DMAP] {completionMsg}");
                return Error.Ok;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Unexpected error during import: {ex.Message}";
                _logger.LogError(ex, errorMsg);
                GD.PrintErr($"[DMAP FATAL ERROR] {errorMsg}");
                GD.PrintErr($"[DMAP FATAL ERROR] Exception type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    GD.PrintErr($"[DMAP FATAL ERROR] Inner exception: {ex.InnerException.Message}");
                }
                GD.PrintErr($"[DMAP FATAL ERROR] Stack trace: {ex.StackTrace}");
                return Error.Failed;
            }
        }
    }
}
#endif