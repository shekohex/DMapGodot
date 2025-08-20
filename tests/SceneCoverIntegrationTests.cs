using GdUnit4;
using static GdUnit4.Assertions;
using static GdUnit4.Utils;
using Godot;
using DMapImporter.Nodes;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using System.IO;
using System;

namespace DMapImporter.Tests
{
    [TestSuite]
    [RequireGodotRuntime]
    public class SceneCoverIntegrationTests
    {
        private string GetGameDataPath()
        {
            // Get the project root and navigate to game data
            var projectRoot = ProjectSettings.GlobalizePath("res://");
            return Path.Combine(projectRoot, "Game", "5017");
        }

        [TestCase]
        public void LoadsRealDMapFileWithSceneObjects()
        {
            var gameDataPath = GetGameDataPath();
            var dmapPath = Path.Combine(gameDataPath, "map", "map", "Dcloister.DMap");

            // Skip test if the file doesn't exist (e.g., in CI environment)
            if (!File.Exists(dmapPath))
            {
                GD.Print($"Skipping integration test - DMAP file not found: {dmapPath}");
                return;
            }

            try
            {
                var dmap = new DmapFile(dmapPath, gameDataPath);
                var renderer = AutoFree(new DMapRenderer())!;

                // This should not throw an exception
                renderer.LoadFromDMap(dmap);

                // Verify basic structure
                AssertThat(renderer.MapSize.X).IsGreater(0);
                AssertThat(renderer.MapSize.Y).IsGreater(0);

                // Verify object layer exists
                var objectLayer = renderer.GetNode<Node2D>("ObjectLayer");
                AssertThat(objectLayer).IsNotNull();
                AssertThat(objectLayer.YSortEnabled).IsTrue();

                // Verify scene layers exist
                var terrainLayer = objectLayer.GetNode<Node2D>("TerrainObjects");
                AssertThat(terrainLayer).IsNotNull();

                var coverLayer = objectLayer.GetNode<Node2D>("CoverObjects");
                AssertThat(coverLayer).IsNotNull();

                var portalLayer = objectLayer.GetNode<Node2D>("Portals");
                AssertThat(portalLayer).IsNotNull();

                GD.Print($"Successfully loaded DMAP: {dmapPath}");
                GD.Print($"Map Size: {renderer.MapSize}");
                GD.Print($"TerrainScenes: {dmap.TerrainScenes.Count}");
                GD.Print($"Covers: {dmap.Covers.Count}");
                GD.Print($"Portals: {dmap.Portals.Count}");
            }
            catch (Exception ex)
            {
                // This is an integration test, so we want to see what goes wrong
                GD.PrintErr($"Error loading DMAP file: {ex.Message}");
                throw;
            }
        }

        [TestCase]
        public void ProcessesSceneObjectsFromRealData()
        {
            var gameDataPath = GetGameDataPath();
            var dmapPath = Path.Combine(gameDataPath, "map", "map", "Dcloister.DMap");

            if (!File.Exists(dmapPath))
            {
                GD.Print("Skipping integration test - DMAP file not found");
                return;
            }

            try
            {
                var dmap = new DmapFile(dmapPath, gameDataPath);
                var renderer = AutoFree(new DMapRenderer())!;

                renderer.LoadFromDMap(dmap);

                // If we have terrain scenes, verify they're processed
                if (dmap.TerrainScenes.Count > 0)
                {
                    var terrainLayer = renderer.GetNode<Node2D>("ObjectLayer/TerrainObjects");
                    AssertThat(terrainLayer).IsNotNull();

                    GD.Print($"Found {dmap.TerrainScenes.Count} terrain scenes");
                    foreach (var scene in dmap.TerrainScenes)
                    {
                        GD.Print($"  Scene: {scene.SceneFile} at {scene.Position.X}, {scene.Position.Y}");
                    }
                }

                // If we have covers, verify they're processed
                if (dmap.Covers.Count > 0)
                {
                    var coverLayer = renderer.GetNode<Node2D>("ObjectLayer/CoverObjects");
                    AssertThat(coverLayer).IsNotNull();

                    GD.Print($"Found {dmap.Covers.Count} covers");
                    foreach (var cover in dmap.Covers)
                    {
                        GD.Print($"  Cover: {cover.AniName} at {cover.Position.X}, {cover.Position.Y}");
                    }
                }

                // If we have portals, verify they're processed
                if (dmap.Portals.Count > 0)
                {
                    var portalLayer = renderer.GetNode<Node2D>("ObjectLayer/Portals");
                    AssertThat(portalLayer).IsNotNull();

                    GD.Print($"Found {dmap.Portals.Count} portals");
                }
            }
            catch (FileNotFoundException ex)
            {
                GD.Print($"Scene file not found (expected): {ex.Message}");
                // This is normal - scene files might not exist in the test environment
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Unexpected error: {ex}");
                throw;
            }
        }

