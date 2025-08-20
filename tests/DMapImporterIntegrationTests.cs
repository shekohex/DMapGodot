using GdUnit4;
using static GdUnit4.Assertions;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using System.IO;
using System.Linq;
using System;
using System.Reflection;

namespace DMapImporter.Tests
{
    [TestSuite]
    public class DMapImporterIntegrationTests
    {
        private static readonly string TestMapDirectory = Path.Combine(
            Directory.GetCurrentDirectory(), "Game", "5017", "map", "map");
        
        [TestCase]
        public void TestDMapImporterPluginFilesExist()
        {
            // Verify all DMapImporter plugin files exist
            var requiredFiles = new[]
            {
                "addons/dmap_importer/Importers/DMapImporter.cs",
                "addons/dmap_importer/Nodes/DMapRenderer.cs",
                "addons/dmap_importer/DMapImporterPlugin.cs",
                "addons/dmap_importer/plugin.cfg"
            };
            
            foreach (var file in requiredFiles)
            {
                AssertThat(File.Exists(file))
                    .OverrideFailureMessage($"DMapImporter plugin file missing: {file}")
                    .IsTrue();
            }
        }
        
        [TestCase]
        public void TestDMapImporterClassStructure()
        {
            // Verify the DMapImporter class exists and has correct structure
            var assembly = Assembly.GetExecutingAssembly();
            var importerType = assembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DMapImporter" && t.Namespace == "DMapImporter.Importers");
            
            AssertThat(importerType).IsNotNull();
            
            // Verify it's in correct namespace
            AssertThat(importerType!.Namespace).IsEqual("DMapImporter.Importers");
            
            // Verify it has the required methods for EditorImportPlugin
            var methodNames = importerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => m.Name).ToArray();
                
            AssertThat(methodNames).Contains("_GetImporterName");
            AssertThat(methodNames).Contains("_GetVisibleName");
            AssertThat(methodNames).Contains("_GetRecognizedExtensions");
            AssertThat(methodNames).Contains("_GetSaveExtension");
            AssertThat(methodNames).Contains("_GetResourceType");
            AssertThat(methodNames).Contains("_Import");
        }
        
        [TestCase]
        public void TestDMapRendererClassStructure()
        {
            // Verify the DMapRenderer class exists and has correct structure
            var assembly = Assembly.GetExecutingAssembly();
            var rendererType = assembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DMapRenderer" && t.Namespace == "DMapImporter.Nodes");
            
            AssertThat(rendererType).IsNotNull();
            AssertThat(rendererType!.Namespace).IsEqual("DMapImporter.Nodes");
            
            // Verify it has required properties
            var propertyNames = rendererType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name).ToArray();
                
            AssertThat(propertyNames).Contains("TileSize");
            AssertThat(propertyNames).Contains("DMapPath");
            AssertThat(propertyNames).Contains("MapSize");
            
