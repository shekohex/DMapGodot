using GdUnit4;
using static GdUnit4.Assertions;
using DMapImporter.Core.Dmap;
using System.IO;
using System;

namespace DMapImporter.Tests
{
    [TestSuite]
    public class DmapSharpCompressTest
    {
        [TestCase]
        public void TestBasicDmapFileLoading()
        {
            // Test with a simple DMAP file that we know exists
            string testMapPath = "Game/5017/map/map/Dcloister.DMap";

            if (!File.Exists(testMapPath))
            {
                return; // Skip test if file not found
            }

            var dmapFile = new DmapFile(testMapPath, Directory.GetCurrentDirectory());

            // Basic verification that the file loaded successfully
            AssertThat(dmapFile.DmapPath).Contains("Dcloister.DMap");
            AssertThat(dmapFile.MapVersion).IsGreater(0);
            AssertThat(dmapFile.SizeTiles.Width).IsGreater(0);
            AssertThat(dmapFile.SizeTiles.Height).IsGreater(0);
        }

        [TestCase]
        public void TestFileNotFoundHandling()
        {
            string invalidPath = "nonexistent/file.DMap";

            AssertThrown(() => new DmapFile(invalidPath))
                .IsInstanceOf<FileNotFoundException>();
        }
    }
}