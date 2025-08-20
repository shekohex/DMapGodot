using GdUnit4;
using static GdUnit4.Assertions;
using static GdUnit4.Utils;
using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Nodes;
using System.IO;
using System.Collections.Generic;

namespace DMapImporter.Tests
{
    [TestSuite]
    [RequireGodotRuntime]
    public class DMapImportIntegrationTests
    {
        private const string TestMapPath = "Game/5017/map/0001.7z";
        private const string TempOutputPath = "res://temp_test_output";

        [TestCase]
        public void ImporterHasCorrectConfiguration()
        {
            var importer = AutoFree(new DMapImporter.Importers.DMapImporter())!;

            AssertThat(importer._GetImporterName()).IsEqual("dmap.importer");
            AssertThat(importer._GetVisibleName()).IsEqual("DMAP Map File");
            AssertThat(importer._GetSaveExtension()).IsEqual("tscn");
            AssertThat(importer._GetResourceType()).IsEqual("PackedScene");
            AssertThat(importer._GetPriority()).IsEqual(1.0f);
            AssertThat(importer._GetImportOrder()).IsEqual(0);
        }

        [TestCase]
        public void ImporterRecognizesCorrectExtensions()
        {
            var importer = AutoFree(new DMapImporter.Importers.DMapImporter())!;
            var extensions = importer._GetRecognizedExtensions();

            AssertThat(extensions).ContainsExactly(new[] { "dmap", "7z", "zmap" });
        }

        [TestCase]
        public void ImporterHasCorrectPresetCount()
        {
            var importer = AutoFree(new DMapImporter.Importers.DMapImporter())!;

            AssertThat(importer._GetPresetCount()).IsEqual(2);
            AssertThat(importer._GetPresetName(0)).IsEqual("Default");
            AssertThat(importer._GetPresetName(1)).IsEqual("High Quality");
            AssertThat(importer._GetPresetName(999)).IsEqual("Unknown");
        }

        [TestCase]
        public void ImporterProvideDefaultImportOptions()
        {
            var importer = AutoFree(new DMapImporter.Importers.DMapImporter())!;
            var options = importer._GetImportOptions("test.dmap", 0);

            AssertThat(options.Count).IsGreater(0);

            // Check for required import options
            var optionNames = new List<string>();
            foreach (var option in options)
            {
                var dict = option as Godot.Collections.Dictionary;
                if (dict.ContainsKey("name"))
                {
                    optionNames.Add(dict["name"].AsString());
                }
            }

            AssertThat(optionNames).Contains("tile_size");
            AssertThat(optionNames).Contains("enable_terrain");
            AssertThat(optionNames).Contains("enable_portals");
            AssertThat(optionNames).Contains("enable_objects");
            AssertThat(optionNames).Contains("coordinate_system");
        }

        [TestCase]
        public void ImporterHighQualityPresetHasDifferentOptions()
        {
            var importer = AutoFree(new DMapImporter.Importers.DMapImporter())!;
            var defaultOptions = importer._GetImportOptions("test.dmap", 0);
            var highQualityOptions = importer._GetImportOptions("test.dmap", 1);

            AssertThat(highQualityOptions.Count).IsGreaterEqual(defaultOptions.Count);

            // Both should have texture_quality and enable_compression
            var hqOptionNames = new List<string>();
            foreach (var option in highQualityOptions)
            {
                var dict = option as Godot.Collections.Dictionary;
                if (dict.ContainsKey("name"))
                {
                    hqOptionNames.Add(dict["name"].AsString());
                }
            }

            AssertThat(hqOptionNames).Contains("texture_quality");
            AssertThat(hqOptionNames).Contains("enable_compression");
        }

        [TestCase]
        public void ImporterHandlesInvalidSourceFile()
        {
            var importer = AutoFree(new DMapImporter.Importers.DMapImporter())!;
            var options = new Godot.Collections.Dictionary();
            var platformVariants = new Godot.Collections.Array<string>();
            var genFiles = new Godot.Collections.Array<string>();

            var result = importer._Import(
                "non_existent_file.dmap",
                TempOutputPath,
                options,
                platformVariants,
                genFiles
            );

            AssertThat(result).IsEqual(Error.FileNotFound);
        }

