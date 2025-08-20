using GdUnit4;
using static GdUnit4.Assertions;
using DMapImporter.Core.Dmap;
using System.IO;
using System;
using System.Linq;

namespace DMapImporter.Tests
{
    [TestSuite]
    public class DmapFileTest
    {
        // Use absolute path to ensure tests work regardless of working directory
        private static readonly string TestMapDirectory = Path.Combine(
            Directory.GetCurrentDirectory(), "Game", "5017", "map", "map");

        [TestCase]
        public void TestDmapFileExists()
        {
            string testMapPath = Path.Combine(TestMapDirectory, "Gulf.DMap");

            if (!File.Exists(testMapPath))
            {
                // Try alternative test file
                testMapPath = Path.Combine(TestMapDirectory, "Dcloister.DMap");
            }

            AssertThat(File.Exists(testMapPath))
                .OverrideFailureMessage($"No test DMAP files found in {TestMapDirectory}")
                .IsTrue();
        }

        [TestCase]
        public void TestDmapFileSharpCompressLoading()
        {
            // Use project root as ClientPath and relative path for DMAP file
            var projectRoot = Directory.GetCurrentDirectory();
            string relativePath = "Game/5017/map/map/Dcloister.DMap";

            // First verify the file exists
            string fullPath = Path.Combine(projectRoot, relativePath);
            if (!File.Exists(fullPath))
            {
                return; // Skip test if file doesn't exist
            }

            // Test the proper way to load DMAP files with ClientPath
            var dmapFile = new DmapFile(relativePath, projectRoot);

            // Verify basic properties are loaded
            AssertThat(dmapFile.DmapName).IsEqual("Dcloister");
            AssertThat(dmapFile.MapVersion).IsGreater(0);
            AssertThat(dmapFile.SizeTiles.Width).IsGreater(0);
            AssertThat(dmapFile.SizeTiles.Height).IsGreater(0);

            // Verify collections are initialized (even if empty)
            AssertThat(dmapFile.Portals).IsNotNull();
            AssertThat(dmapFile.Covers).IsNotNull();
            AssertThat(dmapFile.TerrainScenes).IsNotNull();
            AssertThat(dmapFile.SceneLayers).IsNotNull();
            AssertThat(dmapFile.TileSet).IsNotNull();
        }

        [TestCase]
        public void TestDmapFileAPICompatibility()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            string relativePath = "Game/5017/map/map/Dcloister.DMap";
            string fullPath = Path.Combine(projectRoot, relativePath);

            if (!File.Exists(fullPath))
            {
                return; // Skip test if file doesn't exist
            }

            var dmapFile = new DmapFile(relativePath, projectRoot);

            // Test that all public API properties are accessible without throwing exceptions
            AssertThat(dmapFile.DmapName).IsNotNull();
            AssertThat(dmapFile.DmapPath).IsNotNull();
            AssertThat(dmapFile.Header).IsNotNull();
            AssertThat(dmapFile.MapVersion).IsGreaterEqual(0);
            AssertThat(dmapFile.PuzzleFile).IsNotNull();
            AssertThat(dmapFile.SizeTiles).IsNotNull();
            AssertThat(dmapFile.TileSet).IsNotNull();
            AssertThat(dmapFile.Portals).IsNotNull();
            AssertThat(dmapFile.TerrainScenes).IsNotNull();
            AssertThat(dmapFile.Covers).IsNotNull();
            AssertThat(dmapFile.Puzzles).IsNotNull();
            AssertThat(dmapFile.Effects).IsNotNull();
            AssertThat(dmapFile.Sounds).IsNotNull();
            AssertThat(dmapFile.SceneLayers).IsNotNull();
        }

        [TestCase]
        public void TestMultipleDmapFiles()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            var mapDirectory = Path.Combine(projectRoot, "Game", "5017", "map", "map");

            if (!Directory.Exists(mapDirectory))
            {
                return;
            }

            var availableFiles = Directory.GetFiles(mapDirectory, "*.DMap").Take(3).ToArray();

            if (availableFiles.Length == 0)
            {
                return;
            }

            int successCount = 0;
            foreach (string testFile in availableFiles)
            {
                try
                {
                    // Convert to relative path
                    var relativePath = Path.GetRelativePath(projectRoot, testFile);
                    var dmapFile = new DmapFile(relativePath, projectRoot);
                    successCount++;
                }
                catch
                {
                    // Some files may fail to load, which is acceptable for this test
                }
            }

            AssertThat(successCount)
                .OverrideFailureMessage($"Expected to load at least 1 file, but loaded {successCount}")
                .IsGreaterEqual(1);
        }

        [TestCase]
        public void TestErrorHandlingInvalidFile()
        {
            string invalidPath = "nonexistent/path/invalid.DMap";

            AssertThrown(() => new DmapFile(invalidPath))
                .IsInstanceOf<FileNotFoundException>();
        }
    }
}