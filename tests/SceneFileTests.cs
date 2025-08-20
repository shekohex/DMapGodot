using GdUnit4;
using static GdUnit4.Assertions;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using System;
using System.IO;
using System.Collections.Generic;

namespace DMapImporter.Tests
{
    [TestSuite]
    public class SceneFileTests
    {
        private string _tempDir = string.Empty;
        private string _testSceneFile = string.Empty;

        [Before]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"DMapTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            
            _testSceneFile = Path.Combine(_tempDir, "test_scene.scene");
            CreateTestSceneFile();
        }

        [After]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [TestCase]
        public void Constructor_WithValidFile_LoadsCorrectly()
        {
            var sceneFile = new SceneFile(_tempDir, "test_scene.scene");

            AssertThat(sceneFile.SceneFilePath).IsEqual("test_scene.scene");
            AssertThat(sceneFile.ScenePartCount).IsEqual(1u);
            AssertThat(sceneFile.SceneParts).HasSize(1);
        }

        [TestCase]
        public void Constructor_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            AssertThrown(() => new SceneFile(_tempDir, "nonexistent.scene"))
                .IsInstanceOf<FileNotFoundException>();
        }

        [TestCase]
        public void Constructor_WithFullyQualifiedPath_ConvertsToRelativePath()
        {
            var fullPath = Path.Combine(_tempDir, "test_scene.scene");
            var sceneFile = new SceneFile(_tempDir, fullPath);

            AssertThat(sceneFile.SceneFilePath).IsEqual("test_scene.scene");
        }

        [TestCase]
        public void LoadedScenePart_HasCorrectProperties()
        {
            var sceneFile = new SceneFile(_tempDir, "test_scene.scene");
            var scenePart = sceneFile.SceneParts[0];

            AssertThat(scenePart.AniPath).IsEqual("test/path/animation.ani");
            AssertThat(scenePart.AniName).IsEqual("TestAnimation");
            AssertThat(scenePart.PixelLocation.X).IsEqual(100);
            AssertThat(scenePart.PixelLocation.Y).IsEqual(200);
            AssertThat(scenePart.Interval).IsEqual(500u);
            AssertThat(scenePart.Size.Width).IsEqual(2);
            AssertThat(scenePart.Size.Height).IsEqual(2);
            AssertThat(scenePart.Thickness).IsEqual(10u);
            AssertThat(scenePart.TileOffset.X).IsEqual(5);
            AssertThat(scenePart.TileOffset.Y).IsEqual(6);
            AssertThat(scenePart.OffsetElevation).IsEqual(-50);
        }

        [TestCase]
        public void LoadedSceneTiles_HaveCorrectProperties()
        {
            var sceneFile = new SceneFile(_tempDir, "test_scene.scene");
            var scenePart = sceneFile.SceneParts[0];

            AssertThat(scenePart.Tiles[0, 0].NoAccess).IsEqual(1u);
            AssertThat(scenePart.Tiles[0, 0].Surface).IsEqual(2u);
            AssertThat(scenePart.Tiles[0, 0].Height).IsEqual(100);
            
            AssertThat(scenePart.Tiles[1, 1].NoAccess).IsEqual(0u);
            AssertThat(scenePart.Tiles[1, 1].Surface).IsEqual(5u);
            AssertThat(scenePart.Tiles[1, 1].Height).IsEqual(150);
        }

        [TestCase]
        public void Save_CreatesValidFileStructure()
        {
            var originalFile = new SceneFile(_tempDir, "test_scene.scene");
            var outputDir = Path.Combine(_tempDir, "output");
            Directory.CreateDirectory(outputDir);

            var savedPath = originalFile.Save(outputDir);

            AssertThat(savedPath).IsEqual("test_scene.scene");
            AssertThat(File.Exists(Path.Combine(outputDir, "test_scene.scene"))).IsTrue();
        }

        [TestCase]
        public void Save_AndReload_PreservesData()
        {
            var originalFile = new SceneFile(_tempDir, "test_scene.scene");
            var outputDir = Path.Combine(_tempDir, "output");
            Directory.CreateDirectory(outputDir);

            originalFile.Save(outputDir);
            var reloadedFile = new SceneFile(outputDir, "test_scene.scene");

            AssertThat(reloadedFile.ScenePartCount).IsEqual(originalFile.ScenePartCount);
            AssertThat(reloadedFile.SceneParts).HasSize(originalFile.SceneParts.Count);

            var originalPart = originalFile.SceneParts[0];
            var reloadedPart = reloadedFile.SceneParts[0];

            AssertThat(reloadedPart.AniPath).IsEqual(originalPart.AniPath);
            AssertThat(reloadedPart.AniName).IsEqual(originalPart.AniName);
            AssertThat(reloadedPart.PixelLocation.X).IsEqual(originalPart.PixelLocation.X);
            AssertThat(reloadedPart.PixelLocation.Y).IsEqual(originalPart.PixelLocation.Y);
            AssertThat(reloadedPart.Size.Width).IsEqual(originalPart.Size.Width);
            AssertThat(reloadedPart.Size.Height).IsEqual(originalPart.Size.Height);
        }

        private void CreateTestSceneFile()
        {
            using (var writer = new BinaryWriter(File.Create(_testSceneFile)))
            {
                writer.Write(1u);

                var aniPath = "test/path/animation.ani".PadRight(256, '\0');
                writer.Write(System.Text.Encoding.ASCII.GetBytes(aniPath));

                var aniName = "TestAnimation".PadRight(64, '\0');
                writer.Write(System.Text.Encoding.ASCII.GetBytes(aniName));

                writer.Write(100);
                writer.Write(200);

                writer.Write(500u);

                writer.Write(2);
                writer.Write(2);

                writer.Write(10u);

                writer.Write(5);
                writer.Write(6);

                writer.Write(-50);

                writer.Write(1u);
                writer.Write(2u);
                writer.Write(100);

                writer.Write(0u);
                writer.Write(3u);
                writer.Write(120);

                writer.Write(1u);
                writer.Write(4u);
                writer.Write(130);

                writer.Write(0u);
                writer.Write(5u);
                writer.Write(150);
            }
        }
    }
}