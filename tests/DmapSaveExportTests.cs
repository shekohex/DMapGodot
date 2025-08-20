using GdUnit4;
using static GdUnit4.Assertions;
using static GdUnit4.Utils;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using System.IO;
using System;
using System.Linq;

namespace DMapImporter.Tests
{
    [TestSuite]
    [RequireGodotRuntime]
    public class DmapSaveExportTests
    {
        private static readonly string TestMapDirectory = Path.Combine(
            Directory.GetCurrentDirectory(), "Game", "5017", "map", "map");

        private static readonly string TempTestDirectory = Path.Combine(
            Path.GetTempPath(), "DMapGodot_Tests", Guid.NewGuid().ToString());

        [BeforeTest]
        public void SetUp()
        {
            // Create temporary test directory
            if (!Directory.Exists(TempTestDirectory))
                Directory.CreateDirectory(TempTestDirectory);
        }

        [AfterTest]
        public void TearDown()
        {
            // Clean up temporary test files
            try
            {
                if (Directory.Exists(TempTestDirectory))
                    Directory.Delete(TempTestDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }

        [TestCase]
        public void TestSaveToStream()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            string relativePath = "Game/5017/map/map/Dcloister.DMap";
            string fullPath = Path.Combine(projectRoot, relativePath);

            if (!File.Exists(fullPath))
            {
                return; // Skip test if file doesn't exist
            }

            var originalDmap = new DmapFile(relativePath, projectRoot);

            // Save to memory stream
            using var memoryStream = new MemoryStream();
            long dataLength;
            long streamPosition;

            originalDmap.Save(memoryStream);
            dataLength = memoryStream.Length;
            streamPosition = memoryStream.Position;

            // Verify stream has data
            AssertThat(dataLength).IsGreater(0);
            AssertThat(streamPosition).IsGreater(0);
        }

        [TestCase]
        public void TestExportToFile()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            string relativePath = "Game/5017/map/map/Dcloister.DMap";
            string fullPath = Path.Combine(projectRoot, relativePath);

            if (!File.Exists(fullPath))
            {
                return; // Skip test if file doesn't exist
            }

            var originalDmap = new DmapFile(relativePath, projectRoot);
            string exportPath = Path.Combine(TempTestDirectory, "exported_test.dmap");

            // Export to file
            originalDmap.Export(exportPath);

            // Verify file was created and has content
            AssertThat(File.Exists(exportPath)).IsTrue();
            AssertThat(new FileInfo(exportPath).Length).IsGreater(0);
        }

        [TestCase]
        public void TestRoundTripSaveLoad()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            string relativePath = "Game/5017/map/map/Dcloister.DMap";
            string fullPath = Path.Combine(projectRoot, relativePath);

            if (!File.Exists(fullPath))
            {
                return; // Skip test if file doesn't exist
            }

            // Load original
            var originalDmap = new DmapFile(relativePath, projectRoot);

            // Save to temp file
            string tempPath = Path.Combine(TempTestDirectory, "roundtrip_test.dmap");
            originalDmap.Export(tempPath);

            // Load the saved file
            var reloadedDmap = new DmapFile(tempPath);

            // Compare key properties (DmapName will be different since it's based on file name)
            // AssertThat(reloadedDmap.DmapName).IsEqual(originalDmap.DmapName);
            AssertThat(reloadedDmap.MapVersion).IsEqual(originalDmap.MapVersion);
            AssertThat(reloadedDmap.SizeTiles.Width).IsEqual(originalDmap.SizeTiles.Width);
            AssertThat(reloadedDmap.SizeTiles.Height).IsEqual(originalDmap.SizeTiles.Height);
            AssertThat(reloadedDmap.PuzzleFile).IsEqual(originalDmap.PuzzleFile);

