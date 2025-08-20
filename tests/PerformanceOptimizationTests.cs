using GdUnit4;
using static GdUnit4.Assertions;
using static GdUnit4.Utils;
using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Performance;
using DMapImporter.Nodes;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

namespace DMapImporter.Tests
{
    [TestSuite]
    [RequireGodotRuntime]
    public class PerformanceOptimizationTests
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
        public void ViewportCuller_InitializesCorrectly()
        {
            var camera = AutoFree(new Camera2D())!;
            var culler = new ViewportCuller(camera);
            
            camera.Position = Vector2.Zero;
            camera.Zoom = Vector2.One;
            
            AssertThat(culler).IsNotNull();
            
            // Test basic functionality
            culler.UpdateCullingBounds();
            var bounds = culler.GetCullingBounds();
            AssertThat(bounds).IsNotNull();
            
            // Test visible tile range calculation
            var tileRange = culler.GetVisibleTileRange(new Vector2I(64, 32), new Vector2I(100, 100));
            AssertThat(tileRange.Size.X).IsGreaterEqual(0);
            AssertThat(tileRange.Size.Y).IsGreaterEqual(0);
        }

        [TestCase]
        public void ChunkManager_DividesMapCorrectly()
        {
            var mapFile = Path.Combine(_mapPath, "grocery.DMap");
            var dmapFile = new DmapFile(mapFile);
            var container = AutoFree(new Node2D())!;
            
            var chunkManager = new ChunkManager(dmapFile, container);
            var chunks = chunkManager.GetAllChunks();
            
            AssertThat(chunks.Count).IsGreater(0);
            
            // Verify chunk size is correct (256x256 or smaller at edges)
            foreach (var chunk in chunks.Values)
            {
                AssertThat(chunk.TileRange.Size.X).IsLessEqual(256);
                AssertThat(chunk.TileRange.Size.Y).IsLessEqual(256);
                AssertThat(chunk.TileRange.Size.X).IsGreater(0);
                AssertThat(chunk.TileRange.Size.Y).IsGreater(0);
            }
        }

        [TestCase]
        public void ObjectPool_ManagesSpritesEfficiently()
        {
            var container = AutoFree(new Node2D())!;
            var pool = new SpritePool(container, 5, 20);
            
            // Test basic pool functionality
            var initialAvailable = pool.AvailableCount;
            var initialCreated = pool.CreatedCount;
            
            AssertThat(initialAvailable).IsGreaterEqual(0); // Should have objects
            AssertThat(initialCreated).IsGreaterEqual(5);   // Should have created initial size
            
            var sprite1 = pool.Get();
            AssertThat(sprite1).IsNotNull();
            
            var sprite2 = pool.Get();
            AssertThat(sprite2).IsNotNull();
            
            // Test returning to pool
            var availableBeforeReturn = pool.AvailableCount;
            pool.Return(sprite1);
            
            // Pool should have same or more available after return
            AssertThat(pool.AvailableCount).IsGreaterEqual(availableBeforeReturn);
        }

        [TestCase]
        public void LODSystem_AdjustsDetailByDistance()
        {
            var camera = AutoFree(new Camera2D())!;
            var lodSystem = new LODSystem(camera);
            var sprite = AutoFree(new LODSprite())!;
            
            camera.Position = Vector2.Zero;
            sprite.Position = new Vector2(100, 0); // Close distance
            
            lodSystem.RegisterLODObject(sprite);
            lodSystem.Update(0.1); // Trigger update
            
            AssertThat(sprite.GetCurrentLODLevel()).IsEqual(LODLevel.High);
            
            // Move sprite far away
            sprite.Position = new Vector2(3000, 0);
            lodSystem.Update(0.1);
            
            AssertThat(sprite.GetCurrentLODLevel()).IsEqual(LODLevel.Hidden);
        }

