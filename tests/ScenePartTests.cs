using GdUnit4;
using static GdUnit4.Assertions;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;

namespace DMapImporter.Tests
{
    [TestSuite]
    public class ScenePartTests
    {
        [TestCase]
        public void DefaultConstructor_InitializesWithDefaultValues()
        {
            var scenePart = new ScenePart();

            AssertThat(scenePart.AniPath).IsEqual(string.Empty);
            AssertThat(scenePart.AniName).IsEqual(string.Empty);
            AssertThat(scenePart.PixelLocation.X).IsEqual(0);
            AssertThat(scenePart.PixelLocation.Y).IsEqual(0);
            AssertThat(scenePart.Interval).IsEqual(0u);
            AssertThat(scenePart.Size.Width).IsEqual(0);
            AssertThat(scenePart.Size.Height).IsEqual(0);
            AssertThat(scenePart.Thickness).IsEqual(0u);
            AssertThat(scenePart.TileOffset.X).IsEqual(0);
            AssertThat(scenePart.TileOffset.Y).IsEqual(0);
            AssertThat(scenePart.OffsetElevation).IsEqual(0);
            AssertThat(scenePart.Tiles.Length).IsEqual(0);
        }

        [TestCase]
        public void Properties_CanBeInitialized()
        {
            var pixelLocation = new PixelOffset { X = 100, Y = 200 };
            var size = new Size { Width = 10, Height = 15 };
            var tileOffset = new TileOffset { X = 5, Y = 8 };

            var scenePart = new ScenePart
            {
                AniPath = "test/animation.ani",
                AniName = "TestAnim",
                PixelLocation = pixelLocation,
                Interval = 1000u,
                Size = size,
                Thickness = 25u,
                TileOffset = tileOffset,
                OffsetElevation = -100,
                Tiles = new SceneTile[size.Width, size.Height]
            };

            AssertThat(scenePart.AniPath).IsEqual("test/animation.ani");
            AssertThat(scenePart.AniName).IsEqual("TestAnim");
            AssertThat(scenePart.PixelLocation.X).IsEqual(100);
            AssertThat(scenePart.PixelLocation.Y).IsEqual(200);
            AssertThat(scenePart.Interval).IsEqual(1000u);
            AssertThat(scenePart.Size.Width).IsEqual(10);
            AssertThat(scenePart.Size.Height).IsEqual(15);
            AssertThat(scenePart.Thickness).IsEqual(25u);
            AssertThat(scenePart.TileOffset.X).IsEqual(5);
            AssertThat(scenePart.TileOffset.Y).IsEqual(8);
            AssertThat(scenePart.OffsetElevation).IsEqual(-100);
            AssertThat(scenePart.Tiles.GetLength(0)).IsEqual(10);
            AssertThat(scenePart.Tiles.GetLength(1)).IsEqual(15);
        }

        [TestCase]
        public void AniPath_CanBeModified()
        {
            var scenePart = new ScenePart();
            scenePart.AniPath = "new/path/animation.ani";

            AssertThat(scenePart.AniPath).IsEqual("new/path/animation.ani");
        }

        [TestCase]
        public void Tiles_CanBePopulatedWithSceneTiles()
        {
            var scenePart = new ScenePart
            {
                Size = new Size { Width = 2, Height = 2 },
                Tiles = new SceneTile[2, 2]
            };

            scenePart.Tiles[0, 0] = new SceneTile { NoAccess = 1, Surface = 2, Height = 100 };
            scenePart.Tiles[1, 1] = new SceneTile { NoAccess = 0, Surface = 5, Height = 200 };

            AssertThat(scenePart.Tiles[0, 0].NoAccess).IsEqual(1u);
            AssertThat(scenePart.Tiles[0, 0].Surface).IsEqual(2u);
            AssertThat(scenePart.Tiles[0, 0].Height).IsEqual(100);
            AssertThat(scenePart.Tiles[1, 1].NoAccess).IsEqual(0u);
            AssertThat(scenePart.Tiles[1, 1].Surface).IsEqual(5u);
            AssertThat(scenePart.Tiles[1, 1].Height).IsEqual(200);
        }

        [TestCase]
        [DataPoint(nameof(EdgeCaseValues))]
        public void Properties_HandleEdgeCases(uint interval, uint thickness, int elevation)
        {
            var scenePart = new ScenePart
            {
                Interval = interval,
                Thickness = thickness,
                OffsetElevation = elevation
            };

            AssertThat(scenePart.Interval).IsEqual(interval);
            AssertThat(scenePart.Thickness).IsEqual(thickness);
            AssertThat(scenePart.OffsetElevation).IsEqual(elevation);
        }

        public static object[][] EdgeCaseValues => new object[][]
        {
            new object[] { 0u, 0u, int.MinValue },
            new object[] { uint.MaxValue, uint.MaxValue, int.MaxValue },
            new object[] { 1000u, 50u, -1000 },
            new object[] { 16u, 1u, 500 }
        };
    }
}