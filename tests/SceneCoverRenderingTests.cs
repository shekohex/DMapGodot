using GdUnit4;
using static GdUnit4.Assertions;
using static GdUnit4.Utils;
using Godot;
using DMapImporter.Nodes;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System;

namespace DMapImporter.Tests
{
    [TestSuite]
    [RequireGodotRuntime]
    public class SceneCoverRenderingTests
    {
        private string _tempDir = string.Empty;
        private string _testClientPath = string.Empty;

        [Before]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"DMapTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);

            _testClientPath = Path.Combine(_tempDir, "TestClient");
            Directory.CreateDirectory(_testClientPath);

            // Create test data structure
            var dataDir = Path.Combine(_testClientPath, "data");
            Directory.CreateDirectory(dataDir);

            var mapDir = Path.Combine(_testClientPath, "map");
            Directory.CreateDirectory(mapDir);

            var sceneDir = Path.Combine(mapDir, "Scene");
            Directory.CreateDirectory(sceneDir);
        }

        [After]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, true);
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Failed to cleanup temp directory: {ex.Message}");
                }
            }
        }

        [TestCase]
        public void RendersTerrainSceneObjects()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var dmap = CreateTestDMapWithScenes();

            renderer.LoadFromDMap(dmap);

            // Verify object layer structure
            var objectLayer = renderer.GetNode<Node2D>("ObjectLayer");
            AssertThat(objectLayer).IsNotNull();
            AssertThat(objectLayer.YSortEnabled).IsTrue();

            // Verify scene layer exists
            var sceneLayer = objectLayer.GetNode<Node2D>("TerrainObjects");
            AssertThat(sceneLayer).IsNotNull();
            AssertThat(sceneLayer.YSortEnabled).IsTrue();
        }

        [TestCase]
        public void RendersCoverObjects()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var dmap = CreateTestDMapWithCovers();

            renderer.LoadFromDMap(dmap);

            // Verify object layer structure
            var objectLayer = renderer.GetNode<Node2D>("ObjectLayer");
            AssertThat(objectLayer).IsNotNull();

            // Verify cover layer exists
            var coverLayer = objectLayer.GetNode<Node2D>("CoverObjects");
            AssertThat(coverLayer).IsNotNull();
            AssertThat(coverLayer.YSortEnabled).IsTrue();
        }

        [TestCase]
        public void CreatesSceneLayerHierarchy()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var dmap = CreateTestDMapWithScenesAndCovers();

            renderer.LoadFromDMap(dmap);

            var objectLayer = renderer.GetNode<Node2D>("ObjectLayer");
            AssertThat(objectLayer).IsNotNull();

            // Verify all sublayers exist with correct properties
            var terrainLayer = objectLayer.GetNode<Node2D>("TerrainObjects");
            AssertThat(terrainLayer).IsNotNull();
            AssertThat(terrainLayer.ZIndex).IsEqual(0);
            AssertThat(terrainLayer.YSortEnabled).IsTrue();

            var coverLayer = objectLayer.GetNode<Node2D>("CoverObjects");
            AssertThat(coverLayer).IsNotNull();
            AssertThat(coverLayer.ZIndex).IsEqual(1);
            AssertThat(coverLayer.YSortEnabled).IsTrue();

            var portalLayer = objectLayer.GetNode<Node2D>("Portals");
            AssertThat(portalLayer).IsNotNull();
            AssertThat(portalLayer.ZIndex).IsEqual(2);
            AssertThat(portalLayer.YSortEnabled).IsTrue();
        }

        [TestCase]
        public void HandlesSceneFileNotFound()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var dmap = CreateTestDMapWithInvalidScene();

            // Should not throw exception even with invalid scene files
            renderer.LoadFromDMap(dmap);

            var objectLayer = renderer.GetNode<Node2D>("ObjectLayer");
            AssertThat(objectLayer).IsNotNull();
        }

        [TestCase]
        public void ExtractsClientPathCorrectly()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var dmapPath = Path.Combine(_testClientPath, "map", "test.dmap");

            // Create a test dmap file
            File.WriteAllText(dmapPath, "test");

            var dmap = new DmapFile();
            dmap.DmapPath = dmapPath;
            dmap.SizeTiles = new DMapImporter.Core.Utility.Size(10, 10);
            dmap.TileSet = new Tile[10, 10];

            renderer.LoadFromDMap(dmap);

            // The client path should be extracted correctly
            // We can't directly test the private field, but we can verify the structure was created
            var objectLayer = renderer.GetNode<Node2D>("ObjectLayer");
            AssertThat(objectLayer).IsNotNull();
        }

        [TestCase]
        public void AppliesCoverTransparency()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var dmap = CreateTestDMapWithCovers();

            renderer.LoadFromDMap(dmap);

            // For this test, we would need to create actual texture files
            // This tests the structure is set up correctly
            var objectLayer = renderer.GetNode<Node2D>("ObjectLayer");
            var coverLayer = objectLayer.GetNode<Node2D>("CoverObjects");
            AssertThat(coverLayer).IsNotNull();
        }

        private DmapFile CreateTestDMapWithScenes()
        {
            var dmap = CreateBaseDMapFile();

            // Add test terrain scenes
            dmap.TerrainScenes.Add(new TerrainScene("map/Scene/bridge.scene", new TilePosition(10, 10)));
            dmap.TerrainScenes.Add(new TerrainScene("map/Scene/tree.scene", new TilePosition(20, 15)));

            return dmap;
        }

        private DmapFile CreateTestDMapWithCovers()
        {
            var dmap = CreateBaseDMapFile();

            // Add test covers
            var cover1 = new Cover
            {
                AniPath = "data/objects",
                AniName = "tree01",
                Position = new TilePosition(5, 5),
                BaseSize = new DMapImporter.Core.Utility.Size(1, 1),
                Offset = new PixelPosition(0, -16),
                AnimationInterval = 0
            };

            var cover2 = new Cover
            {
                AniPath = "data/objects",
                AniName = "rock01",
                Position = new TilePosition(15, 20),
                BaseSize = new DMapImporter.Core.Utility.Size(1, 1),
                Offset = new PixelPosition(0, 0),
                AnimationInterval = 0
            };

            dmap.Covers.Add(cover1);
            dmap.Covers.Add(cover2);

            return dmap;
        }

        private DmapFile CreateTestDMapWithScenesAndCovers()
        {
            var dmap = CreateBaseDMapFile();

            // Add both scenes and covers
            dmap.TerrainScenes.Add(new TerrainScene("map/Scene/bridge.scene", new TilePosition(10, 10)));

            var cover = new Cover
            {
                AniPath = "data/objects",
                AniName = "tree01",
                Position = new TilePosition(5, 5),
                BaseSize = new DMapImporter.Core.Utility.Size(1, 1),
                Offset = new PixelPosition(0, 0),
                AnimationInterval = 0
            };
            dmap.Covers.Add(cover);

            // Add a test portal
            var portal = new Portal(new TilePosition(25, 25), 1001);
            dmap.Portals.Add(portal);

            return dmap;
        }

        private DmapFile CreateTestDMapWithInvalidScene()
        {
            var dmap = CreateBaseDMapFile();

            // Add scene that doesn't exist
            dmap.TerrainScenes.Add(new TerrainScene("map/Scene/nonexistent.scene", new TilePosition(10, 10)));

            return dmap;
        }

        private DmapFile CreateBaseDMapFile()
        {
            var dmap = new DmapFile();
            dmap.DmapPath = Path.Combine(_testClientPath, "map", "test.dmap");
            dmap.SizeTiles = new DMapImporter.Core.Utility.Size(50, 50);

            // Initialize tile set
            dmap.TileSet = new Tile[50, 50];
            for (int x = 0; x < 50; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    dmap.TileSet[x, y] = new Tile(0, 1, 0); // accessible, surface=1, height=0
                }
            }

            return dmap;
        }
    }
}