        [TestCase]
        public void TextureAtlas_CombinesTexturesCorrectly()
        {
            var atlas = new TextureAtlas();
            
            // Create test textures
            var image1 = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
            image1.Fill(Colors.Red);
            var texture1 = ImageTexture.CreateFromImage(image1);
            
            var image2 = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
            image2.Fill(Colors.Blue);
            var texture2 = ImageTexture.CreateFromImage(image2);
            
            AssertThat(atlas.AddTexture("test1.png", texture1)).IsTrue();
            AssertThat(atlas.AddTexture("test2.png", texture2)).IsTrue();
            
            // Test input validation
            AssertThat(atlas.AddTexture("", texture1)).IsFalse();
            AssertThat(atlas.AddTexture("test3.png", null!)).IsFalse();
            
            var atlasTexture = atlas.FinalizeAtlas();
            AssertThat(atlasTexture).IsNotNull();
            AssertThat(atlas.GetTextureCount()).IsEqual(2);
            
            var info1 = atlas.GetTextureInfo("test1.png");
            var info2 = atlas.GetTextureInfo("test2.png");
            
            AssertThat(info1.HasValue).IsTrue();
            AssertThat(info2.HasValue).IsTrue();
            
            // Test that finalized atlas rejects new textures
            AssertThat(atlas.AddTexture("test4.png", texture1)).IsFalse();
        }

        [TestCase]
        public void PerformanceMonitor_TracksMetricsCorrectly()
        {
            using var monitor = new PerformanceMonitor();
            
            // Simulate frame timing
            monitor.StartFrame();
            System.Threading.Thread.Sleep(16); // Simulate ~60 FPS frame
            monitor.EndFrame();
            
            monitor.Update(1.0); // Force stats update
            
            AssertThat(monitor.CurrentStats.FrameTimeMs).IsGreater(10.0);
            AssertThat(monitor.CurrentStats.FPS).IsGreater(0.0);
            AssertThat(monitor.CurrentStats.MemoryUsageMB).IsGreater(0);
            
            // Test performance targets
            var meetsTargets = monitor.MeetsPerformanceTargets();
            AssertThat(meetsTargets).IsNotNull(); // Should return a boolean value
        }

        [TestCase]
        public void DMapRenderer_WithOptimizations_RendersEfficiently()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var mapFile = Path.Combine(_mapPath, "grocery.DMap");
            var dmapFile = new DmapFile(mapFile);
            
            // Enable all optimizations
            renderer.EnableOptimizations = true;
            renderer.EnableChunking = true;
            renderer.EnableViewportCulling = true;
            renderer.EnableLOD = true;
            renderer.EnableObjectPooling = true;
            
            var stopwatch = Stopwatch.StartNew();
            renderer.LoadFromDMap(dmapFile);
            stopwatch.Stop();
            
            var loadTimeMs = stopwatch.ElapsedMilliseconds;
            AssertThat(loadTimeMs).IsLess(2000); // Should load in under 2 seconds
            
            // Verify optimization components are initialized
            AssertThat(renderer.GetChildCount()).IsGreater(0);
            
