using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using DMapImporter.Core.Performance;
using DMapImporter.Core.Logging;
using DMapGodot.Importers;
using Microsoft.Extensions.Logging;
using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace DMapImporter.Nodes
{
    public class CoordinateHelper
    {
        private CordConverter _converter;
        private Vector2I _mapSize;

        public CoordinateHelper(DmapFile dmapFile)
        {
            var dmapSize = new System.Drawing.Size(
                (int)dmapFile.SizeTiles.Width,
                (int)dmapFile.SizeTiles.Height
            );

            // Background size will be calculated from puzzle file
            var bgSize = new System.Drawing.Size(256, 256); // Placeholder

            _converter = new CordConverter(dmapSize, bgSize);
            _mapSize = new Vector2I(dmapSize.Width, dmapSize.Height);
        }

        public Vector2 TileToLocal(int x, int y)
        {
            var worldPos = _converter.Cell2World(
                new System.Drawing.Point(x, y)
            );
            return new Vector2(worldPos.X, worldPos.Y);
        }

        public Vector2I LocalToTile(Vector2 localPos)
        {
            var cellPos = _converter.World2Cell(
                new System.Drawing.Point((int)localPos.X, (int)localPos.Y)
            );
            return new Vector2I(cellPos.X, cellPos.Y);
        }
    }

    [Tool]
    public partial class DMapRenderer : Node2D
    {
        private static readonly ILogger<DMapRenderer> _logger = DMapLoggerFactory.CreateLogger<DMapRenderer>();
        [Export] public string DMapPath { get; set; } = string.Empty;
        [Export] public Vector2I MapSize { get; set; }
        [Export] public int TileSize { get; set; } = 32;
        [Export] public bool EnableOptimizations { get; set; } = true;
        [Export] public bool EnableChunking { get; set; } = true;
        [Export] public bool EnableViewportCulling { get; set; } = true;
        [Export] public bool EnableLOD { get; set; } = true;
        [Export] public bool EnableObjectPooling { get; set; } = true;
        [Export] public bool ShowPerformanceStats { get; set; } = false;

        private DmapFile? _dmapFile;
        private TileMapLayer? _backgroundLayer;
        private TileMapLayer? _terrainLayer;
        private Node2D? _objectLayer;
        private CoordinateHelper? _coordinateHelper;
        private CordConverter? _cordConverter;
        private Dictionary<string, SceneFile> _loadedSceneFiles = new();
        private string _clientPath = string.Empty;

        // Performance optimization components
        private ViewportCuller? _viewportCuller;
        private ChunkManager? _chunkManager;
        private LODSystem? _lodSystem;
        private SpritePool? _spritePool;
        private MarkerPool? _markerPool;
        private TextureAtlasManager? _textureAtlasManager;
        private PerformanceMonitor? _performanceMonitor;

        // ANI file caching and animation support
        private Dictionary<string, AniFile> _aniCache = new();
        private Dictionary<int, ImageTexture[]> _objectFrames = new();
        private Dictionary<int, double> _lastFrameUpdate = new();
        private Dictionary<int, int> _currentFrame = new();
        private int _nextObjectId = 0;
        private Camera2D? _camera;
        private Vector2 _lastCameraPosition = Vector2.Zero;
        private Vector2 _lastCameraZoom = Vector2.One;
        private bool _cameraChanged = true;

        public override void _Ready()
        {
            if (Engine.IsEditorHint())
            {
                SetNotifyTransform(true);
            }

            InitializePerformanceComponents();

            // Auto-load DMAP if DMapPath is set and no data is loaded
            if (!string.IsNullOrEmpty(DMapPath) && _dmapFile == null)
            {
                GD.Print($"[DMapRenderer] Auto-loading DMAP from: {DMapPath}");
                CallDeferred(nameof(LoadDMapFromPath), DMapPath);
            }
        }

        public override void _Process(double delta)
        {
            if (!EnableOptimizations) return;

            _performanceMonitor?.StartFrame();

            UpdatePerformanceComponents(delta);
            UpdateAnimations(delta);

            _performanceMonitor?.EndFrame();
            _performanceMonitor?.Update(delta);

            if (ShowPerformanceStats && _performanceMonitor != null)
            {
                // Log performance stats periodically (every 5 seconds)
                if (Engine.GetProcessFrames() % (60 * 5) == 0)
                {
                    _performanceMonitor.LogPerformanceReport();
                }
            }
        }

        private void InitializePerformanceComponents()
        {
            if (!EnableOptimizations) return;

            // Find camera (look for Camera2D in scene tree)
            _camera = GetViewport()?.GetCamera2D();
            if (_camera == null)
            {
                // Create a default camera if none exists
                _camera = new Camera2D();
                // Use call_deferred to avoid "Parent node is busy" error
                GetParent()?.CallDeferred("add_child", _camera);
                _logger.LogInformation("Created default Camera2D for DMapRenderer optimizations");
            }

            // Initialize performance components
            if (EnableViewportCulling && _camera != null)
            {
                _viewportCuller = new ViewportCuller(_camera);
            }

            if (EnableLOD && _camera != null)
            {
                _lodSystem = new LODSystem(_camera);
            }

            if (EnableObjectPooling)
            {
                _spritePool = new SpritePool(this);
                _markerPool = new MarkerPool(this);
            }

            _textureAtlasManager = new TextureAtlasManager();
            _performanceMonitor = new PerformanceMonitor();

            ValidateConfiguration();
        }

        private void ValidateConfiguration()
        {
            if (!EnableOptimizations) return;

            if ((EnableLOD || EnableViewportCulling) && _camera == null)
            {
                _logger.LogWarning("LOD or Viewport Culling enabled but no camera found. Creating default camera");
            }

            if (EnableChunking && MapSize.X * MapSize.Y < 65536) // 256x256
            {
                _logger.LogWarning("Chunking enabled for small map. Consider disabling for better performance");
            }

            if (EnableObjectPooling && !EnableChunking && !EnableViewportCulling)
            {
                _logger.LogWarning("Object pooling most effective when combined with culling systems");
            }

            if (ShowPerformanceStats && !EnableOptimizations)
            {
                _logger.LogWarning("Performance stats enabled but optimizations disabled");
            }
        }

        private void UpdatePerformanceComponents(double delta)
        {
            // Check if camera has moved to avoid unnecessary updates
            bool cameraChanged = CheckCameraChanged();

            if (_viewportCuller != null && (cameraChanged || _cameraChanged))
            {
                _viewportCuller.UpdateCullingBounds();
            }

            if (_chunkManager != null && _viewportCuller != null && (cameraChanged || _cameraChanged))
            {
                _chunkManager.UpdateVisibleChunks(_viewportCuller, new Vector2I(64, 32));
                _cameraChanged = false; // Reset flag after update
            }

            if (_lodSystem != null)
            {
                _lodSystem.Update(delta);
            }

            if (_performanceMonitor != null)
            {
                _performanceMonitor.SetReferences(_chunkManager, _lodSystem, _spritePool, _markerPool);

                // Update visible counts
                int visibleTiles = CountVisibleTiles();
                int visibleObjects = CountVisibleObjects();
                _performanceMonitor.SetVisibleCounts(visibleTiles, visibleObjects);
            }
        }

        private void UpdateAnimations(double delta)
        {
            if (_objectFrames.Count == 0) return;

            double currentTime = Time.GetUnixTimeFromSystem();
            var objectsToUpdate = new List<int>();

            // Find objects that need frame updates
            foreach (var kvp in _objectFrames)
            {
                int objectId = kvp.Key;
                var frames = kvp.Value;

                // Skip single-frame animations
                if (frames.Length <= 1) continue;

                // Check if enough time has passed for frame update
                if (_lastFrameUpdate.TryGetValue(objectId, out double lastUpdate))
                {
                    // Find the sprite with this object ID
                    var sprite = FindSpriteByObjectId(objectId);
                    if (sprite != null)
                    {
                        uint animationInterval = (uint)sprite.GetMeta("animation_interval", 500u).AsUInt32();

                        // Convert milliseconds to seconds
                        double intervalSeconds = animationInterval / 1000.0;

                        if (currentTime - lastUpdate >= intervalSeconds)
                        {
                            objectsToUpdate.Add(objectId);
                        }
                    }
                }
            }

            // Update frames for objects that need it
            foreach (int objectId in objectsToUpdate)
            {
                if (_objectFrames.TryGetValue(objectId, out var frames) &&
                    _currentFrame.TryGetValue(objectId, out int currentFrame))
                {
                    // Advance to next frame
                    int nextFrame = (currentFrame + 1) % frames.Length;
                    _currentFrame[objectId] = nextFrame;
                    _lastFrameUpdate[objectId] = currentTime;

                    // Update the sprite texture
                    var sprite = FindSpriteByObjectId(objectId);
                    if (sprite != null)
                    {
                        sprite.Texture = frames[nextFrame];
                    }
                }
            }
        }

        private Sprite2D? FindSpriteByObjectId(int objectId)
        {
            return FindSpriteByObjectIdRecursive(this, objectId);
        }

        private Sprite2D? FindSpriteByObjectIdRecursive(Node node, int objectId)
        {
            if (node is Sprite2D sprite && sprite.HasMeta("object_id"))
            {
                if (sprite.GetMeta("object_id").AsInt32() == objectId)
                {
                    return sprite;
                }
            }

            foreach (Node child in node.GetChildren())
            {
                var result = FindSpriteByObjectIdRecursive(child, objectId);
                if (result != null) return result;
            }

            return null;
        }

        private int CountVisibleTiles()
        {
            if (_terrainLayer == null) return 0;

            int count = 0;
            var usedCells = _terrainLayer.GetUsedCells();
            foreach (var cell in usedCells)
            {
                if (_terrainLayer.GetCellSourceId(cell) >= 0) count++;
            }
            return count;
        }

        private int CountVisibleObjects()
        {
            if (_objectLayer == null) return 0;

            int count = 0;
            foreach (Node child in _objectLayer.GetChildren())
            {
                if (child is CanvasItem canvasItem && canvasItem.Visible) count++;
            }
            return count;
        }

        private bool CheckCameraChanged()
        {
            if (_camera == null) return false;

            var currentPosition = _camera.GlobalPosition;
            var currentZoom = _camera.Zoom;

            bool changed = !currentPosition.IsEqualApprox(_lastCameraPosition) ||
                          !currentZoom.IsEqualApprox(_lastCameraZoom);

            if (changed)
            {
                _lastCameraPosition = currentPosition;
                _lastCameraZoom = currentZoom;
            }

            return changed;
        }

        public void LoadFromDMap(DmapFile dmap)
        {
            if (dmap == null)
            {
                var error = "Cannot load null DmapFile";
                _logger.LogError(error);
                GD.PrintErr($"[DMapRenderer ERROR] {error}");
                return;
            }

            GD.Print("[DMapRenderer] Starting LoadFromDMap...");
            _dmapFile = dmap;
            DMapPath = dmap.DmapPath;
            MapSize = new Vector2I((int)dmap.SizeTiles.Width, (int)dmap.SizeTiles.Height);

            GD.Print($"[DMapRenderer] DMAP loaded: {DMapPath}, Size: {MapSize}");

            // Extract client path from DMapPath
            _clientPath = ExtractClientPath(dmap.DmapPath);

            // Initialize coordinate helper
            _coordinateHelper = new CoordinateHelper(dmap);

            // Initialize CordConverter with correct background size
            var dmapSize = new System.Drawing.Size(
                (int)dmap.SizeTiles.Width,
                (int)dmap.SizeTiles.Height
            );
            
            // Calculate background size from puzzle file if available
            var bgSize = new System.Drawing.Size(256, 256); // Default
            if (!string.IsNullOrEmpty(dmap.PuzzleFile))
            {
                try
                {
                    var puzzleFile = new PuzzleFile(_clientPath, dmap.PuzzleFile);
                    int puzzleWidth = puzzleFile.GetWidth();
                    bgSize = new System.Drawing.Size(
                        (int)(puzzleFile.Size.Width * puzzleWidth),
                        (int)(puzzleFile.Size.Height * puzzleWidth)
                    );
                    GD.Print($"[DMapRenderer] Using puzzle background size: {bgSize.Width}x{bgSize.Height}");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[DMapRenderer] Failed to load puzzle file for background size: {ex.Message}");
                }
            }
            
            _cordConverter = new CordConverter(dmapSize, bgSize);

            try
            {
                GD.Print("[DMapRenderer] Clearing existing children...");
                ClearChildren();

                GD.Print("[DMapRenderer] Creating layers...");
                CreateLayers();
                GD.Print("[DMapRenderer] Layers created successfully");
                
                // Position all layers to center the map around world origin
                if (_cordConverter != null)
                {
                    var bgWorldPos = _cordConverter.GetBackgroundWorldPos();
                    
                    // Position background layer
                    if (_backgroundLayer != null)
                    {
                        _backgroundLayer.Position = new Vector2(bgWorldPos.X, bgWorldPos.Y);
                        GD.Print($"[DMapRenderer] Positioned background layer at world ({bgWorldPos.X}, {bgWorldPos.Y})");
                    }
                    
                    // Position terrain layer at the same position
                    if (_terrainLayer != null)
                    {
                        _terrainLayer.Position = new Vector2(bgWorldPos.X, bgWorldPos.Y);
                        GD.Print($"[DMapRenderer] Positioned terrain layer at world ({bgWorldPos.X}, {bgWorldPos.Y})");
                    }
                    
                    // Position object layer at the same position
                    if (_objectLayer != null)
                    {
                        _objectLayer.Position = new Vector2(bgWorldPos.X, bgWorldPos.Y);
                        GD.Print($"[DMapRenderer] Positioned object layer at world ({bgWorldPos.X}, {bgWorldPos.Y})");
                    }
                    
                    // Also position the sky backdrop layer if it exists
                    var skyLayer = GetNodeOrNull<TileMapLayer>("SkyBackdropLayer");
                    if (skyLayer != null)
                    {
                        skyLayer.Position = new Vector2(bgWorldPos.X, bgWorldPos.Y);
                        GD.Print($"[DMapRenderer] Positioned sky layer at world ({bgWorldPos.X}, {bgWorldPos.Y})");
                    }
                }

                GD.Print("[DMapRenderer] Creating scene layer management...");
                CreateSceneLayerManagement();
                GD.Print("[DMapRenderer] Scene layer management created successfully");

                GD.Print("[DMapRenderer] Creating selection layer...");
                CreateSelectionLayer();
                GD.Print("[DMapRenderer] Selection layer created successfully");

                // Initialize chunk manager if enabled
                if (EnableChunking && EnableOptimizations)
                {
                    GD.Print("[DMapRenderer] Initializing chunk manager...");
                    _chunkManager = new ChunkManager(dmap, _objectLayer!);
                }

                GD.Print("[DMapRenderer] Populating from DMAP...");
                PopulateFromDMap();

                GD.Print("[DMapRenderer] LoadFromDMap completed successfully");
            }
            catch (Exception ex)
            {
                var error = $"Error in LoadFromDMap: {ex.Message}";
                _logger.LogError(ex, error);
                GD.PrintErr($"[DMapRenderer ERROR] {error}");
                GD.PrintErr($"[DMapRenderer ERROR] Exception type: {ex.GetType().Name}");
                GD.PrintErr($"[DMapRenderer ERROR] Stack trace: {ex.StackTrace}");
                throw; // Re-throw to let the importer handle it
            }
        }

        public void LoadDMapFromPath(string dmapPath)
        {
            try
            {
                if (string.IsNullOrEmpty(dmapPath))
                {
                    GD.PrintErr("[DMapRenderer] Cannot load DMAP - path is null or empty");
                    return;
                }

                GD.Print($"[DMapRenderer] LoadDMapFromPath called with: {dmapPath}");

                // Convert Godot resource path to filesystem path
                string absolutePath = ProjectSettings.GlobalizePath(dmapPath);
                GD.Print($"[DMapRenderer] Converted to absolute path: {absolutePath}");

                if (!System.IO.File.Exists(absolutePath))
                {
                    GD.PrintErr($"[DMapRenderer] DMAP file not found: {absolutePath}");
                    return;
                }

                // Load the DMAP file
                var dmapFile = new DmapFile(absolutePath);
                GD.Print($"[DMapRenderer] Successfully loaded DMAP file: {dmapFile.DmapName}");

                // Load it into the renderer
                LoadFromDMap(dmapFile);
                GD.Print("[DMapRenderer] Auto-load completed successfully");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[DMapRenderer] Error in LoadDMapFromPath: {ex.Message}");
                GD.PrintErr($"[DMapRenderer] Stack trace: {ex.StackTrace}");
            }
        }

        private void ClearChildren()
        {
            foreach (Node child in GetChildren())
            {
                child.QueueFree();
            }
        }

        public override void _ExitTree()
        {
            // Cleanup performance components
            _performanceMonitor?.Dispose();
            _lodSystem?.ClearObjects();

            base._ExitTree();
        }

        private void CreateLayers()
        {
            try
            {
                // Create sky/backdrop layer for scene layer puzzles (far background)
                GD.Print("[DMapRenderer] Creating sky backdrop layer...");
                var skyLayer = new TileMapLayer();
                skyLayer.Name = "SkyBackdropLayer";
                skyLayer.ZIndex = -1; // Behind everything
                skyLayer.Enabled = true;
                AddChild(skyLayer);
                GD.Print("[DMapRenderer] Sky backdrop layer added successfully");

                GD.Print("[DMapRenderer] Creating background layer...");
                _backgroundLayer = new TileMapLayer();
                _backgroundLayer.Name = "BackgroundLayer";
                _backgroundLayer.ZIndex = 0;
                _backgroundLayer.Enabled = true;

                GD.Print("[DMapRenderer] Adding background layer to scene...");
                AddChild(_backgroundLayer);
                GD.Print("[DMapRenderer] Background layer added successfully");

                GD.Print("[DMapRenderer] Creating terrain layer...");
                _terrainLayer = new TileMapLayer();
                _terrainLayer.Name = "TerrainLayer";
                _terrainLayer.ZIndex = 1;
                _terrainLayer.Enabled = true;

                GD.Print("[DMapRenderer] Adding terrain layer to scene...");
                AddChild(_terrainLayer);
                GD.Print("[DMapRenderer] Terrain layer added successfully");

                GD.Print("[DMapRenderer] Creating object layer...");
                _objectLayer = new Node2D();
                _objectLayer.Name = "ObjectLayer";
                _objectLayer.ZIndex = 2;
                _objectLayer.YSortEnabled = true;
                GD.Print("[DMapRenderer] Adding object layer to scene...");
                AddChild(_objectLayer);
                GD.Print("[DMapRenderer] Object layer added successfully");
                GD.Print("[DMapRenderer] Object layer created and added");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[DMapRenderer ERROR] Exception in CreateLayers: {ex.Message}");
                GD.PrintErr($"[DMapRenderer ERROR] Stack trace: {ex.StackTrace}");
                throw;
            }

            // Set owner for editor visibility - only if we're in a proper scene tree
            // Skip owner assignment during import as GetTree() may be null
            if (Engine.IsEditorHint() && GetTree() != null)
            {
                try
                {
                    var root = GetTree()?.EditedSceneRoot;
                    if (root != null)
                    {
                        GD.Print("[DMapRenderer] Setting owners for editor visibility");
                        if (_backgroundLayer != null) _backgroundLayer.Owner = root;
                        if (_terrainLayer != null) _terrainLayer.Owner = root;
                        if (_objectLayer != null) _objectLayer.Owner = root;
                        GD.Print("[DMapRenderer] Owner assignment completed");
                    }
                    else
                    {
                        GD.Print("[DMapRenderer] No EditedSceneRoot found - skipping owner assignment");
                    }
                }
                catch (Exception ex)
                {
                    GD.Print($"[DMapRenderer] Owner assignment failed: {ex.Message} - continuing without owners");
                }
            }
            else
            {
                GD.Print("[DMapRenderer] Not in editor or no scene tree - skipping owner assignment");
            }

            // Create and assign separate TileSets
            GD.Print("[DMapRenderer] Creating puzzle TileSet...");
            var puzzleTileSet = CreatePuzzleTileSet();
            GD.Print("[DMapRenderer] Creating terrain TileSet...");
            var terrainTileSet = CreateTerrainTileSet();

            if (_backgroundLayer != null && puzzleTileSet != null)
            {
                _backgroundLayer.TileSet = puzzleTileSet;
                GD.Print($"[DMapRenderer] Assigned puzzle TileSet to background layer with {puzzleTileSet.GetSourceCount()} sources");

                // Verify assignment and log detailed info
                if (_backgroundLayer.TileSet != null)
                {
                    GD.Print($"[DMapRenderer] Background TileSet assignment verified successfully");
                    if (puzzleTileSet.GetSourceCount() > 0)
                    {
                        var source = puzzleTileSet.GetSource(0) as TileSetAtlasSource;
                        if (source != null)
                        {
                            GD.Print($"[DMapRenderer] Background atlas has {source.GetTilesCount()} tiles");
                        }
                    }
                }
                else
                {
                    GD.PrintErr("[DMapRenderer] Background TileSet assignment failed - TileSet is null after assignment");
                }
            }
            else
            {
                GD.PrintErr($"[DMapRenderer] Failed to assign puzzle TileSet - backgroundLayer null: {_backgroundLayer == null}, puzzleTileSet null: {puzzleTileSet == null}");
            }

            if (_terrainLayer != null && terrainTileSet != null)
            {
                _terrainLayer.TileSet = terrainTileSet;
                GD.Print($"[DMapRenderer] Assigned terrain TileSet to terrain layer with {terrainTileSet.GetSourceCount()} sources");

                // Verify assignment and log detailed info
                if (_terrainLayer.TileSet != null)
                {
                    GD.Print($"[DMapRenderer] Terrain TileSet assignment verified successfully");
                    if (terrainTileSet.GetSourceCount() > 0)
                    {
                        var source = terrainTileSet.GetSource(0) as TileSetAtlasSource;
                        if (source != null)
                        {
                            GD.Print($"[DMapRenderer] Terrain atlas has {source.GetTilesCount()} tiles");
                        }
                    }
                }
                else
                {
                    GD.PrintErr("[DMapRenderer] Terrain TileSet assignment failed - TileSet is null after assignment");
                }
            }
            else
            {
                GD.PrintErr($"[DMapRenderer] Failed to assign terrain TileSet - terrainLayer null: {_terrainLayer == null}, terrainTileSet null: {terrainTileSet == null}");
            }
        }

        private TileSet CreateTerrainTileSet()
        {
            var tileSet = new TileSet();
            tileSet.TileShape = TileSet.TileShapeEnum.Isometric;
            tileSet.TileSize = new Vector2I(64, 32);
            tileSet.TileLayout = TileSet.TileLayoutEnum.Stacked;

            // Add custom data layers (from Tile.cs structure)
            tileSet.AddCustomDataLayer();
            tileSet.SetCustomDataLayerName(0, "no_access");
            tileSet.SetCustomDataLayerType(0, Variant.Type.Bool);

            tileSet.AddCustomDataLayer();
            tileSet.SetCustomDataLayerName(1, "surface");
            tileSet.SetCustomDataLayerType(1, Variant.Type.Int);

            tileSet.AddCustomDataLayer();
            tileSet.SetCustomDataLayerName(2, "height");
            tileSet.SetCustomDataLayerType(2, Variant.Type.Int);

            // Create dynamic terrain tiles from terrain scenes
            if (_dmapFile != null && _dmapFile.TerrainScenes.Count > 0)
            {
                try
                {
                    var terrainTextures = new Dictionary<string, ImageTexture>();
                    var scenePartInfo = new List<(string key, ScenePart scenePart, string sceneFile)>();

                    GD.Print($"[DMapRenderer] Creating terrain TileSet from {_dmapFile.TerrainScenes.Count} terrain scenes");

                    // Collect all unique terrain scene parts and their textures
                    foreach (var terrainScene in _dmapFile.TerrainScenes)
                    {
                        if (string.IsNullOrEmpty(terrainScene.SceneFile)) continue;

                        try
                        {
                            var sceneFile = new SceneFile(_clientPath, terrainScene.SceneFile);

                            foreach (var scenePart in sceneFile.SceneParts)
                            {
                                if (string.IsNullOrEmpty(scenePart.AniPath) || string.IsNullOrEmpty(scenePart.AniName))
                                    continue;

                                // Create a unique scene key that isn't empty
                                string sceneKey = $"{scenePart.AniPath}_{scenePart.AniName}";

                                // Validate the scene key is not empty or just underscores
                                if (sceneKey.Length <= 1 || sceneKey.Trim('_').Length == 0)
                                {
                                    GD.PrintErr($"[DMapRenderer] Invalid scene key generated: '{sceneKey}' from AniPath='{scenePart.AniPath}', AniName='{scenePart.AniName}'");
                                    continue;
                                }

                                if (!terrainTextures.ContainsKey(sceneKey))
                                {
                                    var frames = LoadAniFrames(scenePart.AniPath, scenePart.AniName);
                                    if (frames != null && frames.Length > 0)
                                    {
                                        terrainTextures[sceneKey] = frames[0]; // Use first frame
                                        scenePartInfo.Add((sceneKey, scenePart, terrainScene.SceneFile));
                                        GD.Print($"[DMapRenderer] Loaded terrain texture for {sceneKey}");
                                    }
                                    else
                                    {
                                        GD.PrintErr($"[DMapRenderer] Failed to load frames for scene key: {sceneKey}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            GD.PrintErr($"[DMapRenderer] Error loading terrain scene {terrainScene.SceneFile}: {ex.Message}");
                        }
                    }

                    if (terrainTextures.Count > 0)
                    {
                        // Get actual terrain texture size from the first texture
                        var firstTexture = terrainTextures.Values.First();
                        var textureSize = new Vector2I(firstTexture.GetWidth(), firstTexture.GetHeight());
                        GD.Print($"[DMapRenderer] Terrain texture size: {textureSize.X}x{textureSize.Y}");
                        
                        // Create atlas from terrain textures using actual size
                        var atlasTexture = CreateTerrainAtlasFromTextures(terrainTextures, textureSize.X);
                        var source = new TileSetAtlasSource();
                        
                        // IMPORTANT: Set texture BEFORE creating tiles
                        source.Texture = atlasTexture;
                        source.TextureRegionSize = textureSize;
                        
                        GD.Print($"[DMapRenderer] Atlas texture assigned: {atlasTexture.GetWidth()}x{atlasTexture.GetHeight()}, region size: {textureSize}");

                        // Create tiles for each terrain texture in the atlas
                        int atlasX = 0, atlasY = 0;
                        int tilesPerRow = atlasTexture.GetWidth() / textureSize.X;
                        
                        // Store scene metadata for later
                        var sceneMetadata = new Dictionary<Vector2I, (string key, ScenePart scenePart, string sceneFile)>();

                        foreach (var info in scenePartInfo)
                        {
                            var atlasCoords = new Vector2I(atlasX, atlasY);

                            // Create the tile (texture must already be assigned)
                            source.CreateTile(atlasCoords);
                            
                            // Store metadata for later use
                            sceneMetadata[atlasCoords] = info;
                            
                            if (sceneMetadata.Count <= 10) // Log first few
                            {
                                GD.Print($"[DMapRenderer] Created terrain tile at atlas coords ({atlasX}, {atlasY}) for scene key: {info.key}");
                            }

                            // Move to next position in atlas
                            atlasX++;
                            if (atlasX >= tilesPerRow)
                            {
                                atlasX = 0;
                                atlasY++;
                            }
                        }
                        
                        GD.Print($"[DMapRenderer] Created {sceneMetadata.Count} terrain tiles in atlas");

                        // Add custom data layers for terrain scene info
                        tileSet.AddCustomDataLayer();
                        tileSet.SetCustomDataLayerName(3, "scene_key");
                        tileSet.SetCustomDataLayerType(3, Variant.Type.String);

                        tileSet.AddCustomDataLayer();
                        tileSet.SetCustomDataLayerName(4, "ani_path");
                        tileSet.SetCustomDataLayerType(4, Variant.Type.String);

                        tileSet.AddCustomDataLayer();
                        tileSet.SetCustomDataLayerName(5, "ani_name");
                        tileSet.SetCustomDataLayerType(5, Variant.Type.String);

                        tileSet.AddCustomDataLayer();
                        tileSet.SetCustomDataLayerName(6, "scene_file");
                        tileSet.SetCustomDataLayerType(6, Variant.Type.String);

                        tileSet.AddCustomDataLayer();
                        tileSet.SetCustomDataLayerName(7, "interval");
                        tileSet.SetCustomDataLayerType(7, Variant.Type.Int);

                        // Add the source to the tileset FIRST before setting custom data
                        var sourceId = tileSet.AddSource(source, 0);
                        if (sourceId == -1)
                        {
                            GD.PrintErr("[DMapRenderer ERROR] Failed to add atlas source to terrain TileSet");
                        }
                        else
                        {
                            GD.Print($"[DMapRenderer] Successfully added atlas source with ID {sourceId} to terrain TileSet");
                            
                            // NOW set custom data on tiles after source is added to tileset
                            int successCount = 0;
                            foreach (var kvp in sceneMetadata)
                            {
                                var atlasCoords = kvp.Key;
                                var info = kvp.Value;
                                
                                var tileData = source.GetTileData(atlasCoords, 0);
                                if (tileData != null)
                                {
                                    // Store scene part info as custom data
                                    tileData.SetCustomData("scene_key", info.key);
                                    tileData.SetCustomData("ani_path", info.scenePart.AniPath);
                                    tileData.SetCustomData("ani_name", info.scenePart.AniName);
                                    tileData.SetCustomData("scene_file", info.sceneFile);
                                    tileData.SetCustomData("interval", (int)info.scenePart.Interval);
                                    
                                    // Verify scene_key was set correctly
                                    var verifyData = tileData.GetCustomData("scene_key");
                                    if (verifyData.VariantType == Variant.Type.String && verifyData.AsString() == info.key)
                                    {
                                        successCount++;
                                        if (successCount <= 5) // Log first few successes
                                        {
                                            GD.Print($"[DMapRenderer] Verified scene key '{info.key}' stored correctly at atlas position ({atlasCoords.X}, {atlasCoords.Y})");
                                        }
                                    }
                                    else
                                    {
                                        GD.PrintErr($"[DMapRenderer ERROR] Failed to store scene key '{info.key}' at atlas position ({atlasCoords.X}, {atlasCoords.Y})");
                                    }
                                }
                                else
                                {
                                    GD.PrintErr($"[DMapRenderer ERROR] Could not get tile data for terrain atlas coords ({atlasCoords.X}, {atlasCoords.Y})");
                                }
                            }
                            
                            GD.Print($"[DMapRenderer] Successfully set custom data for {successCount}/{sceneMetadata.Count} terrain tiles");
                        }

                        GD.Print($"[DMapRenderer] Created terrain tileset with {source.GetTilesCount()} terrain tiles");
                    }
                    else
                    {
                        GD.PrintErr("[DMapRenderer] No terrain textures loaded, creating fallback");
                        CreateFallbackTerrainTile(tileSet);
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[DMapRenderer] Error creating dynamic terrain tileset: {ex.Message}");
                    CreateFallbackTerrainTile(tileSet);
                }
            }
            else
            {
                GD.Print("[DMapRenderer] No terrain scenes found, creating fallback terrain tileset");
                CreateFallbackTerrainTile(tileSet);
            }

            return tileSet;
        }

        private TileSet CreatePuzzleTileSet()
        {
            if (_dmapFile == null || string.IsNullOrEmpty(_dmapFile.PuzzleFile))
            {
                GD.Print("[DMapRenderer] No puzzle file, creating empty tileset");
                var emptyTileSet = new TileSet();
                emptyTileSet.TileShape = TileSet.TileShapeEnum.Isometric;
                emptyTileSet.TileSize = new Vector2I(256, 256);
                emptyTileSet.TileLayout = TileSet.TileLayoutEnum.Stacked;
                return emptyTileSet;
            }

            try
            {
                GD.Print($"[DMapRenderer] Creating puzzle TileSet from {_dmapFile.PuzzleFile}");

                // Load the puzzle file
                var puzzleFile = new PuzzleFile(_clientPath, _dmapFile.PuzzleFile);
                int puzzleWidth = puzzleFile.GetWidth();

                var tileSet = new TileSet();
                tileSet.TileShape = TileSet.TileShapeEnum.Square; // Puzzle tiles are square
                tileSet.TileSize = new Vector2I(puzzleWidth, puzzleWidth);
                tileSet.TileLayout = TileSet.TileLayoutEnum.Stacked;

                // Add custom data layer for puzzle IDs
                tileSet.AddCustomDataLayer();
                tileSet.SetCustomDataLayerName(0, "puzzle_id");
                tileSet.SetCustomDataLayerType(0, Variant.Type.Int);

                // Create atlas source for puzzle tiles
                var atlasSource = new TileSetAtlasSource();

                // Load ANI file to get available puzzle animations
                if (!string.IsNullOrEmpty(puzzleFile.AniFile))
                {
                    // Get or load ANI file from cache
                    AniFile? aniFile = null;
                    if (!_aniCache.TryGetValue(puzzleFile.AniFile, out aniFile))
                    {
                        aniFile = new AniFile(_clientPath, puzzleFile.AniFile);
                        _aniCache.TryAdd(puzzleFile.AniFile, aniFile);
                    }

                    if (aniFile != null)
                    {
                        // Create a combined texture atlas from all puzzle pieces
                        var puzzleTextures = new Dictionary<int, ImageTexture>();

                        // Collect all unique puzzle tile IDs, excluding 65535 (invalid/empty)
                        var uniquePuzzleIds = new HashSet<ushort>();
                        for (int x = 0; x < puzzleFile.Size.Width; x++)
                        {
                            for (int y = 0; y < puzzleFile.Size.Height; y++)
                            {
                                ushort puzzleId = puzzleFile.PuzzleTiles[x, y];
                                if (puzzleId != 65535) // Skip invalid tiles
                                {
                                    uniquePuzzleIds.Add(puzzleId);
                                }
                            }
                        }

                        GD.Print($"[DMapRenderer] Found {uniquePuzzleIds.Count} unique valid puzzle tiles (excluding 65535)");

                        // Load textures for each unique puzzle ID
                        int loadedCount = 0;
                        int skippedCount = 0;
                        foreach (var puzzleId in uniquePuzzleIds)
                        {
                            string puzzleAniName = $"Puzzle{puzzleId}";
                            var frames = LoadAniFrames(puzzleFile.AniFile, puzzleAniName);

                            if (frames != null && frames.Length > 0)
                            {
                                puzzleTextures[puzzleId] = frames[0]; // Use first frame
                                loadedCount++;

                                if (loadedCount <= 5) // Log first few successes
                                {
                                    GD.Print($"[DMapRenderer] Loaded texture for puzzle tile {puzzleId}");
                                }
                            }
                            else
                            {
                                skippedCount++;

                                // Only log the first few failures to avoid spam
                                if (skippedCount <= 5)
                                {
                                    GD.Print($"[DMapRenderer] No texture found for puzzle tile {puzzleId} (expected for unused tiles)");
                                }
                            }
                        }

                        GD.Print($"[DMapRenderer] Puzzle texture loading completed: {loadedCount} loaded, {skippedCount} skipped (no ANI entries)");

                        if (puzzleTextures.Count > 0)
                        {
                            // Create an atlas texture by combining all puzzle textures
                            var atlasTexture = CreateAtlasFromTextures(puzzleTextures, puzzleWidth);
                            if (atlasTexture != null)
                            {
                                // IMPORTANT: Set texture and region size BEFORE creating tiles
                                atlasSource.Texture = atlasTexture;
                                atlasSource.TextureRegionSize = new Vector2I(puzzleWidth, puzzleWidth);
                                
                                GD.Print($"[DMapRenderer] Puzzle atlas texture assigned: {atlasTexture.GetWidth()}x{atlasTexture.GetHeight()}, region size: {puzzleWidth}x{puzzleWidth}");

                                // Create tiles for each puzzle piece in the atlas
                                int atlasX = 0, atlasY = 0;
                                int tilesPerRow = atlasTexture.GetWidth() / puzzleWidth;
                                
                                // Store puzzle ID mappings for later
                                var puzzleIdMapping = new Dictionary<Vector2I, int>();

                                foreach (var kvp in puzzleTextures)
                                {
                                    int puzzleId = kvp.Key;
                                    var atlasCoords = new Vector2I(atlasX, atlasY);

                                    // Create the tile (texture must already be assigned)
                                    atlasSource.CreateTile(atlasCoords);
                                    
                                    // Store the mapping for later use
                                    puzzleIdMapping[atlasCoords] = puzzleId;
                                    
                                    if (puzzleIdMapping.Count <= 10) // Log first few
                                    {
                                        GD.Print($"[DMapRenderer] Created puzzle tile at atlas coords ({atlasX}, {atlasY}) for puzzle ID: {puzzleId}");
                                    }

                                    // Move to next position in atlas
                                    atlasX++;
                                    if (atlasX >= tilesPerRow)
                                    {
                                        atlasX = 0;
                                        atlasY++;
                                    }
                                }
                                
                                GD.Print($"[DMapRenderer] Created {puzzleIdMapping.Count} puzzle tiles in atlas");
                                
                                // Add the source to the tileset FIRST before setting custom data
                                var sourceId = tileSet.AddSource(atlasSource, 0);
                                if (sourceId == -1)
                                {
                                    GD.PrintErr("[DMapRenderer ERROR] Failed to add atlas source to puzzle TileSet");
                                    return tileSet;
                                }
                                
                                GD.Print($"[DMapRenderer] Added atlas source with ID {sourceId} to puzzle TileSet");
                                
                                // NOW set custom data on tiles after source is added to tileset
                                int successCount = 0;
                                foreach (var kvp in puzzleIdMapping)
                                {
                                    var atlasCoords = kvp.Key;
                                    int puzzleId = kvp.Value;
                                    
                                    var tileData = atlasSource.GetTileData(atlasCoords, 0);
                                    if (tileData != null)
                                    {
                                        // Store puzzle ID as custom data
                                        tileData.SetCustomData("puzzle_id", puzzleId);
                                        
                                        // Verify it was set correctly
                                        var verifyData = tileData.GetCustomData("puzzle_id");
                                        if (verifyData.VariantType == Variant.Type.Int && verifyData.AsInt32() == puzzleId)
                                        {
                                            successCount++;
                                            if (successCount <= 5) // Log first few successes
                                            {
                                                GD.Print($"[DMapRenderer] Verified puzzle ID {puzzleId} stored correctly at atlas position ({atlasCoords.X}, {atlasCoords.Y})");
                                            }
                                        }
                                        else
                                        {
                                            GD.PrintErr($"[DMapRenderer ERROR] Failed to store puzzle ID {puzzleId} at atlas position ({atlasCoords.X}, {atlasCoords.Y})");
                                        }
                                    }
                                    else
                                    {
                                        GD.PrintErr($"[DMapRenderer ERROR] Could not get tile data for atlas coords ({atlasCoords.X}, {atlasCoords.Y})");
                                    }
                                }
                                
                                GD.Print($"[DMapRenderer] Successfully set custom data for {successCount}/{puzzleIdMapping.Count} puzzle tiles");
                            }
                            else
                            {
                                GD.PrintErr("[DMapRenderer] Failed to create atlas texture");
                                CreateFallbackPuzzleTile(atlasSource, puzzleWidth);
                            }
                        }
                        else
                        {
                            GD.PrintErr("[DMapRenderer] No puzzle textures loaded, creating fallback");
                            CreateFallbackPuzzleTile(atlasSource, puzzleWidth);
                        }
                    }
                    else
                    {
                        GD.PrintErr("[DMapRenderer] Could not load ANI file for puzzle");
                        CreateFallbackPuzzleTile(atlasSource, puzzleWidth);
                    }
                }
                else
                {
                    GD.PrintErr("[DMapRenderer] Puzzle file has no ANI file");
                    CreateFallbackPuzzleTile(atlasSource, puzzleWidth);
                }

                // Source has already been added in the texture creation block above
                GD.Print($"[DMapRenderer] Completed puzzle TileSet creation with {atlasSource.GetTilesCount()} tiles");

                return tileSet;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[DMapRenderer] Error creating puzzle TileSet: {ex.Message}");
                GD.PrintErr($"[DMapRenderer] Exception details: {ex.StackTrace}");

                // Return a fallback tileset with at least one tile
                var fallbackTileSet = new TileSet();
                fallbackTileSet.TileShape = TileSet.TileShapeEnum.Square;
                fallbackTileSet.TileSize = new Vector2I(256, 256);
                fallbackTileSet.TileLayout = TileSet.TileLayoutEnum.Stacked;

                // Add custom data layer
                fallbackTileSet.AddCustomDataLayer();
                fallbackTileSet.SetCustomDataLayerName(0, "puzzle_id");
                fallbackTileSet.SetCustomDataLayerType(0, Variant.Type.Int);

                // Create a simple atlas source with one fallback tile
                var fallbackAtlasSource = new TileSetAtlasSource();
                CreateFallbackPuzzleTile(fallbackAtlasSource, 256);
                fallbackTileSet.AddSource(fallbackAtlasSource, 0);

                GD.Print("[DMapRenderer] Created fallback puzzle TileSet with 1 tile");
                return fallbackTileSet;
            }
        }

        private void CreateFallbackPuzzleTile(TileSetAtlasSource atlasSource, int tileSize)
        {
            // Create a simple colored texture as fallback
            var image = Image.CreateEmpty(tileSize, tileSize, false, Image.Format.Rgba8);
            image.Fill(new Godot.Color(0.2f, 0.6f, 0.8f, 0.5f)); // Semi-transparent blue fallback
            var fallbackTexture = ImageTexture.CreateFromImage(image);

            // IMPORTANT: Set texture and region size BEFORE creating tile
            atlasSource.Texture = fallbackTexture;
            atlasSource.TextureRegionSize = new Vector2I(tileSize, tileSize);
            
            // Now create the tile
            atlasSource.CreateTile(Vector2I.Zero);

            GD.Print("[DMapRenderer] Created fallback puzzle tile");
        }

        private ImageTexture CreateAtlasFromTextures(Dictionary<int, ImageTexture> textures, int tileSize)
        {
            if (textures.Count == 0)
            {
                GD.PrintErr("[DMapRenderer] No textures to create atlas from");
                return new ImageTexture();
            }

            // Calculate atlas dimensions (try to make it roughly square)
            int textureCount = textures.Count;
            int tilesPerRow = (int)Math.Ceiling(Math.Sqrt(textureCount));
            int rows = (int)Math.Ceiling((float)textureCount / tilesPerRow);

            int atlasWidth = tilesPerRow * tileSize;
            int atlasHeight = rows * tileSize;

            GD.Print($"[DMapRenderer] Creating atlas {atlasWidth}x{atlasHeight} for {textureCount} textures ({tilesPerRow}x{rows} tiles)");

            // Create the atlas image with transparent background
            var atlasImage = Image.CreateEmpty(atlasWidth, atlasHeight, false, Image.Format.Rgba8);
            atlasImage.Fill(new Godot.Color(0, 0, 0, 0)); // Transparent background

            // Copy each texture into the atlas
            int currentX = 0, currentY = 0;
            foreach (var kvp in textures)
            {
                var texture = kvp.Value;
                var sourceImage = texture.GetImage();

                if (sourceImage != null)
                {
                    // Check if image is compressed (DDS files are typically compressed)
                    if (sourceImage.IsCompressed())
                    {
                        // Decompress the image first
                        sourceImage.Decompress();
                        GD.Print($"[DMapRenderer] Decompressed image for puzzle {kvp.Key}");
                    }
                    
                    // Convert to RGBA8 format for consistency
                    if (sourceImage.GetFormat() != Image.Format.Rgba8)
                    {
                        sourceImage.Convert(Image.Format.Rgba8);
                        GD.Print($"[DMapRenderer] Converted puzzle {kvp.Key} from format {sourceImage.GetFormat()} to RGBA8");
                    }
                    
                    // Ensure source image is the expected size
                    if (sourceImage.GetWidth() != tileSize || sourceImage.GetHeight() != tileSize)
                    {
                        GD.Print($"[DMapRenderer] Resizing puzzle texture from {sourceImage.GetWidth()}x{sourceImage.GetHeight()} to {tileSize}x{tileSize}");
                        sourceImage.Resize(tileSize, tileSize, Image.Interpolation.Lanczos);
                    }
                    
                    // Copy the source image into the atlas
                    atlasImage.BlitRect(sourceImage, new Rect2I(0, 0, tileSize, tileSize),
                                       new Vector2I(currentX * tileSize, currentY * tileSize));

                    GD.Print($"[DMapRenderer] Placed puzzle {kvp.Key} at atlas position ({currentX}, {currentY})");
                }
                else
                {
                    GD.PrintErr($"[DMapRenderer] Could not get image from texture for puzzle {kvp.Key}");
                }

                // Move to next position
                currentX++;
                if (currentX >= tilesPerRow)
                {
                    currentX = 0;
                    currentY++;
                }
            }

            return ImageTexture.CreateFromImage(atlasImage);
        }

        private void CreateFallbackTerrainTile(TileSet tileSet)
        {
            var source = new TileSetAtlasSource();

            // Create a simple colored texture for visibility
            var image = Image.CreateEmpty(64, 32, false, Image.Format.Rgba8);
            image.Fill(new Godot.Color(0.3f, 0.7f, 0.3f, 0.5f)); // Semi-transparent green for terrain
            var texture = ImageTexture.CreateFromImage(image);

            // IMPORTANT: Set texture and region size BEFORE creating tile
            source.Texture = texture;
            source.TextureRegionSize = new Vector2I(64, 32);
            
            // Now create the tile
            source.CreateTile(Vector2I.Zero, new Vector2I(1, 1));

            // Add the source to the tileset
            var sourceId = tileSet.AddSource(source, 0);
            if (sourceId == -1)
            {
                GD.PrintErr("[DMapRenderer ERROR] Failed to add fallback atlas source to terrain TileSet");
            }
            else
            {
                GD.Print($"[DMapRenderer] Successfully added fallback atlas source with ID {sourceId} to terrain TileSet");
            }

            GD.Print("[DMapRenderer] Created fallback terrain tileset with basic green tile");
        }

        private ImageTexture CreateTerrainAtlasFromTextures(Dictionary<string, ImageTexture> textures, int tileSize)
        {
            if (textures.Count == 0)
            {
                GD.PrintErr("[DMapRenderer] No terrain textures to create atlas from");
                return new ImageTexture();
            }

            // Calculate atlas dimensions (try to make it roughly square)
            int textureCount = textures.Count;
            int tilesPerRow = (int)Math.Ceiling(Math.Sqrt(textureCount));
            int rows = (int)Math.Ceiling((float)textureCount / tilesPerRow);

            int atlasWidth = tilesPerRow * tileSize;
            int atlasHeight = rows * tileSize;

            GD.Print($"[DMapRenderer] Creating terrain atlas {atlasWidth}x{atlasHeight} for {textureCount} textures ({tilesPerRow}x{rows} tiles)");

            // Create the atlas image with transparent background
            var atlasImage = Image.CreateEmpty(atlasWidth, atlasHeight, false, Image.Format.Rgba8);
            atlasImage.Fill(new Godot.Color(0, 0, 0, 0)); // Transparent background

            // Copy each texture into the atlas
            int currentX = 0, currentY = 0;
            foreach (var kvp in textures)
            {
                var texture = kvp.Value;
                var sourceImage = texture.GetImage();

                if (sourceImage != null)
                {
                    // Check if image is compressed (DDS files are typically compressed)
                    if (sourceImage.IsCompressed())
                    {
                        // Decompress the image first
                        sourceImage.Decompress();
                        GD.Print($"[DMapRenderer] Decompressed terrain texture for {kvp.Key}");
                    }
                    
                    // Convert to RGBA8 format for consistency
                    if (sourceImage.GetFormat() != Image.Format.Rgba8)
                    {
                        sourceImage.Convert(Image.Format.Rgba8);
                        GD.Print($"[DMapRenderer] Converted terrain {kvp.Key} from format {sourceImage.GetFormat()} to RGBA8");
                    }
                    
                    // Resize source image to tile size if needed
                    if (sourceImage.GetWidth() != tileSize || sourceImage.GetHeight() != tileSize)
                    {
                        GD.Print($"[DMapRenderer] Resizing terrain texture from {sourceImage.GetWidth()}x{sourceImage.GetHeight()} to {tileSize}x{tileSize}");
                        sourceImage.Resize(tileSize, tileSize, Image.Interpolation.Lanczos);
                    }

                    // Copy the source image into the atlas
                    atlasImage.BlitRect(sourceImage, new Rect2I(0, 0, tileSize, tileSize),
                                       new Vector2I(currentX * tileSize, currentY * tileSize));

                    GD.Print($"[DMapRenderer] Placed terrain texture {kvp.Key} at atlas position ({currentX}, {currentY})");
                }
                else
                {
                    GD.PrintErr($"[DMapRenderer] Could not get image from terrain texture for {kvp.Key}");
                }

                // Move to next position
                currentX++;
                if (currentX >= tilesPerRow)
                {
                    currentX = 0;
                    currentY++;
                }
            }

            return ImageTexture.CreateFromImage(atlasImage);
        }


        private void PopulateFromDMap()
        {
            if (_dmapFile == null)
            {
                GD.PrintErr("[DMapRenderer ERROR] _dmapFile is null in PopulateFromDMap");
                return;
            }

            try
            {
                GD.Print("[DMapRenderer] Starting PopulateFromDMap...");

                // Handle backdrop puzzle files from scene layers (sky/far background)
                GD.Print("[DMapRenderer] Placing backdrop tiles from scene layers...");
                PlaceBackdropTiles();

                GD.Print("[DMapRenderer] Placing background tiles...");
                PlaceBackgroundTiles();

                GD.Print("[DMapRenderer] Placing terrain tiles...");
                PlaceTerrainTiles();

                GD.Print("[DMapRenderer] Placing object markers...");
                PlaceObjectMarkers();

                GD.Print("[DMapRenderer] PopulateFromDMap completed successfully");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[DMapRenderer ERROR] Exception in PopulateFromDMap: {ex.Message}");
                GD.PrintErr($"[DMapRenderer ERROR] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private void PlaceBackdropTiles()
        {
            // TODO: Implement backdrop/sky layer from scene layer puzzles
            // For now, we'll focus on getting the main puzzle and terrain working correctly
            if (_dmapFile == null)
            {
                return;
            }

            // Check if there are scene layers with backdrop puzzles
            if (_dmapFile.SceneLayers != null && _dmapFile.SceneLayers.Count > 0)
            {
                foreach (var sceneLayer in _dmapFile.SceneLayers)
                {
                    if (sceneLayer.Puzzles != null && sceneLayer.Puzzles.Count > 0)
                    {
                        GD.Print($"[DMapRenderer] Found {sceneLayer.Puzzles.Count} backdrop puzzle files in scene layer {sceneLayer.Index}");
                        // TODO: Load and render backdrop puzzles with parallax scrolling
                    }
                }
            }
        }


        private void PlaceBackgroundTiles()
        {
            if (_dmapFile == null || _backgroundLayer == null)
            {
                GD.PrintErr("[DMapRenderer ERROR] Cannot place background tiles - dmapFile or backgroundLayer is null");
                return;
            }

            if (string.IsNullOrEmpty(_dmapFile.PuzzleFile))
            {
                GD.Print("[DMapRenderer] No puzzle file specified, skipping background tiles");
                return;
            }

            // Check if TileSet is assigned before proceeding
            if (_backgroundLayer.TileSet == null)
            {
                GD.PrintErr("[DMapRenderer ERROR] Background layer TileSet is null - cannot place tiles");
                return;
            }

            GD.Print($"[DMapRenderer] Background TileSet has {_backgroundLayer.TileSet.GetSourceCount()} sources before placing tiles");

            try
            {
                GD.Print($"[DMapRenderer] Loading puzzle file: {_dmapFile.PuzzleFile}");

                // Load the puzzle file
                var puzzleFile = new PuzzleFile(_clientPath, _dmapFile.PuzzleFile);

                // The tileset should already be created - get it from the TileMap
                var tileSet = _backgroundLayer.TileSet;
                if (tileSet == null)
                {
                    GD.PrintErr("[DMapRenderer ERROR] Background layer has no TileSet");
                    return;
                }

                // Validate TileSet has sources
                if (tileSet.GetSourceCount() == 0)
                {
                    GD.PrintErr("[DMapRenderer ERROR] Background TileSet has no sources - cannot place tiles");
                    return;
                }
                
                var atlasSource = tileSet.GetSource(0) as TileSetAtlasSource;
                if (atlasSource == null)
                {
                    GD.PrintErr("[DMapRenderer ERROR] No atlas source found in background TileSet (source 0 is not TileSetAtlasSource)");
                    return;
                }

                GD.Print($"[DMapRenderer] Placing puzzle tiles using TileSet with {atlasSource.GetTilesCount()} available tiles");

                // Create a mapping from puzzle ID to atlas coordinates
                var puzzleIdToAtlasCoords = new Dictionary<ushort, Vector2I>();

                // Scan the atlas source to build the mapping
                GD.Print($"[DMapRenderer] Building puzzle ID to atlas mapping from {atlasSource.GetTilesCount()} tiles");
                for (int i = 0; i < atlasSource.GetTilesCount(); i++)
                {
                    var atlasCoords = atlasSource.GetTileId(i);
                    var tileData = atlasSource.GetTileData(atlasCoords, 0);

                    if (tileData != null)
                    {
                        // Try to get custom data with index 0 (puzzle_id)
                        var puzzleIdVariant = tileData.GetCustomData("puzzle_id");
                        if (puzzleIdVariant.VariantType == Variant.Type.Int)
                        {
                            ushort puzzleId = (ushort)puzzleIdVariant.AsInt32();
                            puzzleIdToAtlasCoords[puzzleId] = atlasCoords;
                            if (puzzleIdToAtlasCoords.Count <= 10) // Log first few for debugging
                            {
                                GD.Print($"[DMapRenderer] Mapped puzzle ID {puzzleId} to atlas coords ({atlasCoords.X}, {atlasCoords.Y})");
                            }
                        }
                        else
                        {
                            GD.PrintErr($"[DMapRenderer] Tile at ({atlasCoords.X}, {atlasCoords.Y}) has no puzzle_id custom data or wrong type: {puzzleIdVariant.VariantType}");
                        }
                    }
                }
                GD.Print($"[DMapRenderer] Built mapping for {puzzleIdToAtlasCoords.Count} puzzle IDs");

                // The background layer position is already set in LoadFromDMap
                // so we don't need to position it again here
                
                // Place tiles for each puzzle piece using the TileMap
                int tilesPlaced = 0;
                for (int x = 0; x < puzzleFile.Size.Width; x++)
                {
                    for (int y = 0; y < puzzleFile.Size.Height; y++)
                    {
                        ushort puzzleTileId = puzzleFile.PuzzleTiles[x, y];

                        // Skip invalid/empty tiles (65535)
                        if (puzzleTileId == 65535)
                        {
                            // Don't place anything for empty tiles - leave cell empty
                            continue;
                        }

                        if (puzzleIdToAtlasCoords.TryGetValue(puzzleTileId, out var atlasCoords))
                        {
                            // Place the tile in the TileMap - no offset needed since layer is positioned
                            var tileCoords = new Vector2I(x, y);
                            _backgroundLayer.SetCell(tileCoords, 0, atlasCoords, 0);
                            tilesPlaced++;

                            if (tilesPlaced <= 10) // Log first few for debugging
                            {
                                GD.Print($"[DMapRenderer] Placed puzzle tile {puzzleTileId} at tile ({x}, {y}) using atlas coords ({atlasCoords.X}, {atlasCoords.Y})");
                            }
                        }
                        else
                        {
                            // Only warn for non-empty tiles that can't be found
                            if (tilesPlaced <= 10) // Limit warnings to first few
                            {
                                GD.PrintErr($"[DMapRenderer] No atlas mapping found for puzzle tile {puzzleTileId} at ({x}, {y})");
                            }

                            // Leave cell empty rather than placing fallback for missing tiles
                        }
                    }
                }

                GD.Print($"[DMapRenderer] Successfully placed {tilesPlaced} of {puzzleFile.Size.Width * puzzleFile.Size.Height} background tiles using TileMap");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[DMapRenderer ERROR] Error placing background tiles: {ex.Message}");
                GD.PrintErr($"[DMapRenderer ERROR] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private void PlaceTerrainTiles()
        {
            if (_dmapFile == null || _terrainLayer == null)
            {
                GD.PrintErr("[DMapRenderer ERROR] Cannot place terrain tiles - dmapFile or terrainLayer is null");
                return;
            }

            if (_dmapFile.TerrainScenes.Count == 0)
            {
                GD.Print("[DMapRenderer] No terrain scenes found, skipping terrain rendering");
                return;
            }

            // Check if TileSet is assigned before proceeding
            if (_terrainLayer.TileSet == null)
            {
                GD.PrintErr("[DMapRenderer ERROR] Terrain layer TileSet is null - cannot place tiles");
                return;
            }

            GD.Print($"[DMapRenderer] Terrain TileSet has {_terrainLayer.TileSet.GetSourceCount()} sources before placing tiles");

            try
            {
                // Get the tileset from the terrain layer
                var tileSet = _terrainLayer.TileSet;
                if (tileSet == null)
                {
                    GD.PrintErr("[DMapRenderer ERROR] Terrain layer has no TileSet");
                    return;
                }

                // Validate TileSet has sources
                if (tileSet.GetSourceCount() == 0)
                {
                    GD.PrintErr("[DMapRenderer ERROR] Terrain TileSet has no sources - cannot place tiles");
                    return;
                }
                
                var atlasSource = tileSet.GetSource(0) as TileSetAtlasSource;
                if (atlasSource == null)
                {
                    GD.PrintErr("[DMapRenderer ERROR] No atlas source found in terrain TileSet (source 0 is not TileSetAtlasSource)");
                    return;
                }

                GD.Print($"[DMapRenderer] Placing terrain tiles using TileSet with {atlasSource.GetTilesCount()} available tiles");

                // Create a mapping from scene key to atlas coordinates
                var sceneKeyToAtlasCoords = new Dictionary<string, Vector2I>();

                // Scan the atlas source to build the mapping
                GD.Print($"[DMapRenderer] Building scene key to atlas mapping from {atlasSource.GetTilesCount()} tiles");
                for (int i = 0; i < atlasSource.GetTilesCount(); i++)
                {
                    var atlasCoords = atlasSource.GetTileId(i);
                    var tileData = atlasSource.GetTileData(atlasCoords, 0);

                    if (tileData != null)
                    {
                        // Try to get custom data with index 3 (scene_key) - matching the order we added the layers
                        var sceneKeyVariant = tileData.GetCustomData("scene_key");
                        if (sceneKeyVariant.VariantType == Variant.Type.String)
                        {
                            string sceneKey = sceneKeyVariant.AsString();
                            sceneKeyToAtlasCoords[sceneKey] = atlasCoords;
                            if (sceneKeyToAtlasCoords.Count <= 10) // Log first few for debugging
                            {
                                GD.Print($"[DMapRenderer] Mapped scene key {sceneKey} to atlas coords ({atlasCoords.X}, {atlasCoords.Y})");
                            }
                        }
                        else
                        {
                            GD.PrintErr($"[DMapRenderer] Tile at ({atlasCoords.X}, {atlasCoords.Y}) has no scene_key custom data or wrong type: {sceneKeyVariant.VariantType}");
                        }
                    }
                }
                GD.Print($"[DMapRenderer] Built mapping for {sceneKeyToAtlasCoords.Count} scene keys");

                int terrainTilesPlaced = 0;

                // Place terrain tiles based on terrain scenes
                foreach (var terrainScene in _dmapFile.TerrainScenes)
                {
                    if (string.IsNullOrEmpty(terrainScene.SceneFile))
                    {
                        GD.PrintErr("[DMapRenderer] Terrain scene has empty SceneFile, skipping");
                        continue;
                    }

                    try
                    {
                        GD.Print($"[DMapRenderer] Processing scene file: {terrainScene.SceneFile} at position ({terrainScene.Position.X}, {terrainScene.Position.Y})");

                        // Load the scene file
                        var sceneFile = new SceneFile(_clientPath, terrainScene.SceneFile);

                        // Process each scene part and place as tiles
                        foreach (var scenePart in sceneFile.SceneParts)
                        {
                            if (string.IsNullOrEmpty(scenePart.AniPath) || string.IsNullOrEmpty(scenePart.AniName))
                            {
                                GD.PrintErr($"[DMapRenderer] Scene part has empty ANI path or name, skipping");
                                continue;
                            }

                            string sceneKey = $"{scenePart.AniPath}_{scenePart.AniName}";

                            if (sceneKeyToAtlasCoords.TryGetValue(sceneKey, out var atlasCoords))
                            {
                                // Calculate tile position based on terrain scene position and scene part offset
                                int tileX = (int)terrainScene.Position.X + scenePart.TileOffset.X;
                                int tileY = (int)terrainScene.Position.Y + scenePart.TileOffset.Y;

                                var tileCoords = new Vector2I(tileX, tileY);

                                // Place the tile in the TileMap
                                _terrainLayer.SetCell(tileCoords, 0, atlasCoords, 0);
                                terrainTilesPlaced++;

                                if (terrainTilesPlaced <= 10) // Log first few for debugging
                                {
                                    GD.Print($"[DMapRenderer] Placed terrain tile {sceneKey} at ({tileX}, {tileY}) using atlas coords ({atlasCoords.X}, {atlasCoords.Y})");
                                }
                            }
                            else
                            {
                                // Only warn for first few missing mappings to avoid spam
                                if (terrainTilesPlaced <= 10)
                                {
                                    GD.PrintErr($"[DMapRenderer] No atlas mapping found for terrain scene key {sceneKey}");
                                }

                                // Skip placing terrain tiles that don't have mappings
                                // This prevents errors and allows for partial terrain loading
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[DMapRenderer] Error processing terrain scene {terrainScene.SceneFile}: {ex.Message}");
                    }
                }

                GD.Print($"[DMapRenderer] Successfully placed {terrainTilesPlaced} terrain tiles from {_dmapFile.TerrainScenes.Count} terrain scenes using TileMap");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[DMapRenderer ERROR] Error placing terrain tiles: {ex.Message}");
                GD.PrintErr($"[DMapRenderer ERROR] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private string ExtractClientPath(string dmapPath)
        {
            try
            {
                // Extract the base directory where game client files are located
                // The DMap path typically looks like: /path/to/Game/5017/map/filename.dmap
                var directory = Path.GetDirectoryName(dmapPath);
                if (directory != null)
                {
                    // Go up directories until we find the client root (containing 'data' folder)
                    var current = new DirectoryInfo(directory);
                    while (current != null && current.Parent != null)
                    {
                        var dataPath = Path.Combine(current.FullName, "data");
                        if (Directory.Exists(dataPath))
                        {
                            return current.FullName;
                        }
                        current = current.Parent;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting client path from {DmapPath}", dmapPath);
            }

            return string.Empty;
        }

        private void PlaceObjectMarkers()
        {
            if (_dmapFile == null || _objectLayer == null || _cordConverter == null) return;

            // Place DMapPortal nodes for portals
            var portalLayer = GetPortalLayer() ?? _objectLayer;
            foreach (var portal in _dmapFile.Portals)
            {
                var portalNode = new DMapPortal(portal, _cordConverter);
                portalLayer?.AddChild(portalNode);

                if (Engine.IsEditorHint())
                {
                    var root = GetTree()?.EditedSceneRoot;
                    if (root != null) portalNode.Owner = root;
                }
            }

            // Render scene objects from TerrainScenes
            RenderSceneObjects();

            // Render cover objects with actual sprites
            RenderCoverObjects();
        }

        private void RenderSceneObjects()
        {
            if (_dmapFile == null || _objectLayer == null || string.IsNullOrEmpty(_clientPath)) return;

            foreach (var terrainScene in _dmapFile.TerrainScenes)
            {
                try
                {
                    var sceneFile = LoadSceneFile(terrainScene.SceneFile);
                    if (sceneFile != null)
                    {
                        RenderSceneParts(sceneFile, terrainScene.Position);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error rendering scene {SceneFile}", terrainScene.SceneFile);
                }
            }
        }

        private SceneFile? LoadSceneFile(string sceneFilePath)
        {
            if (string.IsNullOrEmpty(sceneFilePath)) return null;

            // Check cache first
            if (_loadedSceneFiles.TryGetValue(sceneFilePath, out var cachedScene))
            {
                return cachedScene;
            }

            try
            {
                var sceneFile = new SceneFile(_clientPath, sceneFilePath);
                _loadedSceneFiles[sceneFilePath] = sceneFile;
                return sceneFile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load scene file {SceneFilePath}", sceneFilePath);
                return null;
            }
        }

        private void RenderSceneParts(SceneFile sceneFile, TilePosition basePosition)
        {
            foreach (var scenePart in sceneFile.SceneParts)
            {
                RenderScenePart(scenePart, basePosition);
            }
        }

        private void RenderScenePart(ScenePart scenePart, TilePosition basePosition)
        {
            if (string.IsNullOrEmpty(scenePart.AniPath) || string.IsNullOrEmpty(scenePart.AniName))
                return;

            // Calculate world position using coordinate converter and scene positioning
            var worldPos = CalculateScenePartPosition(scenePart, basePosition);

            // Load animation frames for the scene part
            var frameTextures = LoadAniFrames(scenePart.AniPath, scenePart.AniName);
            if (frameTextures == null || frameTextures.Length == 0) return;

            // Create sprite node for the scene part - use pooling if enabled
            Sprite2D sprite;
            if (EnableObjectPooling && _spritePool != null)
            {
                sprite = _spritePool.Get();

                // Ensure pooled sprite is detached from any previous parent
                if (sprite.GetParent() != null)
                {
                    sprite.GetParent().RemoveChild(sprite);
                }

                sprite.Name = $"Scene_{scenePart.AniName}";
            }
            else
            {
                sprite = new Sprite2D();
                sprite.Name = $"Scene_{scenePart.AniName}";
            }

            // Set up animation frames and initial texture
            int objectId = _nextObjectId++;
            _objectFrames[objectId] = frameTextures;
            _currentFrame[objectId] = 0;
            _lastFrameUpdate[objectId] = Time.GetUnixTimeFromSystem();

            sprite.Texture = frameTextures[0];
            sprite.SetMeta("object_id", objectId);
            sprite.SetMeta("animation_interval", scenePart.Interval);
            sprite.Visible = true; // Ensure sprite is visible by default

            // Calculate final position with Y-sorting consideration
            var finalPosition = CalculateYSortedPosition(worldPos, scenePart.OffsetElevation);
            sprite.Position = finalPosition;

            // Enable LOD if system is available
            if (EnableLOD && _lodSystem != null)
            {
                var lodSprite = new LODSprite();
                lodSprite.Texture = frameTextures[0];
                lodSprite.Position = finalPosition;
                lodSprite.Name = sprite.Name;

                _lodSystem.RegisterLODObject(lodSprite);
                sprite = lodSprite;
            }

            // Add to appropriate layer
            var sceneLayer = GetSceneObjectsLayer() ?? _objectLayer;
            sceneLayer?.AddChild(sprite);

            // Add to chunk management if enabled
            if (EnableChunking && _chunkManager != null)
            {
                var tileCoord = new Vector2I((int)(basePosition.X), (int)(basePosition.Y));
                _chunkManager.AddObjectToChunk(tileCoord, sprite);
            }

            if (Engine.IsEditorHint())
            {
                var root = GetTree()?.EditedSceneRoot;
                if (root != null) sprite.Owner = root;
            }
        }

        private void RenderCoverObjects()
        {
            if (_dmapFile == null || _objectLayer == null) return;

            foreach (var cover in _dmapFile.Covers)
            {
                RenderCoverObject(cover);
            }
        }

        private void RenderCoverObject(Cover cover)
        {
            if (string.IsNullOrEmpty(cover.AniPath) || string.IsNullOrEmpty(cover.AniName))
                return;

            // Convert cover position to world coordinates
            var worldPos = CalculateCoverPosition(cover);

            // Load animation frames for the cover
            var frameTextures = LoadAniFrames(cover.AniPath, cover.AniName);
            if (frameTextures == null || frameTextures.Length == 0)
            {
                // Fallback to marker if texture loading fails
                CreateCoverMarker(cover, worldPos);
                return;
            }

            // Create sprite node for the cover - use pooling if enabled
            Sprite2D sprite;
            if (EnableObjectPooling && _spritePool != null)
            {
                sprite = _spritePool.Get();

                // Ensure pooled sprite is detached from any previous parent
                if (sprite.GetParent() != null)
                {
                    sprite.GetParent().RemoveChild(sprite);
                }

                sprite.Name = $"Cover_{cover.AniName}";
            }
            else
            {
                sprite = new Sprite2D();
                sprite.Name = $"Cover_{cover.AniName}";
            }

            // Set up animation frames and initial texture
            int objectId = _nextObjectId++;
            _objectFrames[objectId] = frameTextures;
            _currentFrame[objectId] = 0;
            _lastFrameUpdate[objectId] = Time.GetUnixTimeFromSystem();

            sprite.Texture = frameTextures[0];
            sprite.SetMeta("object_id", objectId);
            sprite.SetMeta("animation_interval", cover.AnimationInterval);
            sprite.Visible = true; // Ensure sprite is visible by default

            // Apply pixel offset if specified
            var offsetPos = worldPos;
            if (cover.Offset.X != 0 || cover.Offset.Y != 0)
            {
                offsetPos = new Vector2(
                    worldPos.X + cover.Offset.X,
                    worldPos.Y + cover.Offset.Y
                );
            }

            // Calculate Y-sorted position (covers are typically at ground level)
            sprite.Position = CalculateYSortedPosition(offsetPos, 0);

            // Set transparency for cover objects (they often need to be semi-transparent)
            sprite.Modulate = new Godot.Color(1, 1, 1, 0.8f);

            // Add to appropriate layer
            var coverLayer = GetCoverObjectsLayer() ?? _objectLayer;
            coverLayer?.AddChild(sprite);

            // Add to chunk management if enabled
            if (EnableChunking && _chunkManager != null)
            {
                var tileCoord = new Vector2I((int)cover.Position.X, (int)cover.Position.Y);
                _chunkManager.AddObjectToChunk(tileCoord, sprite);
            }

            // Enable LOD if system is available
            if (EnableLOD && _lodSystem != null && sprite is Sprite2D regularSprite)
            {
                var lodSprite = new LODSprite();
                lodSprite.Texture = regularSprite.Texture;
                lodSprite.Position = regularSprite.Position;
                lodSprite.Modulate = regularSprite.Modulate;
                lodSprite.Name = regularSprite.Name;

                // Replace the regular sprite with LOD sprite
                coverLayer?.RemoveChild(regularSprite);
                coverLayer?.AddChild(lodSprite);

                _lodSystem.RegisterLODObject(lodSprite);
                sprite = lodSprite;
            }

            if (Engine.IsEditorHint())
            {
                var root = GetTree()?.EditedSceneRoot;
                if (root != null) sprite.Owner = root;
            }
        }

        private Vector2 CalculateScenePartPosition(ScenePart scenePart, TilePosition basePosition)
        {
            if (_cordConverter != null && _dmapFile != null)
            {
                // Calculate the scene position like the original C# code
                // Scene position = base terrain scene position + scene part tile offset
                var sceneX = (int)basePosition.X + scenePart.TileOffset.X;
                var sceneY = (int)basePosition.Y + scenePart.TileOffset.Y;
                
                // Convert to background coordinates using Cell2Bg
                var bgPos = _cordConverter.Cell2Bg(
                    new System.Drawing.Point(sceneX, sceneY)
                );
                
                // Add pixel offset
                bgPos.X += scenePart.PixelLocation.X;
                bgPos.Y += scenePart.PixelLocation.Y;
                
                // We need to calculate the puzzle background size for proper positioning
                // For now, use the puzzle file if available
                if (!string.IsNullOrEmpty(_dmapFile.PuzzleFile))
                {
                    try
                    {
                        var puzzleFile = new PuzzleFile(_clientPath, _dmapFile.PuzzleFile);
                        int puzzleWidth = puzzleFile.GetWidth();
                        int bgWidth = (int)(puzzleFile.Size.Width * puzzleWidth);
                        int bgHeight = (int)(puzzleFile.Size.Height * puzzleWidth);
                        
                        // Recreate CordConverter with correct background size
                        var dmapSize = new System.Drawing.Size(
                            (int)_dmapFile.SizeTiles.Width,
                            (int)_dmapFile.SizeTiles.Height
                        );
                        var bgSize = new System.Drawing.Size((int)bgWidth, (int)bgHeight);
                        var correctConverter = new CordConverter(dmapSize, bgSize);
                        
                        // Recalculate with correct converter
                        bgPos = correctConverter.Cell2Bg(
                            new System.Drawing.Point(sceneX, sceneY)
                        );
                        bgPos.X += scenePart.PixelLocation.X;
                        bgPos.Y += scenePart.PixelLocation.Y;
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[DMapRenderer] Failed to get puzzle size for coordinate conversion: {ex.Message}");
                    }
                }
                
                return new Vector2(bgPos.X, bgPos.Y);
            }

            // Fallback to simple calculation
            var totalX = (int)basePosition.X + scenePart.TileOffset.X;
            var totalY = (int)basePosition.Y + scenePart.TileOffset.Y;
            return new Vector2(totalX * 64 + scenePart.PixelLocation.X, totalY * 32 + scenePart.PixelLocation.Y);
        }

        private Vector2 CalculateCoverPosition(Cover cover)
        {
            if (_cordConverter != null && _dmapFile != null)
            {
                // Calculate cover position like the original C# code
                // Convert the isometric tile to orthographic World coords then to background coords
                var bgPos = _cordConverter.Cell2Bg(
                    new System.Drawing.Point((int)cover.Position.X, (int)cover.Position.Y)
                );
                
                // Subtract the orthographic offset (covers have negative offsets)
                bgPos.X -= cover.Offset.X;
                bgPos.Y -= cover.Offset.Y;
                
                // Update coordinate converter with correct background size if needed
                if (!string.IsNullOrEmpty(_dmapFile.PuzzleFile))
                {
                    try
                    {
                        var puzzleFile = new PuzzleFile(_clientPath, _dmapFile.PuzzleFile);
                        int puzzleWidth = puzzleFile.GetWidth();
                        int bgWidth = (int)(puzzleFile.Size.Width * puzzleWidth);
                        int bgHeight = (int)(puzzleFile.Size.Height * puzzleWidth);
                        
                        var dmapSize = new System.Drawing.Size(
                            (int)_dmapFile.SizeTiles.Width,
                            (int)_dmapFile.SizeTiles.Height
                        );
                        var bgSize = new System.Drawing.Size((int)bgWidth, (int)bgHeight);
                        var correctConverter = new CordConverter(dmapSize, bgSize);
                        
                        // Recalculate with correct converter
                        bgPos = correctConverter.Cell2Bg(
                            new System.Drawing.Point((int)cover.Position.X, (int)cover.Position.Y)
                        );
                        bgPos.X -= cover.Offset.X;
                        bgPos.Y -= cover.Offset.Y;
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[DMapRenderer] Failed to get puzzle size for cover coordinate conversion: {ex.Message}");
                    }
                }
                
                return new Vector2(bgPos.X, bgPos.Y);
            }

            // Fallback to simple isometric calculation
            return new Vector2((int)cover.Position.X * 64, (int)cover.Position.Y * 32);
        }

        private ImageTexture[]? LoadAniFrames(string aniPath, string aniName)
        {
            GD.Print($"[DEBUG] LoadAniFrames called with: {aniPath} / {aniName}");
            try
            {
                if (!aniPath.EndsWith(".ani", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Expected .ani file, got: {AniPath}", aniPath);
                    return null;
                }

                // Get or load ANI file from cache
                AniFile? aniFile = null;
                if (!_aniCache.TryGetValue(aniPath, out aniFile))
                {
                    aniFile = new AniFile(_clientPath, aniPath);
                    _aniCache.TryAdd(aniPath, aniFile);
                }

                // Check if the animation exists in the ANI file
                if (!aniFile.Anis.ContainsKey(aniName))
                {
                    if (!aniName.EndsWith("65535"))
                        _logger.LogWarning("Animation not found: {AniName} in {AniPath}", aniName, aniPath);
                    return null;
                }

                // Load all frames for this animation
                var ani = aniFile.Anis[aniName];
                var frameTextures = new List<ImageTexture>();
                int frameIdx = 0;

                foreach (var framePath in ani.Frames)
                {
                    // Normalize path separators for cross-platform compatibility
                    var normalizedFramePath = framePath.Replace('/', Path.DirectorySeparatorChar);
                    var fullTexturePath = Path.Combine(_clientPath, normalizedFramePath);

                    ImageTexture? frameTexture = null;

                    // First, check if the file exists
                    if (!File.Exists(fullTexturePath))
                    {
                        _logger.LogWarning("Frame texture file not found: {FramePath}", fullTexturePath);
                        continue;
                    }

                    // Convert absolute path to res:// path for TextureConverter
                    string resPath = ConvertToResPath(fullTexturePath);
                    GD.Print($"[DEBUG] Path conversion: {fullTexturePath} -> {resPath}");

                    if (fullTexturePath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            // Try ResourceLoader first for DDS files (Godot's preferred method for runtime DDS loading)
                            GD.Print($"[DEBUG] Trying ResourceLoader.Load for DDS: {resPath}");
                            var loadedTexture = ResourceLoader.Load(resPath);
                            if (loadedTexture is ImageTexture imageTexture)
                            {
                                frameTexture = imageTexture;
                                GD.Print($"[DEBUG] ResourceLoader.Load result: SUCCESS");
                            }
                            else if (loadedTexture is Texture2D texture2D)
                            {
                                // Convert Texture2D to ImageTexture if needed
                                var image = texture2D.GetImage();
                                if (image != null)
                                {
                                    frameTexture = ImageTexture.CreateFromImage(image);
                                    GD.Print($"[DEBUG] ResourceLoader.Load (Texture2D converted) result: SUCCESS");
                                }
                                else
                                {
                                    GD.Print($"[DEBUG] ResourceLoader.Load result: NULL (couldn't get image from Texture2D)");
                                }
                            }
                            else
                            {
                                GD.Print($"[DEBUG] ResourceLoader.Load result: NULL (unexpected type: {loadedTexture?.GetType().Name ?? "null"})");
                            }
                        }
                        catch (Exception ex)
                        {
                            GD.Print($"[ERROR] ResourceLoader.Load exception: {ex.Message}");
                            _logger.LogError(ex, "Failed to load DDS texture: {FramePath} -> {ResPath}", fullTexturePath, resPath);
                        }
                    }
                    else
                    {
                        try
                        {
                            GD.Print($"[DEBUG] Calling Image.LoadFromFile with: {resPath}");
                            var image = Image.LoadFromFile(resPath);
                            frameTexture = image != null ? ImageTexture.CreateFromImage(image) : null;
                            GD.Print($"[DEBUG] Image.LoadFromFile result: {(frameTexture != null ? "SUCCESS" : "NULL")}");
                        }
                        catch (Exception ex)
                        {
                            GD.Print($"[ERROR] Image.LoadFromFile exception: {ex.Message}");
                            _logger.LogError(ex, "Failed to load image texture: {FramePath} -> {ResPath}", fullTexturePath, resPath);
                        }
                    }

                    if (frameTexture != null)
                    {
                        frameTextures.Add(frameTexture);
                        frameIdx++;
                        GD.Print($"[SUCCESS] Loaded frame texture: {resPath}");
                    }
                    else
                    {
                        GD.Print($"[WARNING] Could not load frame texture: {resPath} (original: {fullTexturePath})");
                        _logger.LogWarning("Could not load frame texture: {ResPath} (original: {FullPath})", resPath, fullTexturePath);
                    }
                }

                _logger.LogDebug("Loaded {FrameCount} frames for {AniName} from {AniPath}", frameTextures.Count, aniName, aniPath);
                return frameTextures.Count > 0 ? frameTextures.ToArray() : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading animation frames {AniPath}/{AniName}", aniPath, aniName);
                return null;
            }
        }

        private string ConvertToResPath(string absolutePath)
        {
            try
            {
                // Get the project root directory (where the .godot folder is)
                string projectRoot = ProjectSettings.GlobalizePath("res://");

                // Normalize the absolute path
                string normalizedAbsolutePath = Path.GetFullPath(absolutePath);
                string normalizedProjectRoot = Path.GetFullPath(projectRoot);

                GD.Print($"[DEBUG] ConvertToResPath - Project Root: {normalizedProjectRoot}");
                GD.Print($"[DEBUG] ConvertToResPath - Absolute Path: {normalizedAbsolutePath}");

                // Check if the absolute path is within the project directory
                if (normalizedAbsolutePath.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    // Convert to relative path from project root
                    string relativePath = Path.GetRelativePath(normalizedProjectRoot, normalizedAbsolutePath);

                    // Convert to res:// path with forward slashes
                    string resPath = "res://" + relativePath.Replace(Path.DirectorySeparatorChar, '/');

                    GD.Print($"[DEBUG] Successfully converted: {absolutePath} -> {resPath}");
                    return resPath;
                }
                else
                {
                    GD.Print($"[WARNING] Path outside project directory: {normalizedAbsolutePath} (Project: {normalizedProjectRoot})");
                    return absolutePath; // Fallback to original path
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert absolute path to res path: {AbsolutePath}", absolutePath);
                return absolutePath; // Fallback to original path
            }
        }

        private Vector2 CalculateYSortedPosition(Vector2 worldPosition, int elevationOffset = 0)
        {
            // In isometric view, Y-sorting works best when we adjust the Y coordinate
            // to account for the object's depth in the world
            var adjustedY = worldPosition.Y;

            // Apply elevation offset (negative makes objects appear higher/behind)
            if (elevationOffset != 0)
            {
                adjustedY -= elevationOffset;
            }

            // For proper Y-sorting in isometric view, we need to ensure objects
            // further back (higher tile Y coordinates) have higher Y positions
            return new Vector2(worldPosition.X, adjustedY);
        }

        private void CreateSceneLayerManagement()
        {
            try
            {
                GD.Print("[DMapRenderer] Creating scene layer management...");
                // The objectLayer already exists with Y-sorting enabled
                // We can create sub-layers if needed for better organization
                if (_objectLayer == null)
                {
                    GD.PrintErr("[DMapRenderer ERROR] _objectLayer is null in CreateSceneLayerManagement");
                    return;
                }

                GD.Print("[DMapRenderer] Creating terrain objects layer...");
                // Create sublayers for different object types
                var terrainObjectsLayer = new Node2D();
                terrainObjectsLayer.Name = "TerrainObjects";
                terrainObjectsLayer.YSortEnabled = true;
                terrainObjectsLayer.ZIndex = 0;

                GD.Print("[DMapRenderer] Creating cover objects layer...");
                var coverObjectsLayer = new Node2D();
                coverObjectsLayer.Name = "CoverObjects";
                coverObjectsLayer.YSortEnabled = true;
                coverObjectsLayer.ZIndex = 1; // Covers render above terrain objects

                GD.Print("[DMapRenderer] Creating portal layer...");
                var portalLayer = new Node2D();
                portalLayer.Name = "Portals";
                portalLayer.YSortEnabled = true;
                portalLayer.ZIndex = 2; // Portals on top

                GD.Print("[DMapRenderer] Adding sublayers to object layer...");
                _objectLayer.AddChild(terrainObjectsLayer);
                _objectLayer.AddChild(coverObjectsLayer);
                _objectLayer.AddChild(portalLayer);
                GD.Print("[DMapRenderer] Scene layer management completed");

                // Set owner for editor visibility - inside try block to access variables
                if (Engine.IsEditorHint())
                {
                    var root = GetTree()?.EditedSceneRoot;
                    if (root != null)
                    {
                        GD.Print("[DMapRenderer] Setting owners for sublayers");
                        terrainObjectsLayer.Owner = root;
                        coverObjectsLayer.Owner = root;
                        portalLayer.Owner = root;
                    }
                    else
                    {
                        GD.Print("[DMapRenderer] No EditedSceneRoot found for sublayers");
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[DMapRenderer ERROR] Exception in CreateSceneLayerManagement: {ex.Message}");
                GD.PrintErr($"[DMapRenderer ERROR] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private Node2D? GetSceneObjectsLayer()
        {
            return _objectLayer?.GetNode<Node2D>("TerrainObjects");
        }

        private Node2D? GetCoverObjectsLayer()
        {
            return _objectLayer?.GetNode<Node2D>("CoverObjects");
        }

        private Node2D? GetPortalLayer()
        {
            return _objectLayer?.GetNode<Node2D>("Portals");
        }

        private void CreateCoverMarker(Cover cover, Vector2 position)
        {
            var marker = new Marker2D();
            marker.Name = $"Cover_{cover.AniName}_Marker";
            marker.Position = CalculateYSortedPosition(position);

            var coverLayer = GetCoverObjectsLayer() ?? _objectLayer;
            coverLayer?.AddChild(marker);

            if (Engine.IsEditorHint())
            {
                var root = GetTree()?.EditedSceneRoot;
                if (root != null) marker.Owner = root;
            }
        }

        public DmapFile? GetDMapFile() => _dmapFile;

        // Selection functionality for Editor Dock
        [Signal]
        public delegate void TileSelectedEventHandler(Vector2I tileCoords, short height, ushort surface, ushort noAccess);

        [Signal]
        public delegate void TileHoveredEventHandler(Vector2I tileCoords);

        private Vector2I _selectedTile = new Vector2I(-1, -1);
        private System.Collections.Generic.HashSet<Vector2I> _selectedTiles = new System.Collections.Generic.HashSet<Vector2I>();
        private System.Collections.Generic.HashSet<Vector2I> _previousSelectedTiles = new System.Collections.Generic.HashSet<Vector2I>();
        private TileMapLayer? _selectionLayer;
        private static Texture2D? _cachedSelectionTexture;

        private void CreateSelectionLayer()
        {
            _selectionLayer = new TileMapLayer();
            _selectionLayer.Name = "SelectionLayer";
            _selectionLayer.ZIndex = 10;
            _selectionLayer.Enabled = true;

            // Create a simple selection tileset
            var selectionTileSet = new TileSet();
            selectionTileSet.TileShape = TileSet.TileShapeEnum.Isometric;
            selectionTileSet.TileSize = new Vector2I(64, 32);
            selectionTileSet.TileLayout = TileSet.TileLayoutEnum.Stacked;

            var source = new TileSetAtlasSource();
            source.Texture = CreateSelectionTexture();
            source.TextureRegionSize = new Vector2I(64, 32);
            source.CreateTile(Vector2I.Zero, new Vector2I(1, 1));

            selectionTileSet.AddSource(source);
            _selectionLayer.TileSet = selectionTileSet;

            AddChild(_selectionLayer);

            if (Engine.IsEditorHint())
            {
                var root = GetTree()?.EditedSceneRoot;
                if (root != null)
                {
                    _selectionLayer.Owner = root;
                }
            }
        }

        private static Texture2D CreateSelectionTexture()
        {
            if (_cachedSelectionTexture != null)
                return _cachedSelectionTexture;

            // Create a simple selection texture - cached for performance
            var image = Image.CreateEmpty(64, 32, false, Image.Format.Rgba8);
            image.Fill(new Godot.Color(1, 1, 0, 0.5f)); // Yellow with transparency

            _cachedSelectionTexture = ImageTexture.CreateFromImage(image);
            return _cachedSelectionTexture;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!Engine.IsEditorHint())
                return;

            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
                {
                    var globalPos = mouseButton.GlobalPosition;
                    var localPos = ToLocal(globalPos);
                    var tileCoords = GetTileFromPosition(localPos);

                    if (IsValidTileCoordinate(tileCoords))
                    {
                        SelectTile(tileCoords);
                    }
                }
            }
        }

        private Vector2I GetTileFromPosition(Vector2 localPos)
        {
            if (_terrainLayer != null)
            {
                return _terrainLayer.LocalToMap(localPos);
            }

            // Fallback to coordinate helper
            if (_coordinateHelper != null)
            {
                return _coordinateHelper.LocalToTile(localPos);
            }

            return new Vector2I(-1, -1);
        }

        private void SelectTile(Vector2I tileCoords)
        {
            _selectedTile = tileCoords;
            _selectedTiles.Clear();
            _selectedTiles.Add(tileCoords);

            var tileData = GetTileData(tileCoords);
            if (tileData.HasValue)
            {
                var tile = tileData.Value;
                EmitSignal(SignalName.TileSelected, tileCoords, tile.Height, tile.Surface, tile.NoAccess);
            }

            UpdateSelectionVisual();
        }

        private void UpdateSelectionVisual()
        {
            if (_selectionLayer == null)
                return;

            // Efficient update: only modify changed tiles
            var tilesToRemove = new System.Collections.Generic.HashSet<Vector2I>(_previousSelectedTiles);
            tilesToRemove.ExceptWith(_selectedTiles);

            var tilesToAdd = new System.Collections.Generic.HashSet<Vector2I>(_selectedTiles);
            tilesToAdd.ExceptWith(_previousSelectedTiles);

            // Remove deselected tiles
            foreach (var tile in tilesToRemove)
            {
                _selectionLayer.EraseCell(tile);
            }

            // Add newly selected tiles
            foreach (var tile in tilesToAdd)
            {
                _selectionLayer.SetCell(tile, 0, Vector2I.Zero);
            }

            // Update previous selection cache
            _previousSelectedTiles.Clear();
            _previousSelectedTiles.UnionWith(_selectedTiles);
        }

        public void UpdateTileProperty(Vector2I tileCoords, TileProperty property, object value)
        {
            if (_dmapFile == null || !IsValidTileCoordinate(tileCoords))
                return;

            // Validate property value before applying
            if (!property.IsValidValue(value))
            {
                GD.PrintErr($"Invalid value {value} for property {property}");
                return;
            }

            var tile = _dmapFile.TileSet[tileCoords.X, tileCoords.Y];

            try
            {
                switch (property)
                {
                    case TileProperty.Height:
                        tile = new Tile(tile.NoAccess, tile.Surface, System.Convert.ToInt16(value));
                        break;
                    case TileProperty.Surface:
                        tile = new Tile(tile.NoAccess, System.Convert.ToUInt16(value), tile.Height);
                        break;
                    case TileProperty.NoAccess:
                        tile = new Tile(System.Convert.ToUInt16(value), tile.Surface, tile.Height);
                        break;
                    default:
                        GD.PrintErr($"Unknown tile property: {property}");
                        return;
                }

                _dmapFile.TileSet[tileCoords.X, tileCoords.Y] = tile;
                RefreshTileVisual(tileCoords);
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"Error updating tile property {property}: {ex.Message}");
            }
        }

        // Backwards compatibility overload
        public void UpdateTileProperty(Vector2I tileCoords, string property, object value)
        {
            try
            {
                var tileProperty = TilePropertyExtensions.FromPropertyString(property);
                UpdateTileProperty(tileCoords, tileProperty, value);
            }
            catch (System.ArgumentException ex)
            {
                GD.PrintErr($"Invalid property name '{property}': {ex.Message}");
            }
        }

        public Tile? GetTileData(Vector2I tileCoords)
        {
            if (_dmapFile == null || !IsValidTileCoordinate(tileCoords))
                return null;

            return _dmapFile.TileSet[tileCoords.X, tileCoords.Y];
        }

        public System.Collections.Generic.List<Vector2I> GetSelectedTiles()
        {
            return new System.Collections.Generic.List<Vector2I>(_selectedTiles);
        }

        private bool IsValidTileCoordinate(Vector2I coords)
        {
            return coords.X >= 0 && coords.X < MapSize.X &&
                   coords.Y >= 0 && coords.Y < MapSize.Y;
        }

        private void RefreshTileVisual(Vector2I tileCoords)
        {
            if (_terrainLayer != null && _dmapFile != null)
            {
                var tile = _dmapFile.TileSet[tileCoords.X, tileCoords.Y];
                // Update custom data for the tile if needed
                // This will be expanded when the tileset creation is complete
            }
        }
    }
}
