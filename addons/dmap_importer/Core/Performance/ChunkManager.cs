using Godot;
using System;
using System.Collections.Generic;
using DMapImporter.Core.Dmap;

namespace DMapImporter.Core.Performance
{
    public class MapChunk
    {
        public Vector2I ChunkCoordinate { get; set; }
        public Rect2I TileRange { get; set; }
        public List<Node2D> ChunkObjects { get; set; } = new();
        public bool IsLoaded { get; set; } = false;
        public bool IsVisible { get; set; } = false;
        
        public MapChunk(Vector2I coordinate, Rect2I range)
        {
            ChunkCoordinate = coordinate;
            TileRange = range;
        }
    }
    
    public class ChunkManager
    {
        private const int CHUNK_SIZE = 256;
        
        private Dictionary<Vector2I, MapChunk> _chunks = new();
        private DmapFile? _dmapFile;
        private Vector2I _mapSize;
        private Node2D? _chunkContainer;
        
        public ChunkManager(DmapFile dmapFile, Node2D chunkContainer)
        {
            if (dmapFile == null)
            {
                throw new ArgumentNullException(nameof(dmapFile));
            }
            
            if (chunkContainer == null)
            {
                throw new ArgumentNullException(nameof(chunkContainer));
            }
            
            _dmapFile = dmapFile;
            _mapSize = new Vector2I((int)dmapFile.SizeTiles.Width, (int)dmapFile.SizeTiles.Height);
            _chunkContainer = chunkContainer;
            
            if (_mapSize.X <= 0 || _mapSize.Y <= 0)
            {
                throw new ArgumentException($"Invalid map size: {_mapSize}");
            }
            
            InitializeChunks();
        }
        
        private void InitializeChunks()
        {
            var chunksX = (int)Math.Ceiling((float)_mapSize.X / CHUNK_SIZE);
            var chunksY = (int)Math.Ceiling((float)_mapSize.Y / CHUNK_SIZE);
            
            for (int chunkX = 0; chunkX < chunksX; chunkX++)
            {
                for (int chunkY = 0; chunkY < chunksY; chunkY++)
                {
                    var chunkCoord = new Vector2I(chunkX, chunkY);
                    
                    var startX = chunkX * CHUNK_SIZE;
                    var startY = chunkY * CHUNK_SIZE;
                    var endX = Math.Min(startX + CHUNK_SIZE, _mapSize.X);
                    var endY = Math.Min(startY + CHUNK_SIZE, _mapSize.Y);
                    
                    var tileRange = new Rect2I(startX, startY, endX - startX, endY - startY);
                    var chunk = new MapChunk(chunkCoord, tileRange);
                    
                    _chunks[chunkCoord] = chunk;
                }
            }
        }
        
        public void UpdateVisibleChunks(ViewportCuller culler, Vector2I tileSize)
        {
            var visibleTileRange = culler.GetVisibleTileRange(tileSize, _mapSize);
            
            foreach (var chunk in _chunks.Values)
            {
                bool shouldBeVisible = chunk.TileRange.Intersects(visibleTileRange);
                
                if (shouldBeVisible && !chunk.IsVisible)
                {
                    ShowChunk(chunk);
                }
                else if (!shouldBeVisible && chunk.IsVisible)
                {
                    HideChunk(chunk);
                }
            }
        }
        
        private void ShowChunk(MapChunk chunk)
        {
            chunk.IsVisible = true;
            
            foreach (var obj in chunk.ChunkObjects)
            {
                if (obj != null)
                {
                    obj.Visible = true;
                }
            }
        }
        
        private void HideChunk(MapChunk chunk)
        {
            chunk.IsVisible = false;
            
            foreach (var obj in chunk.ChunkObjects)
            {
                if (obj != null)
                {
                    obj.Visible = false;
                }
            }
        }
        
        public MapChunk? GetChunkForTile(Vector2I tileCoordinate)
        {
            var chunkCoord = new Vector2I(
                tileCoordinate.X / CHUNK_SIZE,
                tileCoordinate.Y / CHUNK_SIZE
            );
            
            return _chunks.GetValueOrDefault(chunkCoord);
        }
        
        public void AddObjectToChunk(Vector2I tileCoordinate, Node2D obj)
        {
            var chunk = GetChunkForTile(tileCoordinate);
            if (chunk != null)
            {
                chunk.ChunkObjects.Add(obj);
                obj.Visible = chunk.IsVisible;
            }
        }
        
        public Dictionary<Vector2I, MapChunk> GetAllChunks()
        {
            return _chunks;
        }
        
        public int GetLoadedChunkCount()
        {
            int count = 0;
            foreach (var chunk in _chunks.Values)
            {
                if (chunk.IsLoaded) count++;
            }
            return count;
        }
        
        public int GetVisibleChunkCount()
        {
            int count = 0;
            foreach (var chunk in _chunks.Values)
            {
                if (chunk.IsVisible) count++;
            }
            return count;
        }
    }
}