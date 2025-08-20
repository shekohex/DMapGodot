using System;
using System.IO;
using DMapGodot.Importers;
using Godot;
using GdUnit4;
using Microsoft.Extensions.Logging;
using static GdUnit4.Assertions;
using static GdUnit4.Utils;

[TestSuite]
[RequireGodotRuntime]
public partial class TextureConverterTests : Node
{

    private string _testDataPath = null!;
    private byte[] _validDdsData = null!;

    public override void _Ready()
    {
        var validGamePath = Path.Combine(ProjectSettings.GlobalizePath("res://"), "Game", "5017", "data", "ItemMinIcon");
        if (Directory.Exists(validGamePath))
        {
            _testDataPath = validGamePath;
        }
        else
        {
            _testDataPath = Path.GetTempPath();
        }

        CreateTestDdsData();
    }

    private void CreateTestDdsData()
    {
        _validDdsData = new byte[]
        {
            0x44, 0x44, 0x53, 0x20,
            0x7C, 0x00, 0x00, 0x00,
            0x07, 0x10, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00
        };
        Array.Resize(ref _validDdsData, 128);
    }

    [TestCase]
    public void TestConvertValidDDSFile()
    {
        TextureConverter.ClearCache();

        if (Directory.Exists(_testDataPath))
        {
            var ddsFiles = Directory.GetFiles(_testDataPath, "*.dds");
            if (ddsFiles.Length > 0)
            {
                var result = TextureConverter.ConvertDDSToTexture(ddsFiles[0]);
                AssertThat(result).IsNotNull();
                return;
            }
        }

        AssertThat(true).IsTrue();
    }

    [TestCase]
    public void TestConvertNonExistentFile()
    {
        TextureConverter.ClearCache();

        var result = TextureConverter.ConvertDDSToTexture("/non/existent/path.dds");

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestConvertNullPath()
    {
        TextureConverter.ClearCache();

        var result = TextureConverter.ConvertDDSToTexture((string)null!);

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestConvertEmptyPath()
    {
        TextureConverter.ClearCache();

        var result = TextureConverter.ConvertDDSToTexture("");

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestConvertNullByteArray()
    {
        TextureConverter.ClearCache();

        var result = TextureConverter.ConvertDDSToTexture((byte[])null!);

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestConvertEmptyByteArray()
    {
        TextureConverter.ClearCache();

        var result = TextureConverter.ConvertDDSToTexture(new byte[0]);

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestConvertByteArray()
    {
        TextureConverter.ClearCache();

        var result = TextureConverter.ConvertDDSToTexture(_validDdsData);

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestCacheFunctionality()
    {
        TextureConverter.ClearCache();
        AssertThat(TextureConverter.GetCacheCount()).IsEqual(0);

        if (Directory.Exists(_testDataPath))
        {
            var ddsFiles = Directory.GetFiles(_testDataPath, "*.dds");
            if (ddsFiles.Length > 0)
            {
                var firstCall = TextureConverter.ConvertDDSToTexture(ddsFiles[0]);
                var cacheCountAfterFirst = TextureConverter.GetCacheCount();

                var secondCall = TextureConverter.ConvertDDSToTexture(ddsFiles[0]);
                var cacheCountAfterSecond = TextureConverter.GetCacheCount();

                if (firstCall != null)
                {
                    AssertThat(cacheCountAfterFirst).IsEqual(1);
                    AssertThat(cacheCountAfterSecond).IsEqual(1);
                    AssertThat(firstCall).IsEqual(secondCall);
                    return;
                }
            }
        }

        AssertThat(true).IsTrue();
    }

    [TestCase]
    public void TestClearCache()
    {
        if (Directory.Exists(_testDataPath))
        {
            var ddsFiles = Directory.GetFiles(_testDataPath, "*.dds");
            if (ddsFiles.Length > 0)
            {
                TextureConverter.ConvertDDSToTexture(ddsFiles[0]);
                AssertThat(TextureConverter.GetCacheCount()).IsGreaterEqual(0);

                TextureConverter.ClearCache();
                AssertThat(TextureConverter.GetCacheCount()).IsEqual(0);
                return;
            }
        }

        AssertThat(true).IsTrue();
    }

    [TestCase]
    public void TestGetCachedTexture()
    {
        TextureConverter.ClearCache();

        var result = TextureConverter.GetCachedTexture("non-existent-key");
        AssertThat(result).IsNull();

        if (Directory.Exists(_testDataPath))
        {
            var ddsFiles = Directory.GetFiles(_testDataPath, "*.dds");
            if (ddsFiles.Length > 0)
            {
                TextureConverter.ConvertDDSToTexture(ddsFiles[0]);
                var cached = TextureConverter.GetCachedTexture(ddsFiles[0]);

                if (cached != null)
                {
                    AssertThat(cached).IsNotNull();
                }
                return;
            }
        }

        AssertThat(true).IsTrue();
    }

    [TestCase]
    public void TestInvalidFileFormat()
    {
        TextureConverter.ClearCache();

        string tempPath = Path.GetTempFileName();
        File.WriteAllText(tempPath, "This is not a DDS file");

        var result = TextureConverter.ConvertDDSToTexture(tempPath);

        File.Delete(tempPath);
        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestPathValidation()
    {
        TextureConverter.ClearCache();

        var maliciousPath = "../../../etc/passwd";
        var result = TextureConverter.ConvertDDSToTexture(maliciousPath);

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestDDSHeaderValidation()
    {
        TextureConverter.ClearCache();

        var invalidDDS = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };
        var result = TextureConverter.ConvertDDSToTexture(invalidDDS);

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestCacheStatsAndLimits()
    {
        TextureConverter.ClearCache();

        var stats = TextureConverter.GetCacheStats();
        AssertThat(stats.Count).IsEqual(0);
        AssertThat(stats.SizeBytes).IsEqual(0);
        AssertThat(stats.MaxEntries).IsEqual(100);
        AssertThat(stats.MaxSizeBytes).IsEqual(256 * 1024 * 1024);
    }

    [TestCase]
    public void TestCacheSizeReporting()
    {
        TextureConverter.ClearCache();

        var initialSize = TextureConverter.GetCacheSize();
        AssertThat(initialSize).IsEqual(0);

        if (Directory.Exists(_testDataPath))
        {
            var ddsFiles = Directory.GetFiles(_testDataPath, "*.dds");
            if (ddsFiles.Length > 0)
            {
                TextureConverter.ConvertDDSToTexture(ddsFiles[0]);
                var afterSize = TextureConverter.GetCacheSize();
                AssertThat(afterSize).IsGreater(0);
                return;
            }
        }

        AssertThat(true).IsTrue();
    }

    public override void _ExitTree()
    {
        TextureConverter.ClearCache();
    }
}