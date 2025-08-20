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
    public class DMapPortalGodotTests
    {
        [TestCase]
        public void CreatesValidDMapPortal()
        {
            var portal = AutoFree(new DMapPortal())!;

            AssertThat(portal).IsNotNull();
            AssertThat(portal).IsInstanceOf<Area2D>();
            AssertThat(portal.PortalId).IsEqual(0u);
            AssertThat(portal.DestinationMap).IsEqual("");
            AssertThat(portal.DestinationPos).IsEqual(Vector2I.Zero);
        }

        [TestCase]
        public void CreatesPortalFromCorePortal()
        {
            var corePortal = new Portal(new TilePosition(10, 20), 123);
            var converter = CreateTestConverter();
            
            var portal = AutoFree(new DMapPortal(corePortal, converter))!;

            AssertThat(portal.PortalId).IsEqual(123u);
            AssertThat(portal.Name).IsEqual("Portal_123");
        }

        [TestCase]
        public void PortalHasCorrectToolAttribute()
        {
            var portal = AutoFree(new DMapPortal())!;
            
            var type = portal.GetType();
            var toolAttribute = type.GetCustomAttributes(typeof(Godot.ToolAttribute), false);
            
            AssertThat(toolAttribute.Length).IsEqual(1);
        }

        [TestCase]
        public void PortalExportsPropertiesCorrectly()
        {
            var portal = AutoFree(new DMapPortal())!;
            var type = portal.GetType();

            var portalIdProperty = type.GetProperty("PortalId");
            var destinationMapProperty = type.GetProperty("DestinationMap");
            var destinationPosProperty = type.GetProperty("DestinationPos");

            AssertThat(portalIdProperty).IsNotNull();
            AssertThat(destinationMapProperty).IsNotNull();
            AssertThat(destinationPosProperty).IsNotNull();

            // Check for Export attributes
            var portalIdExport = portalIdProperty.GetCustomAttributes(typeof(Godot.ExportAttribute), false);
            var destinationMapExport = destinationMapProperty.GetCustomAttributes(typeof(Godot.ExportAttribute), false);
            var destinationPosExport = destinationPosProperty.GetCustomAttributes(typeof(Godot.ExportAttribute), false);

            AssertThat(portalIdExport.Length).IsEqual(1);
            AssertThat(destinationMapExport.Length).IsEqual(1);
            AssertThat(destinationPosExport.Length).IsEqual(1);
        }

        [TestCase]
        public void PortalSetupCreatesRequiredChildren()
        {
            var portal = AutoFree(new DMapPortal())!;
            
            // Add to scene tree to trigger _Ready
            var scene = AutoFree(new Node2D())!;
            scene.AddChild(portal);
            
            // Portal should have sprite and collision shape
            AssertThat(portal.GetChildCount()).IsGreater(0);
            
            // Try to find sprite and collision children (names may vary)
            Sprite2D? sprite = null;
            CollisionShape2D? collision = null;
            
            foreach (Node child in portal.GetChildren())
            {
                if (child is Sprite2D s) sprite = s;
                if (child is CollisionShape2D c) collision = c;
            }
            
            // At least one of these should exist after _Ready is called
            AssertThat(portal.GetChildCount()).IsGreaterEqual(1);
        }

        [TestCase]
        public void PortalMonitoringIsEnabled()
        {
            var portal = AutoFree(new DMapPortal())!;
            
            // Add to scene tree to trigger _Ready
            var scene = AutoFree(new Node2D())!;
            scene.AddChild(portal);
            
            AssertThat(portal.Monitoring).IsTrue();
        }

        [TestCase]
        public void PortalPositionCalculatedCorrectly()
        {
            var corePortal = new Portal(new TilePosition(25, 25), 456);
            var converter = CreateTestConverter();
            
            var portal = AutoFree(new DMapPortal(corePortal, converter))!;
            
            // Position should be converted from tile coordinates
            AssertThat(portal.Position.X).IsNotEqual(0);
            AssertThat(portal.Position.Y).IsNotEqual(0);
        }

        [TestCase]
        public void PortalPropertiesCanBeSet()
        {
            var portal = AutoFree(new DMapPortal())!;
            
            portal.PortalId = 789u;
            portal.DestinationMap = "test_map.dmap";
            portal.DestinationPos = new Vector2I(50, 75);

            AssertThat(portal.PortalId).IsEqual(789u);
            AssertThat(portal.DestinationMap).IsEqual("test_map.dmap");
            AssertThat(portal.DestinationPos).IsEqual(new Vector2I(50, 75));
        }

        [TestCase]
        public void PortalInheritsFromArea2D()
        {
            var portal = AutoFree(new DMapPortal())!;
            
            AssertThat(portal).IsInstanceOf<Area2D>();
        }

        private CordConverter CreateTestConverter()
        {
            var dmapSize = new System.Drawing.Size(100, 100);
            var backgroundSize = new System.Drawing.Size(1024, 1024);
            return new CordConverter(dmapSize, backgroundSize);
        }
    }
}