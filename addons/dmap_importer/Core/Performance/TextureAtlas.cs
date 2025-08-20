using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using DMapGodot.Importers;

namespace DMapImporter.Core.Performance
{
    public struct AtlasEntry
    {
        public Rect2I SourceRect;
        public Rect2 UVRect;
        public string OriginalPath;
        
        public AtlasEntry(Rect2I sourceRect, Rect2 uvRect, string path)
        {
            SourceRect = sourceRect;
            UVRect = uvRect;
            OriginalPath = path;
        }
    }
    
    public class TextureAtlas
    {
        private const int ATLAS_SIZE = 2048;
        private const int PADDING = 2; // Prevent texture bleeding
        
        private Dictionary<string, AtlasEntry> _textureMap = new();
        private ImageTexture? _atlasTexture;
        private Image? _atlasImage;
        private Vector2I _currentPosition = Vector2I.Zero;
        private int _currentRowHeight = 0;
        private bool _isFinalized = false;
        
        public TextureAtlas()
        {
            _atlasImage = Image.CreateEmpty(ATLAS_SIZE, ATLAS_SIZE, false, Image.Format.Rgba8);
            _atlasImage.Fill(Colors.Transparent);
        }
        
        public bool AddTexture(string texturePath, ImageTexture texture)
        {
            if (_isFinalized)
            {
                GD.PrintErr("Cannot add textures to finalized atlas");
                return false;
            }
            
            if (string.IsNullOrEmpty(texturePath))
            {
                GD.PrintErr("Texture path cannot be null or empty");
                return false;
            }
            
            if (texture == null)
            {
                GD.PrintErr($"Texture is null for path: {texturePath}");
                return false;
            }
            
            if (_textureMap.ContainsKey(texturePath))
            {
                return true; // Already added
            }
            
            var image = texture.GetImage();
            if (image == null) 
            {
                GD.PrintErr($"Failed to get image from texture: {texturePath}");
                return false;
            }
            
            var size = image.GetSize();
            
            // Validate texture size is reasonable
            if (size.X <= 0 || size.Y <= 0 || size.X > ATLAS_SIZE || size.Y > ATLAS_SIZE)
            {
                GD.PrintErr($"Invalid texture size {size} for {texturePath}. Must be > 0 and <= {ATLAS_SIZE}");
                return false;
            }
            
            // Check if texture fits in current row
            if (_currentPosition.X + size.X + PADDING > ATLAS_SIZE)
            {
                // Move to next row
                _currentPosition.X = 0;
                _currentPosition.Y += _currentRowHeight + PADDING;
                _currentRowHeight = 0;
            }
            
            // Check if we have vertical space
            if (_currentPosition.Y + size.Y > ATLAS_SIZE)
            {
                GD.PrintErr($"Atlas full, cannot add texture: {texturePath}");
                return false;
            }
            
            // Copy texture to atlas
            var sourceRect = new Rect2I(Vector2I.Zero, size);
            var destPos = _currentPosition;
            
            _atlasImage?.BlitRect(image, sourceRect, destPos);
            
            // Calculate UV coordinates
            var uvRect = new Rect2(
                (float)destPos.X / ATLAS_SIZE,
                (float)destPos.Y / ATLAS_SIZE,
                (float)size.X / ATLAS_SIZE,
                (float)size.Y / ATLAS_SIZE
            );
            
            // Store atlas entry
            var entry = new AtlasEntry(
                new Rect2I(destPos, size),
                uvRect,
                texturePath
            );
            
            _textureMap[texturePath] = entry;
            
            // Update position for next texture
            _currentPosition.X += size.X + PADDING;
            _currentRowHeight = Math.Max(_currentRowHeight, size.Y);
            
            return true;
        }
        
        public ImageTexture? FinalizeAtlas()
        {
            if (_atlasImage == null) return null;
            
            _atlasTexture = ImageTexture.CreateFromImage(_atlasImage);
            _isFinalized = true;
            
            GD.Print($"Texture atlas finalized with {_textureMap.Count} textures");
            return _atlasTexture;
        }
        
        public AtlasEntry? GetTextureInfo(string texturePath)
        {
            return _textureMap.GetValueOrDefault(texturePath);
        }
        
        public ImageTexture? GetAtlasTexture()
        {
            return _atlasTexture;
        }
        
        public bool ContainsTexture(string texturePath)
        {
            return _textureMap.ContainsKey(texturePath);
        }
        
        public int GetTextureCount()
        {
            return _textureMap.Count;
        }
        
        public static TextureAtlas CreateFromPaths(string[] texturePaths, string clientPath)
        {
            var atlas = new TextureAtlas();
            
            foreach (var path in texturePaths)
            {
                try
                {
                    // Sanitize path to prevent directory traversal attacks
                    var sanitizedPath = TextureAtlasHelper.SanitizePath(path);
                    var fullPath = Path.Combine(clientPath, sanitizedPath);
                    ImageTexture? texture = null;
                    
                    if (fullPath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                    {
                        texture = TextureConverter.ConvertDDSToTexture(fullPath);
                    }
                    else if (File.Exists(fullPath))
                    {
                        var image = Image.LoadFromFile(fullPath);
                        texture = image != null ? ImageTexture.CreateFromImage(image) : null;
                    }
                    
                    if (texture != null)
                    {
                        atlas.AddTexture(path, texture);
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Failed to add texture to atlas: {path}, Error: {ex.Message}");
                }
            }
            
            atlas.FinalizeAtlas();
            return atlas;
        }
    }
    
    public class TextureAtlasManager
    {
        private Dictionary<string, TextureAtlas> _atlases = new();
        private const int MAX_TEXTURES_PER_ATLAS = 64;
        
        public TextureAtlas CreateAtlas(string atlasName, string[] texturePaths, string clientPath)
        {
            var atlas = TextureAtlas.CreateFromPaths(texturePaths, clientPath);
            _atlases[atlasName] = atlas;
            return atlas;
        }
        
        public TextureAtlas? GetAtlas(string atlasName)
        {
            return _atlases.GetValueOrDefault(atlasName);
        }
        
        public void ClearAtlases()
        {
            _atlases.Clear();
        }
    }
    
    public static class TextureAtlasHelper
    {
        public static string SanitizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            
            // Remove directory traversal attempts
            var sanitized = path.Replace("..", "").Replace("//", "/").Replace("\\\\", "\\");
            
            // Remove leading slashes/backslashes to prevent absolute path access
            sanitized = sanitized.TrimStart('/', '\\');
            
            // Normalize path separators
            sanitized = sanitized.Replace('\\', '/');
            
            return sanitized;
        }
    }
}