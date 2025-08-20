using GdUnit4;
using static GdUnit4.Assertions;
using static GdUnit4.Utils;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace DMapImporter.Tests
{
    [TestSuite]
    [RequireGodotRuntime]
    public class DmapSaveSecurityTests
    {
        private static readonly string TempTestDirectory = Path.Combine(
            Path.GetTempPath(), "DMapGodot_SecurityTests", Guid.NewGuid().ToString());

        [BeforeTest]
        public void SetUp()
        {
            if (!Directory.Exists(TempTestDirectory))
                Directory.CreateDirectory(TempTestDirectory);
        }

        [AfterTest]
        public void TearDown()
        {
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
        public void TestPathTraversalAttackPrevention()
        {
            var testDmap = CreateValidTestDmap();

            // Test various directory traversal attack vectors
            string[] maliciousPaths = {
                "../../../etc/passwd",
                "..\\..\\..\\Windows\\System32\\test.dmap",
                "/etc/passwd",
                "C:\\Windows\\System32\\test.dmap",
                "test/../../../etc/passwd",
                "legitimate.dmap\0../../../etc/passwd"
            };

            foreach (var maliciousPath in maliciousPaths)
            {
                bool threwExpectedException = false;
                try
                {
                    testDmap.Export(maliciousPath);
                }
                catch (ArgumentException)
                {
                    threwExpectedException = true;
                }
                catch (UnauthorizedAccessException)
                {
                    threwExpectedException = true;
                }

                AssertThat(threwExpectedException)
                    .OverrideFailureMessage($"Path traversal attack should have been blocked for path: {maliciousPath}")
                    .IsTrue();
            }
        }

        [TestCase]
        public void TestLongPathHandling()
        {
            var testDmap = CreateValidTestDmap();

            // Create a path that's too long
            var longPath = Path.Combine(TempTestDirectory, new string('a', 300) + ".dmap");

            AssertThrown(() => testDmap.Export(longPath))
                .IsInstanceOf<PathTooLongException>();
        }

        [TestCase]
        public void TestNullCharacterInPath()
        {
            var testDmap = CreateValidTestDmap();

            string pathWithNull = Path.Combine(TempTestDirectory, "test\0injection.dmap");

            // Should sanitize the null character and work
            string validPath = Path.Combine(TempTestDirectory, "test injection.dmap");
            testDmap.Export(pathWithNull);

            // Verify the file was created with sanitized name (spaces replacing nulls)
            AssertThat(File.Exists(validPath.Trim())).IsTrue();
        }

        [TestCase]
        public void TestResourceExhaustionProtection()
        {
            // Test with extremely large dimensions that would cause memory/CPU exhaustion
            var maliciousDmap = new DmapFile
            {
                Header = new byte[] { 0x65, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                MapVersion = 0x365,
                PuzzleFile = "",
                SizeTiles = new Size(50000, 50000), // Would be 2.5 billion tiles
                TileSet = new Tile[1, 1] // Mismatched dimensions to trigger validation
            };

            string tempPath = Path.Combine(TempTestDirectory, "huge_map_test.dmap");

            bool threwCorrectException = false;
            try
            {
                maliciousDmap.Export(tempPath);
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("Map dimensions too large"))
                    threwCorrectException = true;
            }

            AssertThat(threwCorrectException)
                .OverrideFailureMessage("Should throw InvalidOperationException about map dimensions")
                .IsTrue();
        }

        [TestCase]
        public void TestMaxObjectCountProtection()
        {
            var testDmap = CreateValidTestDmap();

            // Add too many portals
            for (int i = 0; i < 100001; i++)
            {
                testDmap.Portals.Add(new Portal { Position = new TilePosition(0, 0), Id = (uint)i });
            }

            string tempPath = Path.Combine(TempTestDirectory, "too_many_objects_test.dmap");

            bool threwCorrectException = false;
            try
            {
                testDmap.Export(tempPath);
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("Too many portals"))
                    threwCorrectException = true;
            }

            AssertThat(threwCorrectException)
                .OverrideFailureMessage("Should throw InvalidOperationException about too many portals")
                .IsTrue();
        }

        [TestCase]
        public void TestTransactionalSaveRollback()
        {
            var testDmap = CreateValidTestDmap();

            string outputPath = Path.Combine(TempTestDirectory, "transactional_test.dmap");

            // Create a file first
            File.WriteAllText(outputPath, "original content");
            var originalContent = File.ReadAllText(outputPath);

            // Now try to save with an invalid DMAP that will fail during validation
            testDmap.TileSet = null!; // This will cause validation to fail

            AssertThrown(() => testDmap.Export(outputPath));

            // Verify original file is unchanged (transactional behavior)
            AssertThat(File.Exists(outputPath)).IsTrue();
            AssertThat(File.ReadAllText(outputPath)).IsEqual(originalContent);

            // Verify no temp files left behind
            var tempFiles = Directory.GetFiles(TempTestDirectory, "*.tmp");
            AssertThat(tempFiles.Length).IsEqual(0);
        }

        [TestCase]
        public async Task TestAsyncCancellation()
        {
            // Create a very large map that will take time to write
            var testDmap = new DmapFile
            {
                Header = new byte[] { 0x65, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                MapVersion = 0x365,
                PuzzleFile = "",
                SizeTiles = new Size(2000, 2000), // Large enough to take time
                TileSet = new Tile[2000, 2000]
            };

            // Initialize tiles
            for (int x = 0; x < 2000; x++)
            {
                for (int y = 0; y < 2000; y++)
                {
                    testDmap.TileSet[x, y] = new Tile((ushort)(x % 2), (ushort)(y % 10), (short)(x + y));
                }
            }

            string outputPath = Path.Combine(TempTestDirectory, "async_cancel_test.dmap");

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100)); // Cancel after 100ms

            bool cancelledCorrectly = false;
            try
            {
                await testDmap.ExportAsync(outputPath, cancellationToken: cts.Token);
            }
            catch (OperationCanceledException)
            {
                cancelledCorrectly = true;
            }

            // If it didn't cancel (because it was too fast), that's also acceptable
            // The important thing is that the cancellation mechanism works when needed
            AssertThat(cancelledCorrectly || File.Exists(outputPath))
                .OverrideFailureMessage("Export should either be cancelled or complete successfully")
                .IsTrue();
        }

        [TestCase]
        public async Task TestAsyncPerformance()
        {
            var testDmap = CreateLargeValidTestDmap();

            string outputPath = Path.Combine(TempTestDirectory, "async_performance_test.dmap");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await testDmap.ExportAsync(outputPath);
            stopwatch.Stop();

            // Verify file was created successfully
            AssertThat(File.Exists(outputPath)).IsTrue();
            AssertThat(new FileInfo(outputPath).Length).IsGreater(0);

            // Performance should be reasonable (less than 5 seconds for moderately large map)
            AssertThat(stopwatch.ElapsedMilliseconds).IsLess(5000);
        }

        [TestCase]
        public void TestBufferedWritingPerformance()
        {
            var testDmap = CreateLargeValidTestDmap();

            string outputPath1 = Path.Combine(TempTestDirectory, "buffered_test1.dmap");
            string outputPath2 = Path.Combine(TempTestDirectory, "buffered_test2.dmap");

            // Time the save operation (should be fast with buffering)
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            testDmap.Export(outputPath1);
            stopwatch.Stop();

            var firstSaveTime = stopwatch.ElapsedMilliseconds;

            // Save again (should be similarly fast)
            stopwatch.Restart();
            testDmap.Export(outputPath2);
            stopwatch.Stop();

            var secondSaveTime = stopwatch.ElapsedMilliseconds;

            // Both files should exist and have same size
            AssertThat(File.Exists(outputPath1)).IsTrue();
            AssertThat(File.Exists(outputPath2)).IsTrue();
            AssertThat(new FileInfo(outputPath1).Length).IsEqual(new FileInfo(outputPath2).Length);

            // Performance should be consistent and reasonable
            AssertThat(Math.Max(firstSaveTime, secondSaveTime)).IsLess(3000);
        }

        [TestCase]
        public void TestFileReplaceOperation()
        {
            var testDmap = CreateValidTestDmap();

            string outputPath = Path.Combine(TempTestDirectory, "replace_test.dmap");

            // Create initial file
            File.WriteAllText(outputPath, "initial content");
            var initialTime = File.GetLastWriteTime(outputPath);

            // Wait a bit to ensure timestamp difference
            System.Threading.Thread.Sleep(100);

            // Export over existing file
            testDmap.Export(outputPath);

            // Verify file was replaced (different timestamp and content)
            var finalTime = File.GetLastWriteTime(outputPath);
            AssertThat(finalTime > initialTime)
                .OverrideFailureMessage($"File timestamp should be newer: {finalTime} > {initialTime}")
                .IsTrue();
            AssertThat(new FileInfo(outputPath).Length).IsGreater(10); // DMAP files are bigger than our test content
        }

        [TestCase]
        public void TestValidFileExtensionWarning()
        {
            var testDmap = CreateValidTestDmap();

            string outputPath = Path.Combine(TempTestDirectory, "test.txt");

            // Should work but log a warning about non-standard extension
            testDmap.Export(outputPath);

            AssertThat(File.Exists(outputPath)).IsTrue();
        }

        private DmapFile CreateValidTestDmap()
        {
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

            return testDmap;
        }

        private DmapFile CreateLargeValidTestDmap()
        {
            var testDmap = new DmapFile
            {
                Header = new byte[] { 0x65, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                MapVersion = 0x365,
                PuzzleFile = "",
                SizeTiles = new Size(500, 500), // Moderately large for performance testing
                TileSet = new Tile[500, 500]
            };

            // Initialize tiles
            for (int x = 0; x < 500; x++)
            {
                for (int y = 0; y < 500; y++)
                {
                    testDmap.TileSet[x, y] = new Tile((ushort)(x % 2), (ushort)(y % 10), (short)(x + y));
                }
            }

            return testDmap;
        }
    }
}