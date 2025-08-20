using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Microsoft.Extensions.Logging;

namespace DMapGodot.Importers;

public static class TextureConverter
{
    private const int MaxCacheEntries = 100;
    private const long MaxCacheSizeBytes = 256 * 1024 * 1024; // 256MB

    private static readonly Dictionary<string, CacheEntry> _textureCache = new();
    private static readonly LinkedList<string> _lruOrder = new();
    private static long _currentCacheSize = 0;
    private static ILogger? _logger;

    private struct CacheEntry
    {
        public ImageTexture Texture { get; }
        public long SizeBytes { get; }
        public LinkedListNode<string> LruNode { get; set; }

        public CacheEntry(ImageTexture texture, long sizeBytes, LinkedListNode<string> lruNode)
        {
            Texture = texture;
            SizeBytes = sizeBytes;
            LruNode = lruNode;
        }
    }

    public static void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    private static bool IsPathSafe(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var allowedPaths = new[] {
                ProjectSettings.GlobalizePath("res://"),
                ProjectSettings.GlobalizePath("user://"),
                Path.GetTempPath()
            };

            return allowedPaths.Any(allowed => fullPath.StartsWith(allowed, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Invalid path detected: {Path}", path);
            return false;
        }
    }

    private static bool IsValidDDSHeader(byte[] data)
    {
        if (data.Length < 4) return false;
        return data[0] == 0x44 && data[1] == 0x44 &&
               data[2] == 0x53 && data[3] == 0x20; // "DDS "
    }

    private static uint ComputeFastHash(byte[] data)
    {
        uint hash = 2166136261u; // FNV-1a offset basis
        foreach (byte b in data)
        {
            hash = (hash ^ b) * 16777619u; // FNV-1a prime
        }
        return hash;
    }

    private static void EvictLruEntry()
    {
        if (_lruOrder.Count == 0) return;

        var oldestKey = _lruOrder.Last!.Value;
        _lruOrder.RemoveLast();

        if (_textureCache.TryGetValue(oldestKey, out var entry))
        {
            _currentCacheSize -= entry.SizeBytes;
            _textureCache.Remove(oldestKey);
            _logger?.LogDebug("Evicted texture from cache: {Key}, size: {Size} bytes", oldestKey, entry.SizeBytes);
        }
    }

    private static long EstimateTextureSize(ImageTexture texture)
    {
        return texture.GetWidth() * texture.GetHeight() * 4; // Assume RGBA32
    }

    private static void AddToCache(string key, ImageTexture texture)
    {
        var sizeBytes = EstimateTextureSize(texture);

        while ((_textureCache.Count >= MaxCacheEntries || _currentCacheSize + sizeBytes > MaxCacheSizeBytes)
               && _textureCache.Count > 0)
        {
            EvictLruEntry();
        }

        var lruNode = _lruOrder.AddFirst(key);
        var entry = new CacheEntry(texture, sizeBytes, lruNode);
        _textureCache[key] = entry;
        _currentCacheSize += sizeBytes;

        _logger?.LogDebug("Added texture to cache: {Key}, size: {Size} bytes, cache count: {Count}",
                         key, sizeBytes, _textureCache.Count);
    }

    private static ImageTexture? GetFromCache(string key)
    {
        if (!_textureCache.TryGetValue(key, out var entry)) return null;

        // Move to front of LRU list
        _lruOrder.Remove(entry.LruNode);
        var newNode = _lruOrder.AddFirst(key);
        entry.LruNode = newNode;
        _textureCache[key] = entry;

        _logger?.LogDebug("Retrieved cached texture for {Key}", key);
        return entry.Texture;
    }

    public static ImageTexture? ConvertDDSToTexture(string ddsPath)
    {
        if (string.IsNullOrEmpty(ddsPath))
        {
            _logger?.LogWarning("DDS path is null or empty");
            return null;
        }

        if (!IsPathSafe(ddsPath))
        {
            _logger?.LogError("Unsafe path detected, potential directory traversal: {Path}", ddsPath);
            return null;
        }

        var cachedTexture = GetFromCache(ddsPath);
        if (cachedTexture != null) return cachedTexture;

        if (!File.Exists(ddsPath))
        {
            _logger?.LogError("DDS file not found: {Path}", ddsPath);
            return null;
        }

        try
        {
            var image = Image.LoadFromFile(ddsPath);
            if (image != null && !image.IsEmpty())
            {
                var texture = ImageTexture.CreateFromImage(image);
                AddToCache(ddsPath, texture);
                _logger?.LogDebug("Successfully converted DDS to texture: {Path}", ddsPath);
                return texture;
            }
            else
            {
                _logger?.LogError("Failed to load image from DDS file: {Path}", ddsPath);
                return null;
            }
        }
        catch (System.Exception ex)
        {
            _logger?.LogError(ex, "Exception while converting DDS file: {Path}", ddsPath);
            return null;
        }
    }

    public static ImageTexture? ConvertDDSToTexture(byte[] ddsData)
    {
        if (ddsData == null || ddsData.Length == 0)
        {
            _logger?.LogWarning("DDS data is null or empty");
            return null;
        }

        if (!IsValidDDSHeader(ddsData))
        {
            _logger?.LogError("Invalid DDS header detected");
            return null;
        }

        var hash = ComputeFastHash(ddsData);
        var cacheKey = $"data_{hash:x8}";

        var cachedTexture = GetFromCache(cacheKey);
        if (cachedTexture != null) return cachedTexture;

        string tempPath = Path.Combine(
            Path.GetTempPath(),
            $"dmap_{Guid.NewGuid():N}.dds"
        );

        try
        {
            File.WriteAllBytes(tempPath, ddsData);
            var result = ConvertDDSToTextureInternal(tempPath);

            if (result != null)
            {
                AddToCache(cacheKey, result);
            }

            return result;
        }
        catch (System.Exception ex)
        {
            _logger?.LogError(ex, "Exception while converting DDS data to texture");
            return null;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (System.Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to delete temporary file: {Path}", tempPath);
                }
            }
        }
    }

    private static ImageTexture? ConvertDDSToTextureInternal(string ddsPath)
    {
        try
        {
            var image = Image.LoadFromFile(ddsPath);
            if (image != null && !image.IsEmpty())
            {
                var texture = ImageTexture.CreateFromImage(image);
                _logger?.LogDebug("Successfully converted DDS to texture internally: {Path}", ddsPath);
                return texture;
            }
            else
            {
                _logger?.LogError("Failed to load image from DDS file internally: {Path}", ddsPath);
                return null;
            }
        }
        catch (System.Exception ex)
        {
            _logger?.LogError(ex, "Exception while converting DDS file internally: {Path}", ddsPath);
            return null;
        }
    }

    public static ImageTexture? GetCachedTexture(string cacheKey)
    {
        return GetFromCache(cacheKey);
    }

    public static void ClearCache()
    {
        _logger?.LogDebug("Clearing texture cache with {Count} entries, {Size} bytes",
                         _textureCache.Count, _currentCacheSize);
        _textureCache.Clear();
        _lruOrder.Clear();
        _currentCacheSize = 0;
    }

    public static int GetCacheCount()
    {
        return _textureCache.Count;
    }

    public static long GetCacheSize()
    {
        return _currentCacheSize;
    }

    public static (int Count, long SizeBytes, int MaxEntries, long MaxSizeBytes) GetCacheStats()
    {
        return (_textureCache.Count, _currentCacheSize, MaxCacheEntries, MaxCacheSizeBytes);
    }
}