using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using static GdUnit4.Utils;
using DMapImporter.Nodes;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;

namespace DMapImporter.Tests
{
    [TestSuite]
    [RequireGodotRuntime]
    public class DMapRendererPortalIntegrationTests
    {
        [TestCase]
        public void TestRendererCreatesPortals()
        {
            var renderer = AutoFree(new DMapRenderer())!;

            var tilePos = new TilePosition(10, 20);
            var portal = new Portal(tilePos, 123);
            var portals = new[] { portal };

            var dmapFile = CreateTestDmapFile(portals);
            renderer.LoadFromDMap(dmapFile);

            var objectLayer = renderer.GetChild(2) as Node2D;
            AssertThat(objectLayer).IsNotNull();
            AssertThat(objectLayer!.Name).IsEqual("ObjectLayer");

            var portalCount = 0;
            foreach (Node child in objectLayer.GetChildren())
            {
                if (child is DMapPortal dmapPortal)
                {
                    portalCount++;
                    AssertThat(dmapPortal.PortalId).IsEqual(123u);
                    AssertThat(dmapPortal.Name).IsEqual("Portal_123");

                    AssertThat(dmapPortal.GetChildCount()).IsEqual(2);

                    var sprite = dmapPortal.GetChild<Sprite2D>(0);
                    AssertThat(sprite).IsNotNull();

                    var collision = dmapPortal.GetChild<CollisionShape2D>(1);
                    AssertThat(collision).IsNotNull();
                    AssertThat(collision.Shape).IsInstanceOf<CircleShape2D>();
                }
            }

            AssertThat(portalCount).IsEqual(1);
        }

        [TestCase]
        public void TestMultiplePortalsCreation()
        {
            var renderer = AutoFree(new DMapRenderer())!;

            var portals = new[]
            {
                new Portal(new TilePosition(5, 10), 1),
                new Portal(new TilePosition(15, 25), 2),
                new Portal(new TilePosition(30, 40), 3)
            };

            var dmapFile = CreateTestDmapFile(portals);
            renderer.LoadFromDMap(dmapFile);

            var objectLayer = renderer.GetChild(2) as Node2D;
            AssertThat(objectLayer).IsNotNull();

            var portalNodes = new System.Collections.Generic.List<DMapPortal>();
            foreach (Node child in objectLayer!.GetChildren())
            {
                if (child is DMapPortal portal)
                {
                    portalNodes.Add(portal);
                }
            }

            AssertThat(portalNodes.Count).IsEqual(3);

            var portalIds = new uint[] { 1, 2, 3 };
            for (int i = 0; i < portalNodes.Count; i++)
            {
                AssertThat(portalNodes[i].PortalId).IsIn(portalIds);
            }
        }

        [TestCase]
        public void TestPortalPositioning()
        {
            var renderer = AutoFree(new DMapRenderer())!;

            var tilePos = new TilePosition(25, 35);
            var portal = new Portal(tilePos, 456);
            var portals = new[] { portal };

            var dmapFile = CreateTestDmapFile(portals);
            renderer.LoadFromDMap(dmapFile);

            var objectLayer = renderer.GetChild(2) as Node2D;
            var portalNode = objectLayer!.GetChildren().OfType<DMapPortal>().FirstOrDefault();

            AssertThat(portalNode).IsNotNull();

            var converter = new CordConverter(new System.Drawing.Size(100, 100), new System.Drawing.Size(256, 256));
            var expectedWorldPos = converter.Cell2World(new Point(25, 35));

            AssertThat(portalNode!.Position.X).IsEqual(expectedWorldPos.X);
            AssertThat(portalNode.Position.Y).IsEqual(expectedWorldPos.Y);
        }

        private DmapFile CreateTestDmapFile(Portal[] portals)
        {
            var dmapFile = new DmapFile
            {
                DmapPath = "test.dmap",
                SizeTiles = new DMapImporter.Core.Utility.Size(100, 100),
                Portals = portals.ToList(),
                Covers = new List<Cover>()
            };

            var tileSet = new Tile[100, 100];
            for (int x = 0; x < 100; x++)
            {
                for (int y = 0; y < 100; y++)
                {
                    tileSet[x, y] = new Tile(0, 0, 0);
                }
            }

            dmapFile.TileSet = tileSet;

            return dmapFile;
        }
    }
}