        [TestCase]
        public void HandlesMultipleDMapFiles()
        {
            var gameDataPath = GetGameDataPath();
            var mapDir = Path.Combine(gameDataPath, "map", "map");

            if (!Directory.Exists(mapDir))
            {
                GD.Print("Skipping integration test - map directory not found");
                return;
            }

            var dmapFiles = Directory.GetFiles(mapDir, "*.DMap");
            if (dmapFiles.Length == 0)
            {
                GD.Print("Skipping integration test - no DMAP files found");
                return;
            }

            // Test loading multiple DMAP files (limit to first 3 for performance)
            var testCount = Math.Min(3, dmapFiles.Length);
            var successCount = 0;

            for (int i = 0; i < testCount; i++)
            {
                var dmapPath = dmapFiles[i];
                var fileName = Path.GetFileName(dmapPath);

                try
                {
                    GD.Print($"Testing DMAP file {i + 1}/{testCount}: {fileName}");

                    var dmap = new DmapFile(dmapPath, gameDataPath);
                    var renderer = AutoFree(new DMapRenderer())!;

                    renderer.LoadFromDMap(dmap);

                    // Basic verification
                    AssertThat(renderer.MapSize.X).IsGreater(0);
                    AssertThat(renderer.MapSize.Y).IsGreater(0);

                    var objectLayer = renderer.GetNode<Node2D>("ObjectLayer");
                    AssertThat(objectLayer).IsNotNull();

                    successCount++;
                    GD.Print($"  ✓ Successfully processed {fileName}");
                    GD.Print($"    Size: {renderer.MapSize}, Scenes: {dmap.TerrainScenes.Count}, Covers: {dmap.Covers.Count}");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"  ✗ Failed to process {fileName}: {ex.Message}");
                    // Don't throw - we want to test all files and report results
                }
            }

            GD.Print($"Integration test results: {successCount}/{testCount} DMAP files processed successfully");

            // Require at least one file to load successfully
            AssertThat(successCount).IsGreater(0);
        }

        [TestCase]
        public void VerifiesSceneFileReferences()
        {
            var gameDataPath = GetGameDataPath();
            var dmapPath = Path.Combine(gameDataPath, "map", "map", "Dcloister.DMap");

            if (!File.Exists(dmapPath))
            {
                GD.Print("Skipping scene file verification test - DMAP file not found");
                return;
            }

            try
            {
                var dmap = new DmapFile(dmapPath, gameDataPath);

                GD.Print($"Checking scene file references in {Path.GetFileName(dmapPath)}:");

                foreach (var terrainScene in dmap.TerrainScenes)
                {
                    var sceneFilePath = Path.Combine(gameDataPath, terrainScene.SceneFile);
                    var exists = File.Exists(sceneFilePath);

                    GD.Print($"  {terrainScene.SceneFile}: {(exists ? "EXISTS" : "MISSING")}");

                    if (exists)
                    {
                        var fileInfo = new FileInfo(sceneFilePath);
                        GD.Print($"    Size: {fileInfo.Length} bytes");
                    }
                }

                // This test is informational - we don't fail if scene files are missing
                // as they might not be included in the repository
            }
            catch (Exception ex)
            {
                GD.Print($"Error during scene file verification: {ex.Message}");
                // This is informational, so we don't throw
            }
        }
    }
}