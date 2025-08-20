using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using System;
using System.Drawing;

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

        private void PlaceObjectMarkers()
        {
            if (_dmapFile == null || _objectLayer == null || _cordConverter == null) return;

            // Place DMapPortal nodes for portals
            foreach (var portal in _dmapFile.Portals)
            {
                var portalNode = new DMapPortal(portal, _cordConverter);
                _objectLayer.AddChild(portalNode);

                if (Engine.IsEditorHint())
                {
                    portalNode.Owner = GetTree()?.EditedSceneRoot;
                }
            }

            // Place markers for covers
            foreach (var cover in _dmapFile.Covers)
            {
                var marker = new Marker2D();
                marker.Name = $"Cover_{cover.AniName}";
                marker.Position = new Vector2(
                    cover.Position.X * 64,
                    cover.Position.Y * 32
                );
                _objectLayer.AddChild(marker);

                if (Engine.IsEditorHint())
                {
                    marker.Owner = GetTree()?.EditedSceneRoot;
                }
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