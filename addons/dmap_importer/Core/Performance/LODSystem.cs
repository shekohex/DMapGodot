using Godot;
using System;
using System.Collections.Generic;

namespace DMapImporter.Core.Performance
{
    public enum LODLevel
    {
        High = 0,    // Full detail - close to camera
        Medium = 1,  // Reduced detail - medium distance
        Low = 2,     // Minimal detail - far from camera
        Hidden = 3   // Too far, completely hidden
    }
    
    public class LODSettings
    {
        public float HighDetailDistance { get; set; } = 500.0f;
        public float MediumDetailDistance { get; set; } = 1000.0f;
        public float LowDetailDistance { get; set; } = 2000.0f;
        public float HiddenDistance { get; set; } = 3000.0f;
        
        public Vector2 MediumDetailScale { get; set; } = new Vector2(0.75f, 0.75f);
        public Vector2 LowDetailScale { get; set; } = new Vector2(0.5f, 0.5f);
        
        public float MediumDetailAlpha { get; set; } = 0.9f;
        public float LowDetailAlpha { get; set; } = 0.7f;
    }
    
    public interface ILODObject
    {
        Vector2 GetWorldPosition();
        void SetLODLevel(LODLevel level, LODSettings settings);
        LODLevel GetCurrentLODLevel();
    }
    
    public partial class LODSprite : Sprite2D, ILODObject
    {
        private LODLevel _currentLOD = LODLevel.High;
        private Vector2 _originalScale = Vector2.One;
        private Color _originalModulate = Colors.White;
        
        public override void _Ready()
        {
            _originalScale = Scale;
            _originalModulate = Modulate;
        }
        
        public Vector2 GetWorldPosition()
        {
            return GlobalPosition;
        }
        
        public void SetLODLevel(LODLevel level, LODSettings settings)
        {
            if (_currentLOD == level) return;
            
            _currentLOD = level;
            
            switch (level)
            {
                case LODLevel.High:
                    Scale = _originalScale;
                    Modulate = _originalModulate;
                    Visible = true;
                    break;
                    
                case LODLevel.Medium:
                    Scale = _originalScale * settings.MediumDetailScale;
                    Modulate = new Color(_originalModulate.R, _originalModulate.G, _originalModulate.B, 
                                       _originalModulate.A * settings.MediumDetailAlpha);
                    Visible = true;
                    break;
                    
                case LODLevel.Low:
                    Scale = _originalScale * settings.LowDetailScale;
                    Modulate = new Color(_originalModulate.R, _originalModulate.G, _originalModulate.B,
                                       _originalModulate.A * settings.LowDetailAlpha);
                    Visible = true;
                    break;
                    
                case LODLevel.Hidden:
                    Visible = false;
                    break;
            }
        }
        
        public LODLevel GetCurrentLODLevel()
        {
            return _currentLOD;
        }
    }
    
    public class LODSystem
    {
        private Camera2D? _camera;
        private LODSettings _settings = new();
        private List<ILODObject> _lodObjects = new();
        private float _updateInterval = 0.1f; // Update LOD every 100ms
        private float _lastUpdateTime = 0.0f;
        
        public LODSystem(Camera2D camera)
        {
            _camera = camera;
        }
        
        public void SetLODSettings(LODSettings settings)
        {
            _settings = settings;
        }
        
        public void RegisterLODObject(ILODObject obj)
        {
            if (!_lodObjects.Contains(obj))
            {
                _lodObjects.Add(obj);
            }
        }
        
        public void UnregisterLODObject(ILODObject obj)
        {
            _lodObjects.Remove(obj);
        }
        
        public void Update(double delta)
        {
            _lastUpdateTime += (float)delta;
            
            if (_lastUpdateTime < _updateInterval) return;
            _lastUpdateTime = 0.0f;
            
            if (_camera == null) return;
            
            var cameraPosition = _camera.GlobalPosition;
            var zoom = _camera.Zoom.X; // Assume uniform zoom
            
            foreach (var obj in _lodObjects)
            {
                var distance = cameraPosition.DistanceTo(obj.GetWorldPosition()) / zoom;
                var newLOD = CalculateLODLevel(distance);
                
                obj.SetLODLevel(newLOD, _settings);
            }
        }
        
        private LODLevel CalculateLODLevel(float distance)
        {
            if (distance <= _settings.HighDetailDistance)
                return LODLevel.High;
            else if (distance <= _settings.MediumDetailDistance)
                return LODLevel.Medium;
            else if (distance <= _settings.LowDetailDistance)
                return LODLevel.Low;
            else if (distance <= _settings.HiddenDistance)
                return LODLevel.Hidden;
            else
                return LODLevel.Hidden;
        }
        
        public void SetUpdateInterval(float interval)
        {
            _updateInterval = Math.Max(0.01f, interval); // Minimum 10ms
        }
        
        public int GetObjectCount()
        {
            return _lodObjects.Count;
        }
        
        public Dictionary<LODLevel, int> GetLODDistribution()
        {
            var distribution = new Dictionary<LODLevel, int>
            {
                [LODLevel.High] = 0,
                [LODLevel.Medium] = 0,
                [LODLevel.Low] = 0,
                [LODLevel.Hidden] = 0
            };
            
            foreach (var obj in _lodObjects)
            {
                var level = obj.GetCurrentLODLevel();
                distribution[level]++;
            }
            
            return distribution;
        }
        
        public void ClearObjects()
        {
            _lodObjects.Clear();
        }
    }
}