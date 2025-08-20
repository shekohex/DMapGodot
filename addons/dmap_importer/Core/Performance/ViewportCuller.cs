using Godot;
using System;

namespace DMapImporter.Core.Performance
{
    public class ViewportCuller
    {
        private Camera2D? _camera;
        private Rect2 _cullingBounds;
        private float _cullingMargin = 128.0f; // Extra margin to avoid pop-in
        
        public ViewportCuller(Camera2D camera)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        }
        
        public void SetCullingMargin(float margin)
        {
            _cullingMargin = margin;
        }
        
        public void UpdateCullingBounds()
        {
            if (_camera == null) return;
            
            var viewport = _camera.GetViewport();
            if (viewport == null) return;
            
            var viewportSize = viewport.GetVisibleRect().Size;
            var cameraPosition = _camera.GlobalPosition;
            var zoom = _camera.Zoom;
            
            // Calculate the visible area in world coordinates
            var halfSize = viewportSize / (2.0f * zoom);
            
            // Add margin to prevent pop-in effects
            var marginVector = new Vector2(_cullingMargin, _cullingMargin);
            
            _cullingBounds = new Rect2(
                cameraPosition - halfSize - marginVector,
                (halfSize * 2.0f) + (marginVector * 2.0f)
            );
        }
        
        public bool ShouldCullTile(Vector2 tileWorldPosition, Vector2 tileSize)
        {
            if (_camera == null) return false;
            
            var tileRect = new Rect2(tileWorldPosition, tileSize);
            return !_cullingBounds.Intersects(tileRect);
        }
        
        public bool ShouldCullObject(Vector2 objectPosition, Vector2 objectSize)
        {
            if (_camera == null) return false;
            
            var objectRect = new Rect2(objectPosition - objectSize * 0.5f, objectSize);
            return !_cullingBounds.Intersects(objectRect);
        }
        
        public Rect2 GetCullingBounds()
        {
            return _cullingBounds;
        }
        
        public Rect2I GetVisibleTileRange(Vector2I tileSize, Vector2I mapSize)
        {
            if (_camera == null) return new Rect2I(0, 0, mapSize.X, mapSize.Y);
            
            if (tileSize.X <= 0 || tileSize.Y <= 0)
            {
                GD.PrintErr($"Invalid tile size: {tileSize}");
                return new Rect2I(0, 0, mapSize.X, mapSize.Y);
            }
            
            if (mapSize.X <= 0 || mapSize.Y <= 0)
            {
                GD.PrintErr($"Invalid map size: {mapSize}");
                return new Rect2I(0, 0, 1, 1);
            }
            
            // Convert world bounds to tile coordinates
            var minTileX = Math.Max(0, (int)Math.Floor(_cullingBounds.Position.X / tileSize.X));
            var minTileY = Math.Max(0, (int)Math.Floor(_cullingBounds.Position.Y / tileSize.Y));
            var maxTileX = Math.Min(mapSize.X - 1, (int)Mathf.Ceil(_cullingBounds.End.X / tileSize.X));
            var maxTileY = Math.Min(mapSize.Y - 1, (int)Mathf.Ceil(_cullingBounds.End.Y / tileSize.Y));
            
            return new Rect2I(
                minTileX, 
                minTileY,
                maxTileX - minTileX + 1,
                maxTileY - minTileY + 1
            );
        }
    }
}