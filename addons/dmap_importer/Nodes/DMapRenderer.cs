using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using System;

namespace DMapImporter.Nodes
{
    [Tool]
    public partial class DMapRenderer : Node2D
    {
        [Export] public string DMapPath { get; set; } = string.Empty;
        [Export] public Vector2I MapSize { get; set; }
        [Export] public int TileSize { get; set; } = 32;
        
        private DmapFile? _dmapFile;
        private TileMapLayer? _terrainLayer;
        private Node2D? _objectLayer;
        
        public override void _Ready()
        {
            if (Engine.IsEditorHint())
            {
                SetNotifyTransform(true);
            }
        }
        
        public void LoadFromDMap(DmapFile dmap)
        {
            _dmapFile = dmap;
            DMapPath = dmap.DmapPath;
            MapSize = new Vector2I((int)dmap.SizeTiles.Width, (int)dmap.SizeTiles.Height);
            
            ClearChildren();
            CreateTerrainLayer();
            CreateObjectLayer();
            PopulateFromDMap();
        }
        
        private void ClearChildren()
        {
            foreach (Node child in GetChildren())
            {
                child.QueueFree();
            }
        }
        
        private void CreateTerrainLayer()
        {
            _terrainLayer = new TileMapLayer();
            _terrainLayer.Name = "TerrainLayer";
            AddChild(_terrainLayer);
            
            if (Engine.IsEditorHint())
            {
                _terrainLayer.Owner = GetTree()?.EditedSceneRoot;
            }
        }
        
        private void CreateObjectLayer()
        {
            _objectLayer = new Node2D();
            _objectLayer.Name = "ObjectLayer";
            AddChild(_objectLayer);
            
            if (Engine.IsEditorHint())
            {
                _objectLayer.Owner = GetTree()?.EditedSceneRoot;
            }
        }
        
        private void PopulateFromDMap()
        {
            if (_dmapFile == null) return;
            
            PopulateTerrain();
            PopulatePortals();
            PopulateObjects();
        }
        
        private void PopulateTerrain()
        {
            if (_dmapFile == null || _terrainLayer == null) return;
            
            for (int x = 0; x < (int)_dmapFile.SizeTiles.Width; x++)
            {
                for (int y = 0; y < (int)_dmapFile.SizeTiles.Height; y++)
                {
                    var tile = _dmapFile.TileSet[x, y];
                    if (tile.Access > 0)
                    {
                        Vector2I cellCoord = new Vector2I(x, y);
                        _terrainLayer.SetCell(cellCoord, 0, Vector2I.Zero);
                    }
                }
            }
        }
        
        private void PopulatePortals()
        {
            if (_dmapFile == null || _objectLayer == null) return;
            
            foreach (var portal in _dmapFile.Portals)
            {
                var portalMarker = new Marker2D();
                portalMarker.Name = $"Portal_{portal.Id}";
                portalMarker.Position = new Vector2(portal.Position.X * TileSize, portal.Position.Y * TileSize);
                
                _objectLayer.AddChild(portalMarker);
                
                if (Engine.IsEditorHint())
                {
                    portalMarker.Owner = GetTree()?.EditedSceneRoot;
                }
            }
        }
        
        private void PopulateObjects()
        {
            if (_dmapFile == null || _objectLayer == null) return;
            
            foreach (var cover in _dmapFile.Covers)
            {
                var coverMarker = new Marker2D();
                coverMarker.Name = $"Cover_{cover.AniName}";
                coverMarker.Position = new Vector2(cover.Position.X * TileSize, cover.Position.Y * TileSize);
                
                _objectLayer.AddChild(coverMarker);
                
                if (Engine.IsEditorHint())
                {
                    coverMarker.Owner = GetTree()?.EditedSceneRoot;
                }
            }
        }
        
        public DmapFile? GetDMapFile() => _dmapFile;
    }
}