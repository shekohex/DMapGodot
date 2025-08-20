using GdUnit4;
using static GdUnit4.Assertions;
using DMapImporter.Core.Dmap;
using System.Collections.Generic;
using System.Drawing;

namespace DMapImporter.Tests
{
    [TestSuite]
    public class CordConverterTests
    {
        [TestCase]
        [DataPoint(nameof(CoordinateTestData))]
        public void Cell2WorldConvertsCoordinatesCorrectly(int cellX, int cellY, int expectedWorldX, int expectedWorldY)
        {
            var dmapSize = new Size(100, 100);
            var backgroundSize = new Size(1024, 1024);
            var converter = new CordConverter(dmapSize, backgroundSize);
            
            var cellPoint = new Point(cellX, cellY);
            var worldPoint = converter.Cell2World(cellPoint);

            AssertThat(worldPoint.X).IsEqual(expectedWorldX);
            AssertThat(worldPoint.Y).IsEqual(expectedWorldY);
        }

        [TestCase]
        [DataPoint(nameof(RoundTripTestData))]
        public void World2CellRoundTripTest(int originalCellX, int originalCellY)
        {
            var dmapSize = new Size(100, 100);
            var backgroundSize = new Size(1024, 1024);
            var converter = new CordConverter(dmapSize, backgroundSize);
            
            var originalCell = new Point(originalCellX, originalCellY);
            var worldPoint = converter.Cell2World(originalCell);
            var convertedBackCell = converter.World2Cell(worldPoint);

            AssertThat(convertedBackCell.X).IsEqual(originalCellX);
            AssertThat(convertedBackCell.Y).IsEqual(originalCellY);
        }

        [TestCase]
        public void GetBackgroundWorldPosCalculatesCorrectly()
        {
            var dmapSize = new Size(100, 100);
            var backgroundSize = new Size(1024, 1024);
            var converter = new CordConverter(dmapSize, backgroundSize);
            
            var bgPos = converter.GetBackgroundWorldPos();
            
            // origin.X = 64 * (100/2) = 3200, origin.Y = 32/2 = 16
            // bgPos.X = 3200 - 1024/2 = 3200 - 512 = 2688
            // bgPos.Y = 16 + 32*100/2 - 1024/2 - ((100+1) % 2) * 16
            //         = 16 + 1600 - 512 - 16 = 1088
            AssertThat(bgPos.X).IsEqual(2688);
            AssertThat(bgPos.Y).IsEqual(1088);
        }

        [TestCase]
        public void Cell2BgConvertsCorrectly()
        {
            var dmapSize = new Size(100, 100);
            var backgroundSize = new Size(1024, 1024);
            var converter = new CordConverter(dmapSize, backgroundSize);
            
            var cellPoint = new Point(50, 50);
            var bgPoint = converter.Cell2Bg(cellPoint);
            
            AssertThat(bgPoint.X).IsGreaterEqual(0);
            AssertThat(bgPoint.Y).IsGreaterEqual(0);
        }

        public static IEnumerable<object[]> CoordinateTestData => new[]
        {
            // Center origin calculations: 64 * (100/2) = 3200, 32/2 = 16
            // Formula: world.X = 32 * (cell.X - cell.Y) + 3200
            //         world.Y = 16 * (cell.X + cell.Y) + 16
            new object[] { 0, 0, 3200, 16 },      // Origin point
            new object[] { 1, 0, 3232, 32 },      // X=32*(1-0)+3200=3232, Y=16*(1+0)+16=32
            new object[] { 0, 1, 3168, 32 },      // X=32*(0-1)+3200=3168, Y=16*(0+1)+16=32
            new object[] { 50, 50, 3200, 1616 },  // X=32*(50-50)+3200=3200, Y=16*(50+50)+16=1616
            new object[] { 25, 0, 4000, 416 },    // X=32*(25-0)+3200=4000, Y=16*(25+0)+16=416
            new object[] { 0, 25, 2400, 416 },    // X=32*(0-25)+3200=2400, Y=16*(0+25)+16=416
            new object[] { 10, 5, 3360, 256 },    // X=32*(10-5)+3200=3360, Y=16*(10+5)+16=256
            new object[] { 5, 10, 3040, 256 }     // X=32*(5-10)+3200=3040, Y=16*(5+10)+16=256
        };

        public static IEnumerable<object[]> RoundTripTestData => new[]
        {
            new object[] { 0, 0 },
            new object[] { 10, 10 },
            new object[] { 25, 25 },
            new object[] { 50, 50 },
            new object[] { 75, 25 },
            new object[] { 25, 75 },
            new object[] { 99, 99 }
        };
    }
}