using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static GdUnit4.Utils;
using DMapImporter.Nodes;
using DMapImporter.Core.Dmap;
using System.IO;

namespace DMapImporter.Tests
{
    [TestSuite]
    [RequireGodotRuntime]
    public class DMapRendererStructureTests
    {
        private static readonly string TestMapPath = Path.Combine(
            Directory.GetCurrentDirectory(), "Game", "5017", "map", "map", "Dsquare.DMap");

        [TestCase]
        public void TestThreeLayerCreation()
        {
            // Use auto_free to ensure proper cleanup of all created nodes
            var renderer = AutoFree(new DMapRenderer())!;
            var testDmap = new DmapFile(TestMapPath);

            renderer.LoadFromDMap(testDmap);

            // Verify three children
            AssertThat(renderer.GetChildCount()).IsEqual(3);

            // Verify layer types and names
            var bg = renderer.GetNode("BackgroundLayer");
            var terrain = renderer.GetNode("TerrainLayer");
            var objects = renderer.GetNode("ObjectLayer");

            AssertThat(bg).IsNotNull();
            AssertThat(terrain).IsNotNull();
            AssertThat(objects).IsNotNull();

            // Verify Z-ordering
            AssertThat(bg.GetIndex()).IsEqual(0);
            AssertThat(terrain.GetIndex()).IsEqual(1);
            AssertThat(objects.GetIndex()).IsEqual(2);
        }

        [TestCase]
        public void TestIsometricConfiguration()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var testDmap = new DmapFile(TestMapPath);

            renderer.LoadFromDMap(testDmap);

            var terrainLayer = renderer.GetNode<TileMapLayer>("TerrainLayer");
            var tileSet = terrainLayer.TileSet;

            AssertThat(tileSet.TileShape)
                .IsEqual(TileSet.TileShapeEnum.Isometric);
            AssertThat(tileSet.TileSize)
                .IsEqual(new Vector2I(64, 32));
        }

        [TestCase]
        public void TestCustomDataLayers()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var testDmap = new DmapFile(TestMapPath);

            renderer.LoadFromDMap(testDmap);

            var terrainLayer = renderer.GetNode<TileMapLayer>("TerrainLayer");
            var tileSet = terrainLayer.TileSet;

            // Verify custom data layers exist
            AssertThat(tileSet.GetCustomDataLayersCount()).IsEqual(3);
            AssertThat(tileSet.GetCustomDataLayerName(0)).IsEqual("walkable");
            AssertThat(tileSet.GetCustomDataLayerName(1)).IsEqual("surface");
            AssertThat(tileSet.GetCustomDataLayerName(2)).IsEqual("height");
        }

        [TestCase]
        public void TestLayerConfiguration()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var testDmap = new DmapFile(TestMapPath);

            renderer.LoadFromDMap(testDmap);

            var backgroundLayer = renderer.GetNode<TileMapLayer>("BackgroundLayer");
            var terrainLayer = renderer.GetNode<TileMapLayer>("TerrainLayer");
            var objectLayer = renderer.GetNode<Node2D>("ObjectLayer");

            // Verify Z-indices
            AssertThat(backgroundLayer.ZIndex).IsEqual(0);
            AssertThat(terrainLayer.ZIndex).IsEqual(1);
            AssertThat(objectLayer.ZIndex).IsEqual(2);

            // Verify Y-sorting on object layer
            AssertThat(objectLayer.YSortEnabled).IsTrue();

            // Verify layers are enabled
            AssertThat(backgroundLayer.Enabled).IsTrue();
            AssertThat(terrainLayer.Enabled).IsTrue();
        }

        [TestCase]
        public void TestNullDMapHandling()
        {
            var renderer = AutoFree(new DMapRenderer())!;

            // Should not crash with null input
            renderer.LoadFromDMap(null!);

            // Should have no children if null was passed
            AssertThat(renderer.GetChildCount()).IsEqual(0);
        }
    }
}