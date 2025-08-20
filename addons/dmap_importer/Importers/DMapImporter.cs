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
            var loggerFactory = DMapLoggerFactory.CreateGodotOptimizedOptions();
            var factory = DMapLoggerFactory.Create(loggerFactory);
            _logger = factory.CreateLogger<DMapImporter>();
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
                _logger.LogInformation("Starting import of: {sourceFile}", sourceFile);
                
                // Validate source file exists
                if (!File.Exists(sourceFile))
                {
                    _logger.LogError("Source file not found: {sourceFile}", sourceFile);
                    return Error.FileNotFound;
                }
                
                // Extract import options
                int tileSize = options.ContainsKey("tile_size") ? options["tile_size"].AsInt32() : 32;
                bool enableTerrain = options.ContainsKey("enable_terrain") ? options["enable_terrain"].AsBool() : true;
                bool enablePortals = options.ContainsKey("enable_portals") ? options["enable_portals"].AsBool() : true;
                bool enableObjects = options.ContainsKey("enable_objects") ? options["enable_objects"].AsBool() : true;
                int coordinateSystem = options.ContainsKey("coordinate_system") ? options["coordinate_system"].AsInt32() : 0;
                float textureQuality = options.ContainsKey("texture_quality") ? options["texture_quality"].AsSingle() : 0.8f;
                bool enableCompression = options.ContainsKey("enable_compression") ? options["enable_compression"].AsBool() : true;
                
                _logger.LogDebug("Import options - TileSize: {tileSize}, Terrain: {enableTerrain}, Portals: {enablePortals}, Objects: {enableObjects}", 
                    tileSize, enableTerrain, enablePortals, enableObjects);
                
                // Load DMAP file using existing parser
                DmapFile dmap;
                try
                {
                    dmap = new DmapFile(sourceFile);
                    _logger.LogInformation("Successfully loaded DMAP: {dmapName}, Size: {width}x{height}", 
                        dmap.DmapName, dmap.SizeTiles.Width, dmap.SizeTiles.Height);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load DMAP file");
                    return Error.ParseError;
                }
                
                // Create DMapRenderer as root node
                var renderer = new DMapRenderer();
                renderer.Name = Path.GetFileNameWithoutExtension(sourceFile);
                renderer.TileSize = tileSize;
                
                // Load DMAP data into renderer
                try
                {
                    renderer.LoadFromDMap(dmap);
                    _logger.LogDebug("Successfully populated renderer with DMAP data");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to populate renderer");
                    renderer?.QueueFree();
                    return Error.CantCreate;
                }
                
                // Create PackedScene
                var packedScene = new PackedScene();
                try
                {
                    var result = packedScene.Pack(renderer);
                    if (result != Error.Ok)
                    {
                        _logger.LogError("Failed to pack scene: {result}", result);
                        renderer?.QueueFree();
                        return result;
                    }
                    _logger.LogDebug("Successfully packed scene");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while packing scene");
                    renderer?.QueueFree();
                    return Error.CantCreate;
                }
                
                // Save PackedScene
                string outputPath = $"{savePath}.{_GetSaveExtension()}";
                try
                {
                    var saveResult = ResourceSaver.Save(packedScene, outputPath);
                    if (saveResult != Error.Ok)
                    {
                        _logger.LogError("Failed to save PackedScene: {saveResult}", saveResult);
                        return saveResult;
                    }
                    _logger.LogInformation("Successfully saved PackedScene to: {outputPath}", outputPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while saving PackedScene");
                    return Error.FileCantWrite;
                }
                
                // Clean up temporary nodes
                renderer?.QueueFree();
                
                _logger.LogInformation("Import completed successfully");
                return Error.Ok;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during import");
                return Error.Failed;
            }
        }
    }
}
#endif