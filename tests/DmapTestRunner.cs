using Godot;
using System;
using System.IO;
using DMapImporter.Core.Dmap;

[Tool]
public partial class DmapTestRunner : Node
{
    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            TestDmapFileLoading();
        }
    }

    public static void TestDmapFileLoading()
    {
        string testMapPath = "Game/5017/map/map/Gulf.DMap";

        if (!File.Exists(testMapPath))
        {
            GD.PrintErr($"Test map file not found: {testMapPath}");
            return;
        }

        try
        {
            GD.Print($"Testing DMAP file loading with SharpCompress: {testMapPath}");

            var dmapFile = new DmapFile(testMapPath);

            GD.Print($"Successfully loaded DMAP file:");
            GD.Print($"  - Map Version: {dmapFile.MapVersion}");
            GD.Print($"  - Size: {dmapFile.SizeTiles.Width}x{dmapFile.SizeTiles.Height}");
            GD.Print($"  - Tiles loaded: {dmapFile.TileSet?.Length ?? 0}");
            GD.Print($"  - Portals: {dmapFile.Portals?.Count ?? 0}");
            GD.Print($"  - Covers: {dmapFile.Covers?.Count ?? 0}");
            GD.Print($"  - Terrain Scenes: {dmapFile.TerrainScenes?.Count ?? 0}");
            GD.Print($"  - Scene Layers: {dmapFile.SceneLayers?.Count ?? 0}");

            GD.Print("DMAP SharpCompress test PASSED!");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"DMAP SharpCompress test FAILED: {ex.Message}");
        }
    }
}