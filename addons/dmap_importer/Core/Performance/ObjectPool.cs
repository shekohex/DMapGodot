using Godot;
using System;
using System.Collections.Generic;

namespace DMapImporter.Core.Performance
{
    public class ObjectPool<T> where T : CanvasItem, new()
    {
        private readonly Stack<T> _pool = new();
        private readonly Node _parentNode;
        private readonly int _initialSize;
        private readonly int _maxSize;
        private int _createdCount = 0;
        
        public ObjectPool(Node parentNode, int initialSize = 10, int maxSize = 100)
        {
            _parentNode = parentNode;
            _initialSize = initialSize;
            _maxSize = maxSize;
            
            PrewarmPool();
        }
        
        private void PrewarmPool()
        {
            for (int i = 0; i < _initialSize; i++)
            {
                var item = CreateNewItem();
                item.Visible = false;
                _pool.Push(item);
            }
        }
        
        private T CreateNewItem()
        {
            var item = new T();
            _parentNode.AddChild(item);
            _createdCount++;
            return item;
        }
        
        public T Get()
        {
            if (_pool.Count > 0)
            {
                var item = _pool.Pop();
                item.Visible = true;
                return item;
            }
            
            if (_createdCount < _maxSize)
            {
                return CreateNewItem();
            }
            
            // If we've reached max size and no objects available, create temporary one
            GD.PrintErr($"ObjectPool<{typeof(T).Name}> reached maximum size ({_maxSize}). Creating temporary object.");
            return CreateNewItem();
        }
        
        public void Return(T item)
        {
            if (item == null) return;
            
            // Check if item is still valid (not already queued for deletion)
            if (!IsInstanceValid(item))
            {
                _createdCount--;
                return;
            }
            
            // Reset the item to default state
            ResetItem(item);
            item.Visible = false;
            
            if (_pool.Count < _maxSize)
            {
                _pool.Push(item);
            }
            else
            {
                // Pool is full, destroy the item
                item.QueueFree();
                _createdCount--;
            }
        }
        
        private bool IsInstanceValid(T item)
        {
            try
            {
                // Try to access a property to check if object is still valid
                _ = item.Name;
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        protected virtual void ResetItem(T item)
        {
            // Override in derived classes for specific reset logic
            if (item is Sprite2D sprite)
            {
                sprite.Position = Vector2.Zero;
                sprite.Rotation = 0;
                sprite.Scale = Vector2.One;
                sprite.Modulate = Colors.White;
                sprite.Texture = null;
            }
            else if (item is Marker2D marker)
            {
                marker.Position = Vector2.Zero;
            }
        }
        
        public int AvailableCount => _pool.Count;
        public int CreatedCount => _createdCount;
        public int ActiveCount => _createdCount - _pool.Count;
    }
    
    public class SpritePool : ObjectPool<Sprite2D>
    {
        public SpritePool(Node parentNode, int initialSize = 20, int maxSize = 200) 
            : base(parentNode, initialSize, maxSize)
        {
        }
        
        protected override void ResetItem(Sprite2D sprite)
        {
            base.ResetItem(sprite);
            sprite.Name = "PooledSprite";
        }
    }
    
    public class MarkerPool : ObjectPool<Marker2D>
    {
        public MarkerPool(Node parentNode, int initialSize = 10, int maxSize = 100)
            : base(parentNode, initialSize, maxSize)
        {
        }
        
        protected override void ResetItem(Marker2D marker)
        {
            base.ResetItem(marker);
            marker.Name = "PooledMarker";
        }
    }
}