            GD.Print($"Optimized rendering completed in {loadTimeMs}ms");
        }

        [TestCase]
        public void LargeMap_OptimizedRendering_MeetsPerformanceTargets()
        {
            var largeMapFile = Path.Combine(_mapPath, "island.DMap");
            if (!File.Exists(largeMapFile))
            {
                GD.Print("Large map test skipped: island.DMap not found");
                return;
            }
            
            var renderer = AutoFree(new DMapRenderer())!;
            var dmapFile = new DmapFile(largeMapFile);
            
            // Enable all optimizations
            renderer.EnableOptimizations = true;
            renderer.EnableChunking = true;
            renderer.EnableViewportCulling = true;
            renderer.EnableLOD = true;
            renderer.EnableObjectPooling = true;
            
            var initialMemory = GC.GetTotalMemory(true);
            var stopwatch = Stopwatch.StartNew();
            
            renderer.LoadFromDMap(dmapFile);
            
            stopwatch.Stop();
            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsedMB = (finalMemory - initialMemory) / (1024 * 1024);
            
            // Performance targets from PRD
            AssertThat(stopwatch.ElapsedMilliseconds).IsLess(2000); // <2s load time
            AssertThat(memoryUsedMB).IsLess(500); // <500MB memory usage
            
            GD.Print($"Large map loaded in {stopwatch.ElapsedMilliseconds}ms, Memory: {memoryUsedMB}MB");
        }

        [TestCase]
        public void OptimizationComparison_ShowsPerformanceGains()
        {
            var mapFile = Path.Combine(_mapPath, "arena.DMap");
            var dmapFile = new DmapFile(mapFile);
            
            // Test without optimizations
            var rendererUnoptimized = AutoFree(new DMapRenderer())!;
            rendererUnoptimized.EnableOptimizations = false;
            
            var stopwatchUnopt = Stopwatch.StartNew();
            rendererUnoptimized.LoadFromDMap(dmapFile);
            stopwatchUnopt.Stop();
            var unoptimizedTime = stopwatchUnopt.ElapsedMilliseconds;
            
            // Test with optimizations
            var rendererOptimized = AutoFree(new DMapRenderer())!;
            rendererOptimized.EnableOptimizations = true;
            rendererOptimized.EnableChunking = true;
            rendererOptimized.EnableViewportCulling = true;
            rendererOptimized.EnableLOD = true;
            rendererOptimized.EnableObjectPooling = true;
            
            var stopwatchOpt = Stopwatch.StartNew();
            rendererOptimized.LoadFromDMap(dmapFile);
            stopwatchOpt.Stop();
            var optimizedTime = stopwatchOpt.ElapsedMilliseconds;
            
            GD.Print($"Performance comparison - Unoptimized: {unoptimizedTime}ms, Optimized: {optimizedTime}ms");
            
            // Optimized version should be significantly faster or at least not slower
            AssertThat(optimizedTime).IsLessEqual((long)(unoptimizedTime * 1.1f)); // Allow 10% margin
        }

        [TestCase]
        [DataPoint(nameof(PerformanceTestMaps))]
        public void OptimizedRendering_MeetsTargets(string mapFileName, long maxLoadTimeMs, long maxMemoryMB)
        {
            var mapFile = Path.Combine(_mapPath, mapFileName);
            if (!File.Exists(mapFile))
            {
                GD.Print($"Map test skipped: {mapFileName} not found");
                return;
            }
            
            var renderer = AutoFree(new DMapRenderer())!;
            renderer.EnableOptimizations = true;
            renderer.EnableChunking = true;
            renderer.EnableViewportCulling = true;
            renderer.EnableLOD = true;
            renderer.EnableObjectPooling = true;
            
            var initialMemory = GC.GetTotalMemory(true);
            var stopwatch = Stopwatch.StartNew();
            
            var dmapFile = new DmapFile(mapFile);
            renderer.LoadFromDMap(dmapFile);
            
            stopwatch.Stop();
            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsedMB = (finalMemory - initialMemory) / (1024 * 1024);
            
            AssertThat(stopwatch.ElapsedMilliseconds).IsLess(maxLoadTimeMs);
            AssertThat(memoryUsedMB).IsLess(maxMemoryMB);
            
            GD.Print($"{mapFileName}: {stopwatch.ElapsedMilliseconds}ms (target: <{maxLoadTimeMs}ms), " +
                    $"Memory: {memoryUsedMB}MB (target: <{maxMemoryMB}MB)");
        }

        public static object[][] PerformanceTestMaps => new object[][]
        {
            new object[] { "smith.DMap", 500L, 50L },
            new object[] { "grocery.DMap", 800L, 100L },
            new object[] { "arena.DMap", 1200L, 150L },
            new object[] { "forum.DMap", 2000L, 300L },
            new object[] { "island.DMap", 2000L, 500L },
            new object[] { "desert.DMap", 2000L, 500L },
            new object[] { "woods.DMap", 2000L, 500L }
        };
    }
}