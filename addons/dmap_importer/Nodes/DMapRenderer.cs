using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using DMapGodot.Importers;
using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;

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
        [Export] public string DMapPath { get; set; } = string.Empty;
        [Export] public Vector2I MapSize { get; set; }
        [Export] public int TileSize { get; set; } = 32;

        private DmapFile? _dmapFile;
        private TileMapLayer? _backgroundLayer;
        private TileMapLayer? _terrainLayer;
        private Node2D? _objectLayer;
        private CoordinateHelper? _coordinateHelper;
        private CordConverter? _cordConverter;
        private Dictionary<string, SceneFile> _loadedSceneFiles = new();
        private string _clientPath = string.Empty;

        public override void _Ready()
        {
            if (Engine.IsEditorHint())
            {
                SetNotifyTransform(true);
            }
        }

        public void LoadFromDMap(DmapFile dmap)
        {
            if (dmap == null)
            {
                GD.PrintErr("Cannot load null DmapFile");
                return;
            }

            _dmapFile = dmap;
            DMapPath = dmap.DmapPath;
            MapSize = new Vector2I((int)dmap.SizeTiles.Width, (int)dmap.SizeTiles.Height);

            // Extract client path from DMapPath
            _clientPath = ExtractClientPath(dmap.DmapPath);

            // Initialize coordinate helper
            _coordinateHelper = new CoordinateHelper(dmap);

            // Initialize CordConverter for portals
            var dmapSize = new System.Drawing.Size(
                (int)dmap.SizeTiles.Width,
                (int)dmap.SizeTiles.Height
            );
            var bgSize = new System.Drawing.Size(256, 256);
            _cordConverter = new CordConverter(dmapSize, bgSize);

            ClearChildren();
            CreateLayers();
            CreateSceneLayerManagement();
            CreateSelectionLayer();
            PopulateFromDMap();
        }

        private void ClearChildren()
        {
            foreach (Node child in GetChildren())
            {
                child.QueueFree();
            }
        }

        private void CreateLayers()
        {
            // Background Layer (Puzzle pieces)
            _backgroundLayer = new TileMapLayer();
            _backgroundLayer.Name = "BackgroundLayer";
            _backgroundLayer.ZIndex = 0;
            _backgroundLayer.Enabled = true;
            AddChild(_backgroundLayer);

            // Terrain Layer (Walkable/Surface data)
            _terrainLayer = new TileMapLayer();
            _terrainLayer.Name = "TerrainLayer";
            _terrainLayer.ZIndex = 1;
            _terrainLayer.Enabled = true;
            AddChild(_terrainLayer);

            // Object Layer (3D objects with Y-sorting)
            _objectLayer = new Node2D();
            _objectLayer.Name = "ObjectLayer";
            _objectLayer.ZIndex = 2;
            _objectLayer.YSortEnabled = true;
            AddChild(_objectLayer);

            // Set owner for editor visibility
            if (Engine.IsEditorHint())
            {
                var root = GetTree()?.EditedSceneRoot;
                if (root != null)
                {
                    _backgroundLayer.Owner = root;
                    _terrainLayer.Owner = root;
                    _objectLayer.Owner = root;
                }
            }

            // Create and assign separate TileSets
            var puzzleTileSet = CreatePuzzleTileSet();
            var terrainTileSet = CreateTerrainTileSet();

            _backgroundLayer.TileSet = puzzleTileSet;
            _terrainLayer.TileSet = terrainTileSet;
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

            return tileSet;
        }

        private TileSet CreatePuzzleTileSet()
        {
            var tileSet = new TileSet();
            tileSet.TileShape = TileSet.TileShapeEnum.Isometric;
            tileSet.TileSize = new Vector2I(64, 32);
            tileSet.TileLayout = TileSet.TileLayoutEnum.Stacked;

            // No custom data layers for background/puzzle layer

            return tileSet;
        }

        private void PopulateFromDMap()
        {
            if (_dmapFile == null) return;

            PlaceTerrainTiles();
            PlaceObjectMarkers();
        }

        private void PlaceTerrainTiles()
        {
            if (_dmapFile == null || _terrainLayer == null) return;

            for (int x = 0; x < _dmapFile.SizeTiles.Width; x++)
            {
                for (int y = 0; y < _dmapFile.SizeTiles.Height; y++)
                {
                    var tile = _dmapFile.TileSet[x, y];

                    // Only place if accessible
                    if (tile.Access > 0)
                    {
                        var coords = new Vector2I(x, y);

                        // Place empty tile (source_id 0 will be added in Task 6)
                        _terrainLayer.SetCell(coords, -1, Vector2I.Zero, 0);

                        // Note: Custom data will be set when we have actual tiles
                    }
                }
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
                GD.PrintErr($"Error extracting client path from {dmapPath}: {ex.Message}");
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
                    GD.PrintErr($"Error rendering scene {terrainScene.SceneFile}: {ex.Message}");
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
                GD.PrintErr($"Failed to load scene file {sceneFilePath}: {ex.Message}");
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

            // Load texture for the scene part
            var texture = LoadSceneTexture(scenePart.AniPath, scenePart.AniName);
            if (texture == null) return;

            // Create sprite node for the scene part
            var sprite = new Sprite2D();
            sprite.Name = $"Scene_{scenePart.AniName}";
            sprite.Texture = texture;

            // Calculate final position with Y-sorting consideration
            var finalPosition = CalculateYSortedPosition(worldPos, scenePart.OffsetElevation);
            sprite.Position = finalPosition;

            // Add to appropriate layer
            var sceneLayer = GetSceneObjectsLayer() ?? _objectLayer;
            sceneLayer?.AddChild(sprite);

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

            // Load texture for the cover
            var texture = LoadSceneTexture(cover.AniPath, cover.AniName);
            if (texture == null)
            {
                // Fallback to marker if texture loading fails
                CreateCoverMarker(cover, worldPos);
                return;
            }

            // Create sprite node for the cover
            var sprite = new Sprite2D();
            sprite.Name = $"Cover_{cover.AniName}";
            sprite.Texture = texture;

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

            if (Engine.IsEditorHint())
            {
                var root = GetTree()?.EditedSceneRoot;
                if (root != null) sprite.Owner = root;
            }
        }

        private Vector2 CalculateScenePartPosition(ScenePart scenePart, TilePosition basePosition)
        {
            // Combine base position with scene part pixel location
            var totalX = (int)basePosition.X + (scenePart.PixelLocation.X / 32.0f);
            var totalY = (int)basePosition.Y + (scenePart.PixelLocation.Y / 16.0f);

            // Use coordinate converter for isometric positioning
            if (_cordConverter != null)
            {
                var worldPos = _cordConverter.Cell2World(
                    new System.Drawing.Point((int)totalX, (int)totalY)
                );
                return new Vector2(worldPos.X, worldPos.Y);
            }

            // Fallback to simple isometric calculation
            return new Vector2(totalX * 64, totalY * 32);
        }

        private Vector2 CalculateCoverPosition(Cover cover)
        {
            if (_cordConverter != null)
            {
                var worldPos = _cordConverter.Cell2World(
                    new System.Drawing.Point((int)cover.Position.X, (int)cover.Position.Y)
                );
                return new Vector2(worldPos.X, worldPos.Y);
            }

            // Fallback to simple isometric calculation
            return new Vector2((int)cover.Position.X * 64, (int)cover.Position.Y * 32);
        }

        private ImageTexture? LoadSceneTexture(string aniPath, string aniName)
        {
            try
            {
                // Construct full path to texture file
                var texturePath = Path.Combine(_clientPath, aniPath, $"{aniName}.dds");
                if (!File.Exists(texturePath))
                {
                    // Try alternative extensions
                    texturePath = Path.Combine(_clientPath, aniPath, $"{aniName}.bmp");
                    if (!File.Exists(texturePath))
                    {
                        texturePath = Path.Combine(_clientPath, aniPath, $"{aniName}.png");
                        if (!File.Exists(texturePath))
                        {
                            GD.PrintErr($"Scene texture not found: {aniPath}/{aniName}");
                            return null;
                        }
                    }
                }

                // Use TextureConverter for DDS files, direct loading for others
                if (texturePath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                {
                    return TextureConverter.ConvertDDSToTexture(texturePath);
                }
                else
                {
                    var image = Image.LoadFromFile(texturePath);
                    return image != null ? ImageTexture.CreateFromImage(image) : null;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Error loading scene texture {aniPath}/{aniName}: {ex.Message}");
                return null;
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
            // The objectLayer already exists with Y-sorting enabled
            // We can create sub-layers if needed for better organization
            if (_objectLayer == null) return;

            // Create sublayers for different object types
            var terrainObjectsLayer = new Node2D();
            terrainObjectsLayer.Name = "TerrainObjects";
            terrainObjectsLayer.YSortEnabled = true;
            terrainObjectsLayer.ZIndex = 0;

            var coverObjectsLayer = new Node2D();
            coverObjectsLayer.Name = "CoverObjects";
            coverObjectsLayer.YSortEnabled = true;
            coverObjectsLayer.ZIndex = 1; // Covers render above terrain objects

            var portalLayer = new Node2D();
            portalLayer.Name = "Portals";
            portalLayer.YSortEnabled = true;
            portalLayer.ZIndex = 2; // Portals on top

            _objectLayer.AddChild(terrainObjectsLayer);
            _objectLayer.AddChild(coverObjectsLayer);
            _objectLayer.AddChild(portalLayer);

            if (Engine.IsEditorHint())
            {
                var root = GetTree()?.EditedSceneRoot;
                if (root != null)
                {
                    terrainObjectsLayer.Owner = root;
                    coverObjectsLayer.Owner = root;
                    portalLayer.Owner = root;
                }
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