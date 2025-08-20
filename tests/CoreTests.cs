using GdUnit4;
using static GdUnit4.Assertions;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;

namespace DMapImporter.Tests
{
    [TestSuite]
    public class CoreTests
    {
        [TestCase]
        public void TestTileCreation()
        {
            var tile = new Tile(1, 100, 50);

            AssertThat(tile.NoAccess).IsEqual(1);
            AssertThat(tile.Surface).IsEqual(100);
            AssertThat(tile.Height).IsEqual(50);
            AssertThat(tile.Access).IsEqual(0); // NoAccess=1 means Access=0
        }

        [TestCase]
        public void TestTileAccess()
        {
            var accessibleTile = new Tile(0, 100, 50);
            var blockedTile = new Tile(1, 100, 50);

            AssertThat(accessibleTile.Access).IsEqual(1);
            AssertThat(blockedTile.Access).IsEqual(0);
        }

        [TestCase]
        public void TestPortalCreation()
        {
            var position = new TilePosition(10, 20);
            var portal = new Portal(position, 123);

            AssertThat(portal.Position.X).IsEqual(10);
            AssertThat(portal.Position.Y).IsEqual(20);
            AssertThat(portal.Id).IsEqual(123);
        }

        [TestCase]
        public void TestCoverCreation()
        {
            var position = new TilePosition(5, 10);
            var size = new Size(2, 3);
            var offset = new PixelPosition(16, 32);

            var cover = new Cover
            {
                AniPath = "test/path.ani",
                AniName = "testAni",
                Position = position,
                BaseSize = size,
                Offset = offset,
                AnimationInterval = 1000
            };

            AssertThat(cover.AniPath).IsEqual("test/path.ani");
            AssertThat(cover.AniName).IsEqual("testAni");
            AssertThat(cover.Position.X).IsEqual(5);
            AssertThat(cover.Position.Y).IsEqual(10);
            AssertThat(cover.BaseSize.Width).IsEqual(2);
            AssertThat(cover.BaseSize.Height).IsEqual(3);
            AssertThat(cover.Offset.X).IsEqual(16);
            AssertThat(cover.Offset.Y).IsEqual(32);
            AssertThat(cover.AnimationInterval).IsEqual(1000);
        }

        [TestCase]
        public void TestUtilityStructs()
        {
            var size = new Size(640, 480);
            var tilePos = new TilePosition(100, 200);
            var pixelPos = new PixelPosition(1024, 768);
            var tileOffset = new TileOffset(-1, 2);
            var pixelOffset = new PixelOffset(-10, 15);

            AssertThat(size.Width).IsEqual(640);
            AssertThat(size.Height).IsEqual(480);

            AssertThat(tilePos.X).IsEqual(100);
            AssertThat(tilePos.Y).IsEqual(200);

            AssertThat(pixelPos.X).IsEqual(1024);
            AssertThat(pixelPos.Y).IsEqual(768);

            AssertThat(tileOffset.X).IsEqual(-1);
            AssertThat(tileOffset.Y).IsEqual(2);

            AssertThat(pixelOffset.X).IsEqual(-10);
            AssertThat(pixelOffset.Y).IsEqual(15);
        }

        [TestCase]
        public void TestSceneTileCreation()
        {
            var sceneTile = new SceneTile(1, 200, 75);

            AssertThat(sceneTile.NoAccess).IsEqual(1);
            AssertThat(sceneTile.Surface).IsEqual(200);
            AssertThat(sceneTile.Height).IsEqual(75);
            AssertThat(sceneTile.Access).IsEqual(0); // NoAccess=1 means Access=0
        }

        [TestCase]
        public void TestEffectCreation()
        {
            var position = new PixelPosition(100, 200);
            var effect = new Effect
            {
                EffectName = "explosion",
                Position = position
            };

            AssertThat(effect.EffectName).IsEqual("explosion");
            AssertThat(effect.Position.X).IsEqual(100);
            AssertThat(effect.Position.Y).IsEqual(200);
        }

        [TestCase]
        public void TestSoundCreation()
        {
            var position = new PixelPosition(300, 400);
            var sound = new Sound
            {
                SoundFile = "battle.wav",
                Position = position,
                Volume = 75,
                Range = 500
            };

            AssertThat(sound.SoundFile).IsEqual("battle.wav");
            AssertThat(sound.Position.X).IsEqual(300);
            AssertThat(sound.Position.Y).IsEqual(400);
            AssertThat(sound.Volume).IsEqual(75);
            AssertThat(sound.Range).IsEqual(500);
        }

        [TestCase]
        public void TestTerrainSceneCreation()
        {
            var position = new TilePosition(50, 75);
            var terrainScene = new TerrainScene("scenes/forest.scene", position);

            AssertThat(terrainScene.SceneFile).IsEqual("scenes/forest.scene");
            AssertThat(terrainScene.Position.X).IsEqual(50);
            AssertThat(terrainScene.Position.Y).IsEqual(75);
        }
    }
}