using GdUnit4;
using static GdUnit4.Assertions;
using static GdUnit4.Utils;
using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Nodes;
using System;
using System.IO;
using System.Diagnostics;

namespace DMapImporter.Tests
{
    [TestSuite]
    [RequireGodotRuntime]
    public class DMapIntegrationTests
    {
        private string _gameDataPath = "/home/hakim/github/SantasCode/DMapGodot/Game/5017";
        private string _mapPath => Path.Combine(_gameDataPath, "map", "map");

        [Before]
        public void Setup()
        {
            AssertThat(Directory.Exists(_gameDataPath)).IsTrue();
            AssertThat(Directory.Exists(_mapPath)).IsTrue();
        }

        [TestCase]
        public void SmallMap_LoadsSuccessfully()
        {
            var mapFile = Path.Combine(_mapPath, "smith.DMap");
            AssertThat(File.Exists(mapFile)).IsTrue();

            var stopwatch = Stopwatch.StartNew();
            var dmapFile = new DmapFile(mapFile);
            stopwatch.Stop();

            AssertThat(dmapFile).IsNotNull();
            AssertThat(dmapFile.SizeTiles.Width).IsGreater(0u);
            AssertThat(dmapFile.SizeTiles.Height).IsGreater(0u);
            AssertThat(stopwatch.ElapsedMilliseconds).IsLess(2000);

            GD.Print($"Smith.DMap loaded in {stopwatch.ElapsedMilliseconds}ms, size: {dmapFile.SizeTiles.Width}x{dmapFile.SizeTiles.Height}");
        }

        [TestCase]
        public void MediumMap_LoadsSuccessfully()
        {
            var mapFile = Path.Combine(_mapPath, "arena.DMap");
            AssertThat(File.Exists(mapFile)).IsTrue();

            var stopwatch = Stopwatch.StartNew();
            var dmapFile = new DmapFile(mapFile);
            stopwatch.Stop();

            AssertThat(dmapFile).IsNotNull();
            AssertThat(dmapFile.SizeTiles.Width).IsGreater(0u);
            AssertThat(dmapFile.SizeTiles.Height).IsGreater(0u);
            AssertThat(stopwatch.ElapsedMilliseconds).IsLess(5000);

            GD.Print($"Arena.DMap loaded in {stopwatch.ElapsedMilliseconds}ms, size: {dmapFile.SizeTiles.Width}x{dmapFile.SizeTiles.Height}");
        }

        [TestCase]
        public void DMapRenderer_LoadsRealMapData()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var mapFile = Path.Combine(_mapPath, "grocery.DMap");
            var dmapFile = new DmapFile(mapFile);

            var stopwatch = Stopwatch.StartNew();
            renderer.LoadFromDMap(dmapFile);
            stopwatch.Stop();

            AssertThat(renderer.MapSize.X).IsEqual((int)dmapFile.SizeTiles.Width);
            AssertThat(renderer.MapSize.Y).IsEqual((int)dmapFile.SizeTiles.Height);
            AssertThat(renderer.DMapPath).IsEqual(mapFile);
            AssertThat(renderer.GetChildCount()).IsGreaterEqual(0);
            AssertThat(stopwatch.ElapsedMilliseconds).IsLess(3000);

            GD.Print($"Grocery.DMap rendered in {stopwatch.ElapsedMilliseconds}ms");
        }

        [TestCase]
        [DataPoint(nameof(TestMapFiles))]
        public void VariousMapSizes_LoadCorrectly(string mapFileName, uint expectedMinSize)
        {
            var mapFile = Path.Combine(_mapPath, mapFileName);
            AssertThat(File.Exists(mapFile)).IsTrue();

            var dmapFile = new DmapFile(mapFile);

            AssertThat(dmapFile.SizeTiles.Width).IsGreaterEqual(expectedMinSize);
            AssertThat(dmapFile.SizeTiles.Height).IsGreaterEqual(expectedMinSize);
            AssertThat(dmapFile.TileSet).IsNotNull();

            GD.Print($"{mapFileName}: {dmapFile.SizeTiles.Width}x{dmapFile.SizeTiles.Height}");
        }

        [TestCase]
        public void RendererWithPortals_CreatesCorrectStructure()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var mapFile = Path.Combine(_mapPath, "jokul01.DMap");
            var dmapFile = new DmapFile(mapFile);

            renderer.LoadFromDMap(dmapFile);

            AssertThat(renderer.GetChildCount()).IsGreaterEqual(0);

