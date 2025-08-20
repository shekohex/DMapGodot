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
    }
}