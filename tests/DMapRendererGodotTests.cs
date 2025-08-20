using GdUnit4;
using static GdUnit4.Assertions;
using static GdUnit4.Utils;
using Godot;
using DMapImporter.Nodes;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using System.Drawing;

namespace DMapImporter.Tests
{
    [TestSuite]
    [RequireGodotRuntime]
    public class DMapRendererGodotTests
    {
        [TestCase]
        public void CreatesValidDMapRenderer()
        {
            var renderer = AutoFree(new DMapRenderer())!;

            AssertThat(renderer).IsNotNull();
            AssertThat(renderer.IsInsideTree()).IsFalse();
            AssertThat(renderer.DMapPath).IsEqual("");
            AssertThat(renderer.MapSize).IsEqual(Vector2I.Zero);
            AssertThat(renderer.TileSize).IsEqual(32);
        }

        [TestCase]
        public void LoadFromDMapInitializesCorrectly()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var dmap = CreateTestDMapFile();

            renderer.LoadFromDMap(dmap);

            AssertThat(renderer.MapSize.X).IsEqual(100);
            AssertThat(renderer.MapSize.Y).IsEqual(100);
            AssertThat(renderer.DMapPath).IsEqual("test.dmap");
        }

        [TestCase]
        public void LoadFromDMapClearsExistingChildren()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var testNode = AutoFree(new Node2D())!;
            renderer.AddChild(testNode);

            AssertThat(renderer.GetChildCount()).IsEqual(1);

            var dmap = CreateTestDMapFile();
            renderer.LoadFromDMap(dmap);

            // After loading, old children should be queued for deletion
            // and new layers created
            AssertThat(renderer.GetChildCount()).IsGreater(0);
        }

        [TestCase]
        public void LoadFromDMapHandlesNullInput()
        {
            var renderer = AutoFree(new DMapRenderer())!;

            // This should not crash and should log an error
            renderer.LoadFromDMap(null);

            AssertThat(renderer.MapSize).IsEqual(Vector2I.Zero);
            AssertThat(renderer.DMapPath).IsEqual("");
        }

        [TestCase]
        public void CoordinateHelperConvertsCorrectly()
        {
            var dmap = CreateTestDMapFile();
            var helper = new CoordinateHelper(dmap);

            var localPos = helper.TileToLocal(50, 50);
            var tilePos = helper.LocalToTile(localPos);

            AssertThat(tilePos.X).IsEqual(50);
            AssertThat(tilePos.Y).IsEqual(50);
        }

        [TestCase]
        public void RendererHasCorrectToolAttribute()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            
            // Should be marked as [Tool] for editor functionality
            var type = renderer.GetType();
            var toolAttribute = type.GetCustomAttributes(typeof(Godot.ToolAttribute), false);
            
            AssertThat(toolAttribute.Length).IsEqual(1);
        }

        [TestCase]
        public void RendererExportsPropertiesCorrectly()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var type = renderer.GetType();

            var dmapPathProperty = type.GetProperty("DMapPath");
            var mapSizeProperty = type.GetProperty("MapSize");
            var tileSizeProperty = type.GetProperty("TileSize");

            AssertThat(dmapPathProperty).IsNotNull();
            AssertThat(mapSizeProperty).IsNotNull();
            AssertThat(tileSizeProperty).IsNotNull();

            // Check for Export attributes
            var dmapPathExport = dmapPathProperty.GetCustomAttributes(typeof(Godot.ExportAttribute), false);
            var mapSizeExport = mapSizeProperty.GetCustomAttributes(typeof(Godot.ExportAttribute), false);
            var tileSizeExport = tileSizeProperty.GetCustomAttributes(typeof(Godot.ExportAttribute), false);

            AssertThat(dmapPathExport.Length).IsEqual(1);
            AssertThat(mapSizeExport.Length).IsEqual(1);
            AssertThat(tileSizeExport.Length).IsEqual(1);
        }

        [TestCase]
        public void RendererInheritsFromNode2D()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            
            AssertThat(renderer).IsInstanceOf<Node2D>();
        }

        private DmapFile CreateTestDMapFile()
        {
            // Create a minimal test DMAP file
            var dmap = new DmapFile();
            dmap.DmapPath = "test.dmap";
            dmap.SizeTiles = new DMapImporter.Core.Utility.Size(100, 100);
            
            // Initialize minimal required data
            dmap.TileSet = new Tile[100, 100];
            for (int x = 0; x < 100; x++)
            {
                for (int y = 0; y < 100; y++)
                {
                    dmap.TileSet[x, y] = new Tile(0, 1, 0); // accessible, surface=1, height=0
                }
            }

            return dmap;
        }
    }
}