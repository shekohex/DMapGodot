using GdUnit4;
using static GdUnit4.Assertions;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using System.Collections.Generic;

namespace DMapImporter.Tests
{
    [TestSuite]
    public class SceneLayerTests
    {
        [TestCase]
        public void DefaultConstructor_InitializesWithDefaultValues()
        {
            var sceneLayer = new SceneLayer();

            AssertThat(sceneLayer.Index).IsEqual(0u);
            AssertThat(sceneLayer.MoveRate.X).IsEqual(0);
            AssertThat(sceneLayer.MoveRate.Y).IsEqual(0);
            AssertThat(sceneLayer.TerrainScenes).IsNotNull();
            AssertThat(sceneLayer.TerrainScenes).HasSize(0);
            AssertThat(sceneLayer.Puzzles).IsNotNull();
            AssertThat(sceneLayer.Puzzles).HasSize(0);
        }

        [TestCase]
        public void Properties_CanBeSetAndRetrieved()
        {
            var moveRate = new PixelPosition { X = 50, Y = 75 };
            var sceneLayer = new SceneLayer
            {
                Index = 5u,
                MoveRate = moveRate
            };

            AssertThat(sceneLayer.Index).IsEqual(5u);
            AssertThat(sceneLayer.MoveRate.X).IsEqual(50);
            AssertThat(sceneLayer.MoveRate.Y).IsEqual(75);
        }

        [TestCase]
        public void TerrainScenes_CanBeAdded()
        {
            var sceneLayer = new SceneLayer();
            var terrainScene1 = new TerrainScene();
            var terrainScene2 = new TerrainScene();

            sceneLayer.TerrainScenes.Add(terrainScene1);
            sceneLayer.TerrainScenes.Add(terrainScene2);

            AssertThat(sceneLayer.TerrainScenes).HasSize(2);
            AssertThat(sceneLayer.TerrainScenes[0]).IsEqual(terrainScene1);
            AssertThat(sceneLayer.TerrainScenes[1]).IsEqual(terrainScene2);
        }

        [TestCase]
        public void Puzzles_CanBeAdded()
        {
            var sceneLayer = new SceneLayer();
            
            sceneLayer.Puzzles.Add("puzzle1.dat");
            sceneLayer.Puzzles.Add("puzzle2.dat");
            sceneLayer.Puzzles.Add("complex_puzzle.dat");

            AssertThat(sceneLayer.Puzzles).HasSize(3);
            AssertThat(sceneLayer.Puzzles[0]).IsEqual("puzzle1.dat");
            AssertThat(sceneLayer.Puzzles[1]).IsEqual("puzzle2.dat");
            AssertThat(sceneLayer.Puzzles[2]).IsEqual("complex_puzzle.dat");
        }

        [TestCase]
        public void TerrainScenes_CanBeCleared()
        {
            var sceneLayer = new SceneLayer();
            sceneLayer.TerrainScenes.Add(new TerrainScene());
            sceneLayer.TerrainScenes.Add(new TerrainScene());

            AssertThat(sceneLayer.TerrainScenes).HasSize(2);

            sceneLayer.TerrainScenes.Clear();

            AssertThat(sceneLayer.TerrainScenes).HasSize(0);
        }

        [TestCase]
        public void Puzzles_CanBeCleared()
        {
            var sceneLayer = new SceneLayer();
            sceneLayer.Puzzles.Add("puzzle1.dat");
            sceneLayer.Puzzles.Add("puzzle2.dat");

            AssertThat(sceneLayer.Puzzles).HasSize(2);

            sceneLayer.Puzzles.Clear();

            AssertThat(sceneLayer.Puzzles).HasSize(0);
        }

        [TestCase]
        [DataPoint(nameof(LayerIndexValues))]
        public void Index_AcceptsVariousValues(uint index)
        {
            var sceneLayer = new SceneLayer { Index = index };

            AssertThat(sceneLayer.Index).IsEqual(index);
        }

        [TestCase]
        [DataPoint(nameof(MoveRateValues))]
        public void MoveRate_AcceptsVariousValues(int x, int y)
        {
            var moveRate = new PixelPosition { X = x, Y = y };
            var sceneLayer = new SceneLayer { MoveRate = moveRate };

            AssertThat(sceneLayer.MoveRate.X).IsEqual(x);
            AssertThat(sceneLayer.MoveRate.Y).IsEqual(y);
        }

        public static object[][] LayerIndexValues => new object[][]
        {
            new object[] { 0u },
            new object[] { 1u },
            new object[] { 10u },
            new object[] { 255u },
            new object[] { uint.MaxValue }
        };

        public static object[][] MoveRateValues => new object[][]
        {
            new object[] { 0, 0 },
            new object[] { -100, -200 },
            new object[] { 100, 200 },
            new object[] { int.MinValue, int.MaxValue },
            new object[] { 1, -1 }
        };
    }
}