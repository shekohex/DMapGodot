#if TOOLS
using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Nodes;
using System;
using System.IO;

namespace DMapImporter.Importers
{
    [Tool]
    public partial class DMapImporter : EditorImportPlugin
    {
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
                GD.Print($"[DMapImporter] Starting import of: {sourceFile}");
                
                // Validate source file exists
                if (!File.Exists(sourceFile))
                {
                    GD.PrintErr($"[DMapImporter] Source file not found: {sourceFile}");
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
                
                GD.Print($"[DMapImporter] Import options - TileSize: {tileSize}, Terrain: {enableTerrain}, Portals: {enablePortals}, Objects: {enableObjects}");
                
                // Load DMAP file using existing parser
                DmapFile dmap;
                try
                {
                    dmap = new DmapFile(sourceFile);
                    GD.Print($"[DMapImporter] Successfully loaded DMAP: {dmap.DmapName}, Size: {dmap.SizeTiles.Width}x{dmap.SizeTiles.Height}");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[DMapImporter] Failed to load DMAP file: {ex.Message}");
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
                    GD.Print($"[DMapImporter] Successfully populated renderer with DMAP data");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[DMapImporter] Failed to populate renderer: {ex.Message}");
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
                        GD.PrintErr($"[DMapImporter] Failed to pack scene: {result}");
                        renderer?.QueueFree();
                        return result;
                    }
                    GD.Print("[DMapImporter] Successfully packed scene");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[DMapImporter] Exception while packing scene: {ex.Message}");
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
                        GD.PrintErr($"[DMapImporter] Failed to save PackedScene: {saveResult}");
                        return saveResult;
                    }
                    GD.Print($"[DMapImporter] Successfully saved PackedScene to: {outputPath}");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[DMapImporter] Exception while saving PackedScene: {ex.Message}");
                    return Error.FileCantWrite;
                }
                
                // Clean up temporary nodes
                renderer?.QueueFree();
                
                GD.Print($"[DMapImporter] Import completed successfully");
                return Error.Ok;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[DMapImporter] Unexpected error during import: {ex.Message}");
                GD.PrintErr($"[DMapImporter] Stack trace: {ex.StackTrace}");
                return Error.Failed;
            }
        }
    }
}
#endif