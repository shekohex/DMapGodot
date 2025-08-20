using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using static GdUnit4.Utils;
using DMapImporter.Nodes;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using System.Drawing;

namespace DMapImporter.Tests
{
    [TestSuite]
    [RequireGodotRuntime]
    public class DMapPortalTests
    {
        [TestCase]
        public void TestPortalCreation()
        {
            var portal = AutoFree(new DMapPortal())!;
            AssertThat(portal).IsNotNull();
            AssertThat(portal.PortalId).IsEqual(0u);
            AssertThat(portal.DestinationMap).IsEqual("");
            AssertThat(portal.DestinationPos).IsEqual(Vector2I.Zero);
        }

        [TestCase]
        public void TestPortalWithData()
        {
            var tilePos = new TilePosition(5, 10);
            var portalData = new Portal(tilePos, 123);
            var converter = new CordConverter(new System.Drawing.Size(100, 100), new System.Drawing.Size(800, 600));

            var portal = AutoFree(new DMapPortal(portalData, converter))!;
            AssertThat(portal.PortalId).IsEqual(123u);
            AssertThat(portal.Name).IsEqual("Portal_123");
        }

        [TestCase]
        public void TestPortalPositioning()
        {
            var tilePos = new TilePosition(10, 20);
            var portalData = new Portal(tilePos, 456);
            var converter = new CordConverter(new System.Drawing.Size(100, 100), new System.Drawing.Size(800, 600));

            var portal = AutoFree(new DMapPortal(portalData, converter))!;

            var expectedWorldPos = converter.Cell2World(new Point(10, 20));
            AssertThat(portal.Position.X).IsEqual(expectedWorldPos.X);
            AssertThat(portal.Position.Y).IsEqual(expectedWorldPos.Y);
        }

        [TestCase]
        public void TestPortalComponents()
        {
            var portal = AutoFree(new DMapPortal())!;
            portal._Ready();

            AssertThat(portal.GetChildCount()).IsEqual(2);
            AssertThat(portal.Monitoring).IsTrue();

            var sprite = portal.GetChild<Sprite2D>(0);
            AssertThat(sprite).IsNotNull();
            AssertThat(sprite.Texture).IsNotNull();

            var collision = portal.GetChild<CollisionShape2D>(1);
            AssertThat(collision).IsNotNull();
            AssertThat(collision.Shape).IsInstanceOf<CircleShape2D>();

            var circleShape = collision.Shape as CircleShape2D;
            AssertThat(circleShape!.Radius).IsEqual(32f);
        }

        [TestCase]
        public void TestPortalAreaDetection()
        {
            var portal = AutoFree(new DMapPortal())!;
            portal._Ready();

            AssertThat(portal).IsInstanceOf<Area2D>();
            AssertThat(portal.Monitoring).IsTrue();
        }

        [TestCase]
        public void TestPortalExportProperties()
        {
            var portal = AutoFree(new DMapPortal())!;

            portal.DestinationMap = "test_map";
            portal.DestinationPos = new Vector2I(100, 200);

            AssertThat(portal.DestinationMap).IsEqual("test_map");
            AssertThat(portal.DestinationPos).IsEqual(new Vector2I(100, 200));
        }

        [TestCase]
        public void TestCoordinateConversion()
        {
            var converter = new CordConverter(new System.Drawing.Size(50, 50), new System.Drawing.Size(400, 300));

            var testPoint = new Point(25, 25);
            var worldPos = converter.Cell2World(testPoint);

            AssertThat(worldPos.X).IsNotEqual(0);
            AssertThat(worldPos.Y).IsNotEqual(0);

            var tilePos = new TilePosition((uint)testPoint.X, (uint)testPoint.Y);
            var portalData = new Portal(tilePos, 789);
            var portal = AutoFree(new DMapPortal(portalData, converter))!;

            AssertThat(portal.Position.X).IsEqual(worldPos.X);
            AssertThat(portal.Position.Y).IsEqual(worldPos.Y);
        }
    }
}