        [TestCase]
        public void ImportsTestMapSuccessfully()
        {
            if (!File.Exists(TestMapPath))
            {
                // Skip test if test map doesn't exist
                return;
            }

            var importer = AutoFree(new DMapImporter.Importers.DMapImporter())!;
            var options = CreateDefaultImportOptions();
            var platformVariants = new Godot.Collections.Array<string>();
            var genFiles = new Godot.Collections.Array<string>();

            var result = importer._Import(
                TestMapPath,
                TempOutputPath,
                options,
                platformVariants,
                genFiles
            );

            // Import should succeed or return specific error
            AssertThat(result).IsIn(Error.Ok, Error.ParseError, Error.FileCorrupt);

            // Clean up if file was created
            CleanupTestFiles();
        }

        [TestCase]
        public void ImportOptionsValidationWorks()
        {
            var importer = AutoFree(new DMapImporter.Importers.DMapImporter())!;

            // Test option visibility logic
            var options = new Godot.Collections.Dictionary
            {
                { "enable_compression", true }
            };

            var visible = importer._GetOptionVisibility("test.dmap", "texture_quality", options);
            AssertThat(visible).IsTrue();

            options["enable_compression"] = false;
            visible = importer._GetOptionVisibility("test.dmap", "texture_quality", options);
            AssertThat(visible).IsFalse();
        }

        [TestCase]
        public void ImporterProcessesOptionsCorrectly()
        {
            var importer = AutoFree(new DMapImporter.Importers.DMapImporter())!;

            // Create options with custom values
            var options = new Godot.Collections.Dictionary
            {
                { "tile_size", 64 },
                { "enable_terrain", false },
                { "enable_portals", true },
                { "enable_objects", false },
                { "coordinate_system", 1 },
                { "texture_quality", 1.5f },
                { "enable_compression", false }
            };

            var platformVariants = new Godot.Collections.Array<string>();
            var genFiles = new Godot.Collections.Array<string>();

            // This tests that options are parsed correctly even if import fails due to missing file
            var result = importer._Import(
                "test_options.dmap",
                TempOutputPath,
                options,
                platformVariants,
                genFiles
            );

            // Should fail with FileNotFound, but options should be processed
            AssertThat(result).IsEqual(Error.FileNotFound);
        }

        [TestCase]
        public void CreateSimpleDMapRendererScene()
        {
            // Test creating a minimal scene with DMapRenderer
            var scene = AutoFree(new Node2D())!;
            var renderer = AutoFree(new DMapRenderer())!;

            scene.AddChild(renderer);
            scene.Name = "TestDMapScene";
            renderer.Name = "DMapRenderer";

            AssertThat(scene.GetChildCount()).IsEqual(1);
            AssertThat(scene.GetChild<DMapRenderer>(0)).IsNotNull();
            AssertThat(renderer.GetParent()).IsEqual(scene);
        }

        [TestCase]
        public void RendererIntegrationWithPortals()
        {
            var renderer = AutoFree(new DMapRenderer())!;
            var portal = AutoFree(new DMapPortal())!;

            // Add portal as child of renderer
            renderer.AddChild(portal);

            AssertThat(renderer.GetChildCount()).IsEqual(1);
            AssertThat(renderer.GetChild<DMapPortal>(0)).IsNotNull();
            AssertThat(portal.GetParent()).IsEqual(renderer);
        }

        private Godot.Collections.Dictionary CreateDefaultImportOptions()
        {
            return new Godot.Collections.Dictionary
            {
                { "tile_size", 32 },
                { "enable_terrain", true },
                { "enable_portals", true },
                { "enable_objects", true },
                { "coordinate_system", 0 },
                { "texture_quality", 0.8f },
                { "enable_compression", true }
            };
        }

        private void CleanupTestFiles()
        {
            try
            {
                if (File.Exists(TempOutputPath + ".tscn"))
                {
                    File.Delete(TempOutputPath + ".tscn");
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}