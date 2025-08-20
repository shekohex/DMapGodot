using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;
using System.Drawing;

namespace DMapImporter.Nodes
{
    [Tool]
    public partial class DMapPortal : Area2D
    {
        [Export] public uint PortalId { get; set; }
        [Export] public string DestinationMap { get; set; } = "";
        [Export] public Vector2I DestinationPos { get; set; }

        private Sprite2D? _sprite;
        private CollisionShape2D? _collision;

        public DMapPortal()
        {
            SetupVisualComponents();
        }

        public DMapPortal(Portal portal, CordConverter converter) : this()
        {
            PortalId = portal.Id;
            Name = $"Portal_{portal.Id}";
            SetPortalPosition(portal, converter);
        }

        public override void _Ready()
        {
            if (_sprite == null || _collision == null)
            {
                SetupVisualComponents();
            }

            BodyEntered += OnBodyEntered;
            Monitoring = true;

            if (Engine.IsEditorHint())
            {
                var root = GetTree()?.EditedSceneRoot;
                if (root != null)
                {
                    SetOwnerRecursive(root);
                }
            }
        }

        private void SetupVisualComponents()
        {
            _sprite = new Sprite2D();
            var texture = GD.Load<Texture2D>("res://addons/dmap_importer/icons/portal.png");
            if (texture != null)
            {
                _sprite.Texture = texture;
            }
            else
            {
                _sprite.Modulate = Colors.Magenta;
                var placeholderTexture = new PlaceholderTexture2D();
                placeholderTexture.Size = new Vector2I(64, 64);
                _sprite.Texture = placeholderTexture;
            }
            AddChild(_sprite);

            _collision = new CollisionShape2D();
            var shape = new CircleShape2D();
            shape.Radius = 32;
            _collision.Shape = shape;
            AddChild(_collision);
        }

        private void SetOwnerRecursive(Node owner)
        {
            if (owner == null) return;

            Owner = owner;
            foreach (Node child in GetChildren())
            {
                if (child is DMapPortal portal)
                {
                    portal.SetOwnerRecursive(owner);
                }
                else
                {
                    child.Owner = owner;
                }
            }
        }

        private void OnBodyEntered(Node2D body)
        {
            if (!body.IsInGroup("player")) return;

            if (string.IsNullOrEmpty(DestinationMap))
            {
                GD.PrintErr($"Portal {PortalId}: No destination map specified");
                return;
            }

            string scenePath = $"res://maps/{DestinationMap}.tscn";

            if (!FileAccess.FileExists(scenePath))
            {
                GD.PrintErr($"Portal {PortalId}: Destination scene not found: {scenePath}");
                return;
            }

            GetTree().ChangeSceneToFile(scenePath);
        }

        public void SetPortalPosition(Portal portal, CordConverter converter)
        {
            var cellPoint = new Point((int)portal.Position.X, (int)portal.Position.Y);
            var worldPoint = converter.Cell2World(cellPoint);
            Position = new Vector2(worldPoint.X, worldPoint.Y);
        }
    }
}