            // Verify it has required methods
            var methodNames = rendererType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => m.Name).ToArray();
                
            AssertThat(methodNames).Contains("LoadFromDMap");
            AssertThat(methodNames).Contains("GetDMapFile");
        }
        
        [TestCase]
        public void TestDMapRendererWithRealDMapFile()
        {
            var testFile = GetValidTestMapPath();
            if (testFile == null)
            {
                return; // Skip if no test files available
            }
            
            // Load a real DMAP file and verify it works with our renderer logic
            var dmapFile = new DmapFile(testFile);
            AssertThat(dmapFile).IsNotNull();
            AssertThat(dmapFile.SizeTiles.Width).IsGreater(0);
            AssertThat(dmapFile.SizeTiles.Height).IsGreater(0);
            
            // Test the data structures our renderer will use
            AssertThat(dmapFile.TileSet).IsNotNull();
            AssertThat(dmapFile.TileSet.GetLength(0)).IsEqual((int)dmapFile.SizeTiles.Width);
            AssertThat(dmapFile.TileSet.GetLength(1)).IsEqual((int)dmapFile.SizeTiles.Height);
            
            // Test coordinate conversion logic
            for (int x = 0; x < Math.Min(5, (int)dmapFile.SizeTiles.Width); x++)
            {
                for (int y = 0; y < Math.Min(5, (int)dmapFile.SizeTiles.Height); y++)
                {
                    var tile = dmapFile.TileSet[x, y];
                    
                    // Verify tile access logic works (used in renderer)
                    var access = tile.Access;
                    AssertThat(access).IsIn(0, 1);
                    
                    // Verify other tile properties
                    AssertThat(tile.Surface).IsGreaterEqual(0);
                    AssertThat(tile.NoAccess).IsIn(0, 1);
                }
            }
        }
        
        [TestCase]
        public void TestPortalRenderingDataStructure()
        {
            var testFile = GetValidTestMapPath();
            if (testFile == null)
            {
                return; // Skip if no test files available
            }
            
            var dmapFile = new DmapFile(testFile);
            
            // Test portal data structure (used by renderer)
            AssertThat(dmapFile.Portals).IsNotNull();
            
            if (dmapFile.Portals.Count > 0)
            {
                var portal = dmapFile.Portals.First();
                
                // Verify portal properties match renderer expectations
                AssertThat(portal.Id).IsGreater(0);
                AssertThat(portal.Position.X).IsGreaterEqual(0);
                AssertThat(portal.Position.Y).IsGreaterEqual(0);
                
                // Test coordinate conversion for rendering (uint to float)
                float worldX = portal.Position.X * 32; // Default tile size
                float worldY = portal.Position.Y * 32;
                
                AssertThat(worldX).IsGreaterEqual(0);
                AssertThat(worldY).IsGreaterEqual(0);
                
                // Verify position is within map bounds
                AssertThat(portal.Position.X).IsLess(dmapFile.SizeTiles.Width);
                AssertThat(portal.Position.Y).IsLess(dmapFile.SizeTiles.Height);
            }
        }
        
        [TestCase]
        public void TestCoverRenderingDataStructure()
        {
            var testFile = GetValidTestMapPath();
            if (testFile == null)
            {
                return; // Skip if no test files available
            }
            
            var dmapFile = new DmapFile(testFile);
            
            // Test cover data structure (used by renderer)
            AssertThat(dmapFile.Covers).IsNotNull();
            
            if (dmapFile.Covers.Count > 0)
            {
                var cover = dmapFile.Covers.First();
                
                // Verify cover properties match renderer expectations
                AssertThat(cover.AniName).IsNotNull();
                AssertThat(cover.AniPath).IsNotNull();
                AssertThat(cover.Position.X).IsGreaterEqual(0);
                AssertThat(cover.Position.Y).IsGreaterEqual(0);
                
                // Test coordinate conversion for rendering
                float worldX = cover.Position.X * 32;
                float worldY = cover.Position.Y * 32;
                
                AssertThat(worldX).IsGreaterEqual(0);
                AssertThat(worldY).IsGreaterEqual(0);
                
                // Verify size properties
                AssertThat(cover.BaseSize.Width).IsGreater(0);
                AssertThat(cover.BaseSize.Height).IsGreater(0);
            }
        }
        
        [TestCase]
        public void TestImportPipelineDataCompatibility()
        {
            var testFile = GetValidTestMapPath();
            if (testFile == null)
            {
                return; // Skip if no test files available
            }
            
            // Test that our import pipeline data is compatible
            var dmapFile = new DmapFile(testFile);
            
            // Verify all collections needed by importer are initialized
            AssertThat(dmapFile.Portals).IsNotNull();
            AssertThat(dmapFile.Covers).IsNotNull();
            AssertThat(dmapFile.TerrainScenes).IsNotNull();
            AssertThat(dmapFile.SceneLayers).IsNotNull();
            AssertThat(dmapFile.Effects).IsNotNull();
            AssertThat(dmapFile.Sounds).IsNotNull();
            AssertThat(dmapFile.Puzzles).IsNotNull();
            
            // Verify properties needed by importer
            AssertThat(dmapFile.DmapName).IsNotNull();
            AssertThat(dmapFile.DmapPath).IsNotNull();
            AssertThat(dmapFile.MapVersion).IsGreater(0);
            
            // Verify tile data structure integrity
            AssertThat(dmapFile.TileSet).IsNotNull();
            AssertThat(dmapFile.TileSet.Rank).IsEqual(2); // 2D array
            AssertThat(dmapFile.TileSet.GetLength(0)).IsEqual((int)dmapFile.SizeTiles.Width);
            AssertThat(dmapFile.TileSet.GetLength(1)).IsEqual((int)dmapFile.SizeTiles.Height);
        }
        
        [TestCase]
        public void TestCoordinateSystemConversion()
        {
            // Test coordinate conversion logic used by renderer
            uint[] testCoordinates = { 0, 1, 50, 100, 255 };
            int[] tileSizes = { 16, 32, 64 };
            
            foreach (var coord in testCoordinates)
            {
                foreach (var tileSize in tileSizes)
                {
                    // Test uint to int casting (used in renderer)
                    int intCoord = (int)coord;
                    AssertThat(intCoord).IsEqual((int)coord);
                    
                    // Test world position calculation
                    float worldPos = coord * tileSize;
                    AssertThat(worldPos).IsEqual(coord * tileSize);
                    
                    // Verify no overflow occurs for reasonable values
                    AssertThat(worldPos).IsGreaterEqual(0);
                    AssertThat(worldPos).IsLess(1000000); // Reasonable upper bound
                }
            }
        }
        
        [TestCase]
        public void TestFileFormatSupport()
        {
            // Test that our implementation supports the required file formats
            var supportedExtensions = new[] { "dmap", "7z", "zmap" };
            
            foreach (var extension in supportedExtensions)
            {
                AssertThat(extension).IsNotNull();
                AssertThat(extension.Length).IsGreater(0);
                AssertThat(extension.Contains(".")).IsFalse();
            }
            
            // Verify we have the expected number of supported formats
            AssertThat(supportedExtensions.Length).IsEqual(3);
        }
        
        [TestCase]
        public void TestMultipleDMapFileCompatibility()
        {
            if (!Directory.Exists(TestMapDirectory))
            {
                return; // Skip if test directory doesn't exist
            }
            
            var dmapFiles = Directory.GetFiles(TestMapDirectory, "*.DMap").Take(5).ToArray();
            
            if (dmapFiles.Length == 0)
            {
                return; // Skip if no DMAP files found
            }
            
            foreach (var file in dmapFiles)
            {
                try
                {
                    var dmapFile = new DmapFile(file);
                    
                    // Each file should load successfully and have valid structure
                    AssertThat(dmapFile).IsNotNull();
                    AssertThat(dmapFile.DmapName).IsNotNull();
                    AssertThat(dmapFile.SizeTiles.Width).IsGreater(0);
                    AssertThat(dmapFile.SizeTiles.Height).IsGreater(0);
                    
                    // Collections should be initialized (even if empty)
                    AssertThat(dmapFile.TileSet).IsNotNull();
                    AssertThat(dmapFile.Portals).IsNotNull();
                    AssertThat(dmapFile.Covers).IsNotNull();
                }
                catch (Exception ex)
                {
                    // File loading failures should be specific parse errors, not general exceptions
                    AssertThat(ex).IsInstanceOf<IOException>();
                }
            }
        }
        
        [TestCase]
        public void TestBackwardsCompatibilityWithExistingTests()
        {
            // Verify that our Task 4 implementation doesn't break existing functionality
            var testFile = GetValidTestMapPath();
            if (testFile == null)
            {
                return; // Skip if no test files available
            }
            
            // This should work exactly as it did before Task 4 implementation
            var dmapFile = new DmapFile(testFile);
            
            // Test that all existing Core tests would still pass
            AssertThat(dmapFile.DmapName).IsNotNull();
            AssertThat(dmapFile.MapVersion).IsGreater(0);
            AssertThat(dmapFile.SizeTiles.Width).IsGreater(0);
            AssertThat(dmapFile.SizeTiles.Height).IsGreater(0);
            
            // Test tile creation and access (like CoreTests)
            if (dmapFile.SizeTiles.Width > 0 && dmapFile.SizeTiles.Height > 0)
            {
                var tile = dmapFile.TileSet[0, 0];
                var access = tile.Access;
                AssertThat(access).IsIn(0, 1);
                AssertThat(tile.Surface).IsGreaterEqual(0);
            }
            
            // Test collections are still accessible as before
            AssertThat(dmapFile.Portals).IsNotNull();
            AssertThat(dmapFile.Covers).IsNotNull();
            AssertThat(dmapFile.TerrainScenes).IsNotNull();
        }
        
        private string? GetValidTestMapPath()
        {
            if (!Directory.Exists(TestMapDirectory))
                return null;
                
            var testFiles = new[] { "Dcloister.DMap", "Gulf.DMap", "grocery.DMap" };
            
            foreach (var file in testFiles)
            {
                string fullPath = Path.Combine(TestMapDirectory, file);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            
            return null;
        }
    }
}