            // Compare collections
            AssertThat(reloadedDmap.Portals.Count).IsEqual(originalDmap.Portals.Count);
            AssertThat(reloadedDmap.Covers.Count).IsEqual(originalDmap.Covers.Count);
            AssertThat(reloadedDmap.TerrainScenes.Count).IsEqual(originalDmap.TerrainScenes.Count);
            AssertThat(reloadedDmap.Effects.Count).IsEqual(originalDmap.Effects.Count);
            AssertThat(reloadedDmap.Sounds.Count).IsEqual(originalDmap.Sounds.Count);
            AssertThat(reloadedDmap.SceneLayers.Count).IsEqual(originalDmap.SceneLayers.Count);
            AssertThat(reloadedDmap.Puzzles.Count).IsEqual(originalDmap.Puzzles.Count);
        }

        [TestCase]
        public void TestTileDataIntegrity()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            string relativePath = "Game/5017/map/map/Dcloister.DMap";
            string fullPath = Path.Combine(projectRoot, relativePath);

            if (!File.Exists(fullPath))
            {
                return; // Skip test if file doesn't exist
            }

            var originalDmap = new DmapFile(relativePath, projectRoot);
            string tempPath = Path.Combine(TempTestDirectory, "tile_integrity_test.dmap");

            originalDmap.Export(tempPath);
            var reloadedDmap = new DmapFile(tempPath);

            // Compare tile data in detail
            AssertThat(reloadedDmap.TileSet.GetLength(0)).IsEqual(originalDmap.TileSet.GetLength(0));
            AssertThat(reloadedDmap.TileSet.GetLength(1)).IsEqual(originalDmap.TileSet.GetLength(1));

            // Sample a few tiles to verify data integrity
            int maxSamples = Math.Min(10, (int)(originalDmap.SizeTiles.Width * originalDmap.SizeTiles.Height));
            var random = new Random(42); // Use fixed seed for reproducible tests

