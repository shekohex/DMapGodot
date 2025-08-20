using GdUnit4;
using static GdUnit4.Assertions;
using static GdUnit4.Utils;
using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Nodes;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace DMapImporter.Tests
{
    [TestSuite]
    [RequireGodotRuntime]
    public class PerformanceBenchmarkTests
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
        public void SmallMapLoading_MeetsPerformanceTarget()
        {
            var mapFile = Path.Combine(_mapPath, "smith.DMap");
            var stopwatch = Stopwatch.StartNew();

            var dmapFile = new DmapFile(mapFile);

            stopwatch.Stop();
            var loadTimeMs = stopwatch.ElapsedMilliseconds;

            AssertThat(loadTimeMs).IsLess(500);

            GD.Print($"Small map (smith.DMap) loaded in {loadTimeMs}ms (target: <500ms)");
        }

        [TestCase]
        public void MediumMapLoading_MeetsPerformanceTarget()
        {
            var mapFile = Path.Combine(_mapPath, "arena.DMap");
            var stopwatch = Stopwatch.StartNew();

            var dmapFile = new DmapFile(mapFile);

            stopwatch.Stop();
            var loadTimeMs = stopwatch.ElapsedMilliseconds;

            AssertThat(loadTimeMs).IsLess(2000);

            GD.Print($"Medium map (arena.DMap) loaded in {loadTimeMs}ms (target: <2000ms)");
        }

        [TestCase]
        public void LargeMapLoading_MeetsPerformanceTarget()
        {
            var mapFile = Path.Combine(_mapPath, "Gulf.DMap");
            if (!File.Exists(mapFile))
            {
                GD.Print("Gulf.DMap not found, skipping large map test");
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            var dmapFile = new DmapFile(mapFile);

            stopwatch.Stop();
            var loadTimeMs = stopwatch.ElapsedMilliseconds;

            AssertThat(loadTimeMs).IsLess(10000);

            GD.Print($"Large map (Gulf.DMap) loaded in {loadTimeMs}ms (target: <10000ms)");
        }

        [TestCase]
        public void MapRendering_MeetsPerformanceTarget()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var mapFile = Path.Combine(_mapPath, "grocery.DMap");
            var dmapFile = new DmapFile(mapFile);

            var stopwatch = Stopwatch.StartNew();

            renderer.LoadFromDMap(dmapFile);

            stopwatch.Stop();
            var renderTimeMs = stopwatch.ElapsedMilliseconds;

            AssertThat(renderTimeMs).IsLess(1000);

            GD.Print($"Map rendering completed in {renderTimeMs}ms (target: <1000ms)");
        }

        [TestCase]
        public void MemoryUsage_StaysWithinLimits()
        {
            var mapFile = Path.Combine(_mapPath, "forum.DMap");

            var initialMemory = GC.GetTotalMemory(true);

            var dmapFile = new DmapFile(mapFile);
            var renderer = AutoFree(new DMapRenderer())!;
            renderer.LoadFromDMap(dmapFile);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsedMB = (finalMemory - initialMemory) / (1024 * 1024);

            AssertThat(memoryUsedMB).IsLess(100);

            GD.Print($"Memory usage: {memoryUsedMB}MB (target: <100MB)");
        }

        [TestCase]
        [DataPoint(nameof(PerformanceTestMaps))]
        public void BatchMapLoadingPerformance_Benchmarks(string mapFileName, long maxLoadTimeMs)
        {
            var mapFile = Path.Combine(_mapPath, mapFileName);
            AssertThat(File.Exists(mapFile)).IsTrue();

            var stopwatch = Stopwatch.StartNew();

            var dmapFile = new DmapFile(mapFile);

            stopwatch.Stop();
            var loadTimeMs = stopwatch.ElapsedMilliseconds;

            AssertThat(loadTimeMs).IsLess(maxLoadTimeMs);

            GD.Print($"{mapFileName}: {loadTimeMs}ms (target: <{maxLoadTimeMs}ms)");
        }

        [TestCase]
        public void MultipleSequentialLoads_PerformanceConsistency()
        {
            var maps = new[] { "smith.DMap", "grocery.DMap", "horse.DMap" };
            var loadTimes = new List<long>();

            foreach (var mapName in maps)
            {
                var mapFile = Path.Combine(_mapPath, mapName);
                var stopwatch = Stopwatch.StartNew();

                var dmapFile = new DmapFile(mapFile);

                stopwatch.Stop();
                loadTimes.Add(stopwatch.ElapsedMilliseconds);
            }

            var maxTime = Math.Max(Math.Max(loadTimes[0], loadTimes[1]), loadTimes[2]);
            var minTime = Math.Min(Math.Min(loadTimes[0], loadTimes[1]), loadTimes[2]);
            var variation = (double)(maxTime - minTime) / minTime;

            AssertThat(variation).IsLess(2.0);

            GD.Print($"Sequential loads: {string.Join("ms, ", loadTimes)}ms, variation: {variation:P1}");
        }

        [TestCase]
        public void RendererSceneGraph_BuildsEfficiently()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var mapFile = Path.Combine(_mapPath, "jokul01.DMap");
            var dmapFile = new DmapFile(mapFile);

            var stopwatch = Stopwatch.StartNew();

            renderer.LoadFromDMap(dmapFile);

            var childCount = renderer.GetChildCount();
            var buildTimeMs = stopwatch.ElapsedMilliseconds;

            stopwatch.Stop();

            var timePerChild = childCount > 0 ? (double)buildTimeMs / childCount : 0;
            AssertThat(timePerChild).IsLess(10.0);

            GD.Print($"Scene graph: {childCount} nodes built in {buildTimeMs}ms ({timePerChild:F2}ms/node)");
        }

        [TestCase]
        public void TileDataAccess_OptimalPerformance()
        {
            var mapFile = Path.Combine(_mapPath, "house01.DMap");
            var dmapFile = new DmapFile(mapFile);

            var stopwatch = Stopwatch.StartNew();

            var tileCount = 0;
            var validTiles = 0;

            for (int x = 0; x < (int)dmapFile.SizeTiles.Width; x++)
            {
                for (int y = 0; y < (int)dmapFile.SizeTiles.Height; y++)
                {
                    var tile = dmapFile.TileSet[x, y];
                    tileCount++;
                    if (tile.Surface > 0 || tile.NoAccess > 0)
                    {
                        validTiles++;
                    }
                }
            }

            stopwatch.Stop();

            var timePerTileNs = (stopwatch.ElapsedTicks * 1000000000.0) / Stopwatch.Frequency / tileCount;
            AssertThat(timePerTileNs).IsLess(1000.0);

            GD.Print($"Tile access: {tileCount} tiles in {stopwatch.ElapsedMilliseconds}ms ({timePerTileNs:F1}ns/tile)");
        }

        [TestCase]
        public void ConcurrentMapOperations_ThreadSafety()
        {
            var maps = new[] { "smith.DMap", "grocery.DMap", "horse.DMap" };
            var loadTimes = new long[maps.Length];

            var stopwatch = Stopwatch.StartNew();

            System.Threading.Tasks.Parallel.For(0, maps.Length, i =>
            {
                var mapFile = Path.Combine(_mapPath, maps[i]);
                var innerStopwatch = Stopwatch.StartNew();
                var dmapFile = new DmapFile(mapFile);
                innerStopwatch.Stop();
                loadTimes[i] = innerStopwatch.ElapsedMilliseconds;
            });

            stopwatch.Stop();

            var maxTime = loadTimes.Max();
            AssertThat(maxTime).IsLess(3000);
            AssertThat(stopwatch.ElapsedMilliseconds).IsLess(maxTime + 500);

            GD.Print($"Parallel loads: Total {stopwatch.ElapsedMilliseconds}ms, " +
                    $"Individual: [{string.Join(", ", loadTimes)}]ms");
        }

        public static object[][] PerformanceTestMaps => new object[][]
        {
            new object[] { "smith.DMap", 500L },
            new object[] { "grocery.DMap", 800L },
            new object[] { "horse.DMap", 1000L },
            new object[] { "jokul01.DMap", 1200L },
            new object[] { "house01.DMap", 1200L },
            new object[] { "arena.DMap", 2000L },
            new object[] { "forum.DMap", 3000L }
        };
    }
}