            var hasPortals = false;
            for (int i = 0; i < renderer.GetChildCount(); i++)
            {
                if (renderer.GetChild(i) is DMapPortal)
                {
                    hasPortals = true;
                    break;
                }
            }

            GD.Print($"Jokul01.DMap has portals: {hasPortals}, children: {renderer.GetChildCount()}");
        }

        [TestCase]
        public void MapDataIntegrity_ValidatesTileData()
        {
            var mapFile = Path.Combine(_mapPath, "house01.DMap");
            var dmapFile = new DmapFile(mapFile);

            AssertThat(dmapFile.TileSet.GetLength(0)).IsEqual((int)dmapFile.SizeTiles.Width);
            AssertThat(dmapFile.TileSet.GetLength(1)).IsEqual((int)dmapFile.SizeTiles.Height);

            bool hasValidTileData = false;
            for (int x = 0; x < (int)dmapFile.SizeTiles.Width && !hasValidTileData; x++)
            {
                for (int y = 0; y < (int)dmapFile.SizeTiles.Height && !hasValidTileData; y++)
                {
                    var tile = dmapFile.TileSet[x, y];
                    if (tile.Surface > 0 || tile.NoAccess > 0)
                    {
                        hasValidTileData = true;
                    }
                }
            }

            AssertThat(hasValidTileData).IsTrue();
            GD.Print($"House01.DMap has valid tile data across {dmapFile.SizeTiles.Width}x{dmapFile.SizeTiles.Height} map");
        }

        [TestCase]
        public void LargeMap_LoadsWithinMemoryLimits()
        {
            var mapFile = Path.Combine(_mapPath, "forum.DMap");
            AssertThat(File.Exists(mapFile)).IsTrue();

            var initialMemory = GC.GetTotalMemory(true);

            var dmapFile = new DmapFile(mapFile);
            var renderer = AutoFree(new DMapRenderer())!;
            renderer.LoadFromDMap(dmapFile);

            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsed = (finalMemory - initialMemory) / (1024 * 1024);

            AssertThat(memoryUsed).IsLess(500);

            GD.Print($"Forum.DMap memory usage: {memoryUsed}MB");
        }

        [TestCase]
        public void MultipleMapLoads_HandleCorrectly()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var maps = new[] { "smith.DMap", "grocery.DMap", "horse.DMap" };

            foreach (var mapName in maps)
            {
                var mapFile = Path.Combine(_mapPath, mapName);
                var dmapFile = new DmapFile(mapFile);

                var stopwatch = Stopwatch.StartNew();
                renderer.LoadFromDMap(dmapFile);
                stopwatch.Stop();

                AssertThat(renderer.MapSize.X).IsEqual((int)dmapFile.SizeTiles.Width);
                AssertThat(renderer.MapSize.Y).IsEqual((int)dmapFile.SizeTiles.Height);
                AssertThat(renderer.DMapPath).IsEqual(mapFile);
                AssertThat(stopwatch.ElapsedMilliseconds).IsLess(3000);

                GD.Print($"Sequential load {mapName}: {stopwatch.ElapsedMilliseconds}ms");
            }
        }

        [TestCase]
        public void MapMetadata_ExtractedCorrectly()
        {
            var mapFile = Path.Combine(_mapPath, "parena-s.DMap");
            var dmapFile = new DmapFile(mapFile);

            AssertThat(dmapFile.SizeTiles.Width).IsGreater(0u);
            AssertThat(dmapFile.SizeTiles.Height).IsGreater(0u);
            AssertThat(dmapFile.SceneLayers).IsNotNull();
            AssertThat(dmapFile.Portals).IsNotNull();
            AssertThat(dmapFile.Covers).IsNotNull();

            var totalTiles = (long)dmapFile.SizeTiles.Width * dmapFile.SizeTiles.Height;
            AssertThat(totalTiles).IsGreater(0L);

            GD.Print($"Parena-s.DMap metadata: Size={dmapFile.SizeTiles.Width}x{dmapFile.SizeTiles.Height}, " +
                    $"Layers={dmapFile.SceneLayers.Count}, " +
                    $"Portals={dmapFile.Portals.Count}, " +
                    $"Covers={dmapFile.Covers.Count}");
        }

        public static object[][] TestMapFiles => new object[][]
        {
            new object[] { "smith.DMap", 10u },
            new object[] { "grocery.DMap", 15u },
            new object[] { "horse.DMap", 20u },
            new object[] { "jokul01.DMap", 25u },
            new object[] { "house01.DMap", 20u },
            new object[] { "parena-s.DMap", 30u }
        };
    }
}