            for (int i = 0; i < maxSamples; i++)
            {
                int x = random.Next((int)originalDmap.SizeTiles.Width);
                int y = random.Next((int)originalDmap.SizeTiles.Height);

                var originalTile = originalDmap.TileSet[x, y];
                var reloadedTile = reloadedDmap.TileSet[x, y];

                AssertThat(reloadedTile.NoAccess).IsEqual(originalTile.NoAccess);
                AssertThat(reloadedTile.Surface).IsEqual(originalTile.Surface);
                AssertThat(reloadedTile.Height).IsEqual(originalTile.Height);
            }
        }

        [TestCase]
        public void TestPortalDataIntegrity()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            string relativePath = "Game/5017/map/map/Dcloister.DMap";
            string fullPath = Path.Combine(projectRoot, relativePath);

            if (!File.Exists(fullPath))
            {
                return; // Skip test if file doesn't exist
            }

            var originalDmap = new DmapFile(relativePath, projectRoot);

            if (originalDmap.Portals.Count == 0)
            {
                return; // Skip if no portals to test
            }

            string tempPath = Path.Combine(TempTestDirectory, "portal_integrity_test.dmap");
            originalDmap.Export(tempPath);
            var reloadedDmap = new DmapFile(tempPath);

            for (int i = 0; i < originalDmap.Portals.Count; i++)
            {
                var originalPortal = originalDmap.Portals[i];
                var reloadedPortal = reloadedDmap.Portals[i];

                AssertThat(reloadedPortal.Position.X).IsEqual(originalPortal.Position.X);
                AssertThat(reloadedPortal.Position.Y).IsEqual(originalPortal.Position.Y);
                AssertThat(reloadedPortal.Id).IsEqual(originalPortal.Id);
            }
        }

        [TestCase]
        public void TestDataIntegrityValidation()
        {
            // Create a minimal valid DMAP for testing validation
            var testDmap = new DmapFile
            {
                Header = new byte[] { 0x65, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                MapVersion = 0x365,
                PuzzleFile = "",
                SizeTiles = new Size(2, 2),
                TileSet = new Tile[2, 2]
            };

            // Initialize tiles
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    testDmap.TileSet[x, y] = new Tile(0, 1, 0);
                }
            }

            string tempPath = Path.Combine(TempTestDirectory, "validation_test.dmap");

            // This should not throw
            testDmap.Export(tempPath);
            AssertThat(File.Exists(tempPath)).IsTrue();
        }

        [TestCase]
        public void TestValidationWithNullTileSet()
        {
            var testDmap = new DmapFile
            {
                Header = new byte[] { 0x65, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                MapVersion = 0x365,
                PuzzleFile = "",
                SizeTiles = new Size(2, 2),
                TileSet = null!
            };

            string tempPath = Path.Combine(TempTestDirectory, "null_tileset_test.dmap");

            AssertThrown(() => testDmap.Export(tempPath))
                .IsInstanceOf<InvalidOperationException>()
                .HasMessage("TileSet cannot be null");
        }

        [TestCase]
        public void TestValidationWithMismatchedDimensions()
        {
            var testDmap = new DmapFile
            {
                Header = new byte[] { 0x65, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                MapVersion = 0x365,
                PuzzleFile = "",
                SizeTiles = new Size(3, 3),
                TileSet = new Tile[2, 2] // Dimensions don't match SizeTiles
            };

            string tempPath = Path.Combine(TempTestDirectory, "mismatched_dimensions_test.dmap");

            AssertThrown(() => testDmap.Export(tempPath))
                .IsInstanceOf<InvalidOperationException>()
                .HasMessage("TileSet dimensions (2x2) don't match SizeTiles (3x3)");
        }

        [TestCase]
        public void TestSaveWithEmptyPath()
        {
            var testDmap = new DmapFile();

            AssertThrown(() => testDmap.Save())
                .IsInstanceOf<InvalidOperationException>()
                .HasMessage("Cannot save: DmapPath is not set");
        }

        [TestCase]
        public void TestExportWithEmptyPath()
        {
            var testDmap = new DmapFile();

            AssertThrown(() => testDmap.Export(""))
                .IsInstanceOf<ArgumentException>();
        }

        [TestCase]
        public void TestCompressionNotSupported()
        {
            var testDmap = new DmapFile
            {
                Header = new byte[] { 0x65, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                MapVersion = 0x365,
                PuzzleFile = "",
                SizeTiles = new Size(1, 1),
                TileSet = new Tile[1, 1]
            };

            testDmap.TileSet[0, 0] = new Tile(0, 1, 0);

            string tempPath = Path.Combine(TempTestDirectory, "compression_test.dmap");

            AssertThrown(() => testDmap.Export(tempPath, compress: true))
                .IsInstanceOf<NotImplementedException>()
                .HasMessage("7z compression not yet supported - use uncompressed format");
        }

        [TestCase]
        public void TestInPlaceSave()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            string relativePath = "Game/5017/map/map/Dcloister.DMap";
            string fullPath = Path.Combine(projectRoot, relativePath);

            if (!File.Exists(fullPath))
            {
                return; // Skip test if file doesn't exist
            }

            // Copy original to temp location for in-place save test
            string tempOriginal = Path.Combine(TempTestDirectory, "inplace_original.dmap");
            File.Copy(fullPath, tempOriginal);

            var testDmap = new DmapFile(tempOriginal);
            long originalSize = new FileInfo(tempOriginal).Length;

            // Perform in-place save
            testDmap.Save();

            // Verify file still exists and has reasonable size
            AssertThat(File.Exists(tempOriginal)).IsTrue();
            long newSize = new FileInfo(tempOriginal).Length;
            AssertThat(newSize).IsGreater(0);

            // Size should be similar (allowing for minor differences in implementation)
            double sizeDifference = Math.Abs((double)(newSize - originalSize) / originalSize);
            AssertThat(sizeDifference).IsLessEqual(0.1); // Allow 10% difference
        }
    }
}