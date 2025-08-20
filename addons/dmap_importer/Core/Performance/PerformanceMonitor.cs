using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DMapImporter.Core.Performance
{
    public class PerformanceStats
    {
        public double FPS { get; set; }
        public long MemoryUsageMB { get; set; }
        public int VisibleTiles { get; set; }
        public int VisibleObjects { get; set; }
        public int ActiveChunks { get; set; }
        public int PooledObjects { get; set; }
        public double FrameTimeMs { get; set; }
        public Dictionary<LODLevel, int> LODDistribution { get; set; } = new();
        
        public override string ToString()
        {
            return $"FPS: {FPS:F1}, Memory: {MemoryUsageMB}MB, Tiles: {VisibleTiles}, Objects: {VisibleObjects}, " +
                   $"Chunks: {ActiveChunks}, Pool: {PooledObjects}, Frame: {FrameTimeMs:F2}ms";
        }
    }
    
    public class PerformanceMonitor : IDisposable
    {
        private Stopwatch _frameTimer = new();
        private List<double> _frameHistory = new();
        private const int MAX_FRAME_HISTORY = 60; // Keep 1 second of history at 60fps
        private bool _disposed = false;
        
        private ChunkManager? _chunkManager;
        private LODSystem? _lodSystem;
        private SpritePool? _spritePool;
        private MarkerPool? _markerPool;
        
        private double _updateInterval = 1.0; // Update stats every second
        private double _lastUpdateTime = 0.0;
        
        public PerformanceStats CurrentStats { get; private set; } = new();
        
        public void SetReferences(ChunkManager? chunkManager, LODSystem? lodSystem, 
                                SpritePool? spritePool, MarkerPool? markerPool)
        {
            _chunkManager = chunkManager;
            _lodSystem = lodSystem;
            _spritePool = spritePool;
            _markerPool = markerPool;
        }
        
        public void StartFrame()
        {
            _frameTimer.Restart();
        }
        
        public void EndFrame()
        {
            _frameTimer.Stop();
            var frameTime = _frameTimer.Elapsed.TotalMilliseconds;
            
            _frameHistory.Add(frameTime);
            if (_frameHistory.Count > MAX_FRAME_HISTORY)
            {
                _frameHistory.RemoveAt(0);
            }
        }
        
        public void Update(double delta)
        {
            _lastUpdateTime += delta;
            
            if (_lastUpdateTime >= _updateInterval)
            {
                UpdateStats();
                _lastUpdateTime = 0.0;
            }
        }
        
        private void UpdateStats()
        {
            // Calculate FPS from frame history
            if (_frameHistory.Count > 0)
            {
                double avgFrameTime = 0.0;
                foreach (var time in _frameHistory)
                {
                    avgFrameTime += time;
                }
                avgFrameTime /= _frameHistory.Count;
                
                CurrentStats.FPS = avgFrameTime > 0 ? 1000.0 / avgFrameTime : 0.0;
                CurrentStats.FrameTimeMs = avgFrameTime;
            }
            
            // Memory usage
            CurrentStats.MemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024);
            
            // Chunk statistics
            if (_chunkManager != null)
            {
                CurrentStats.ActiveChunks = _chunkManager.GetVisibleChunkCount();
            }
            
            // LOD statistics
            if (_lodSystem != null)
            {
                CurrentStats.LODDistribution = _lodSystem.GetLODDistribution();
            }
            
            // Pool statistics
            int pooledObjects = 0;
            if (_spritePool != null) pooledObjects += _spritePool.ActiveCount;
            if (_markerPool != null) pooledObjects += _markerPool.ActiveCount;
            CurrentStats.PooledObjects = pooledObjects;
        }
        
        public void SetVisibleCounts(int tiles, int objects)
        {
            CurrentStats.VisibleTiles = tiles;
            CurrentStats.VisibleObjects = objects;
        }
        
        public double GetAverageFPS()
        {
            return CurrentStats.FPS;
        }
        
        public double GetMinFPS()
        {
            if (_frameHistory.Count == 0) return 0.0;
            
            double maxFrameTime = 0.0;
            foreach (var time in _frameHistory)
            {
                if (time > maxFrameTime) maxFrameTime = time;
            }
            
            return maxFrameTime > 0 ? 1000.0 / maxFrameTime : 0.0;
        }
        
        public double GetMaxFPS()
        {
            if (_frameHistory.Count == 0) return 0.0;
            
            double minFrameTime = double.MaxValue;
            foreach (var time in _frameHistory)
            {
                if (time < minFrameTime) minFrameTime = time;
            }
            
            return minFrameTime > 0 ? 1000.0 / minFrameTime : 0.0;
        }
        
        public void LogPerformanceReport()
        {
            GD.Print($"=== Performance Report ===");
            GD.Print($"FPS: Avg={GetAverageFPS():F1}, Min={GetMinFPS():F1}, Max={GetMaxFPS():F1}");
            GD.Print($"Memory Usage: {CurrentStats.MemoryUsageMB} MB");
            GD.Print($"Rendering: {CurrentStats.VisibleTiles} tiles, {CurrentStats.VisibleObjects} objects");
            GD.Print($"Chunks: {CurrentStats.ActiveChunks} visible");
            GD.Print($"Object Pool: {CurrentStats.PooledObjects} active");
            
            if (CurrentStats.LODDistribution.Count > 0)
            {
                GD.Print($"LOD Distribution: High={CurrentStats.LODDistribution.GetValueOrDefault(LODLevel.High)}, " +
                        $"Medium={CurrentStats.LODDistribution.GetValueOrDefault(LODLevel.Medium)}, " +
                        $"Low={CurrentStats.LODDistribution.GetValueOrDefault(LODLevel.Low)}, " +
                        $"Hidden={CurrentStats.LODDistribution.GetValueOrDefault(LODLevel.Hidden)}");
            }
            GD.Print($"========================");
        }
        
        public bool MeetsPerformanceTargets()
        {
            return CurrentStats.FPS >= 60.0 && CurrentStats.MemoryUsageMB <= 500;
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _frameTimer?.Stop();
                _frameHistory?.Clear();
                _disposed = true;
            }
        }
    }
}