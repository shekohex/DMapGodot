#if TOOLS
using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Nodes;
using System;
using System.Collections.Generic;

namespace DMapImporter.Editor
{
    [Tool]
    public partial class DMapEditorDock : Control
    {
        private DMapRenderer? _currentRenderer;
        private Vector2I _selectedTile = new Vector2I(-1, -1);
        
        private VBoxContainer? _mainContainer;
        private Label? _titleLabel;
        private Label? _coordinatesLabel;
        private SpinBox? _heightEditor;
        private OptionButton? _surfaceSelector;
        private CheckBox? _walkableToggle;
        private Button? _applyButton;
        
        private Tile? _currentTileData;
        private bool _isUpdating = false;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(250, 0);
            
            _mainContainer = new VBoxContainer();
            _mainContainer.AddThemeConstantOverride("separation", 8);
            AddChild(_mainContainer);
            
            _titleLabel = new Label() { Text = "Tile Properties" };
            _titleLabel.AddThemeStyleboxOverride("normal", new StyleBoxFlat());
            _mainContainer.AddChild(_titleLabel);
            
            _mainContainer.AddChild(new HSeparator());
            
            _coordinatesLabel = new Label() { Text = "No tile selected" };
            _coordinatesLabel.AddThemeColorOverride("font_color", Colors.Gray);
            _mainContainer.AddChild(_coordinatesLabel);
            
            var heightLabel = new Label() { Text = "Height:" };
            _mainContainer.AddChild(heightLabel);
            
            _heightEditor = new SpinBox();
            _heightEditor.MinValue = -100;
            _heightEditor.MaxValue = 100;
            _heightEditor.Step = 1;
            _heightEditor.Value = 0;
            _heightEditor.Editable = false;
            _heightEditor.ValueChanged += OnHeightChanged;
            _mainContainer.AddChild(_heightEditor);
            
            var surfaceLabel = new Label() { Text = "Surface Type:" };
            _mainContainer.AddChild(surfaceLabel);
            
            _surfaceSelector = new OptionButton();
            _surfaceSelector.AddItem("Grass");
            _surfaceSelector.AddItem("Stone");  
            _surfaceSelector.AddItem("Water");
            _surfaceSelector.Disabled = true;
            _surfaceSelector.ItemSelected += OnSurfaceChanged;
            _mainContainer.AddChild(_surfaceSelector);
            
            _walkableToggle = new CheckBox();
            _walkableToggle.Text = "Walkable";
            _walkableToggle.Disabled = true;
            _walkableToggle.Toggled += OnWalkableToggled;
            _mainContainer.AddChild(_walkableToggle);
            
            _mainContainer.AddChild(new HSeparator());
            
            _applyButton = new Button();
            _applyButton.Text = "Apply to Selected";
            _applyButton.Disabled = true;
            _applyButton.Pressed += OnApplyPressed;
            _mainContainer.AddChild(_applyButton);
            
            var infoLabel = new Label();
            infoLabel.Text = "Select a tile in the viewport";
            infoLabel.AddThemeColorOverride("font_color", Colors.Gray);
            infoLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _mainContainer.AddChild(infoLabel);
        }

        private void OnHeightChanged(double value)
        {
            if (_isUpdating || _currentRenderer == null || _selectedTile.X < 0)
                return;
            
            UpdateTileProperty("height", (short)value);
        }

        private void OnSurfaceChanged(long index)
        {
            if (_isUpdating || _currentRenderer == null || _selectedTile.X < 0)
                return;
            
            UpdateTileProperty("surface", (ushort)index);
        }

        private void OnWalkableToggled(bool pressed)
        {
            if (_isUpdating || _currentRenderer == null || _selectedTile.X < 0)
                return;
            
            UpdateTileProperty("no_access", (ushort)(pressed ? 0 : 1));
        }

        private void OnApplyPressed()
        {
            ApplyToAllSelected();
        }

        public void SetCurrentRenderer(DMapRenderer? renderer)
        {
            if (_currentRenderer != null)
            {
                if (_currentRenderer.IsConnected("TileSelected", new Callable(this, nameof(OnTileSelected))))
                {
                    _currentRenderer.Disconnect("TileSelected", new Callable(this, nameof(OnTileSelected)));
                }
            }
            
            _currentRenderer = renderer;
            
            if (_currentRenderer != null)
            {
                _currentRenderer.Connect("TileSelected", new Callable(this, nameof(OnTileSelected)));
                RefreshUI();
            }
            else
            {
                ClearSelection();
            }
        }

        private void OnTileSelected(Vector2I tileCoords, short height, ushort surface, ushort noAccess)
        {
            _selectedTile = tileCoords;
            _currentTileData = new Tile(noAccess, surface, height);
            UpdateUIFromTileData();
        }

        private void ClearSelection()
        {
            _selectedTile = new Vector2I(-1, -1);
            _currentTileData = null;
            
            _isUpdating = true;
            
            if (_coordinatesLabel != null)
            {
                _coordinatesLabel.Text = "No tile selected";
                _coordinatesLabel.AddThemeColorOverride("font_color", Colors.Gray);
            }
            
            if (_heightEditor != null)
            {
                _heightEditor.Editable = false;
                _heightEditor.Value = 0;
            }
            
            if (_surfaceSelector != null)
            {
                _surfaceSelector.Disabled = true;
                _surfaceSelector.Selected = 0;
            }
            
            if (_walkableToggle != null)
            {
                _walkableToggle.Disabled = true;
                _walkableToggle.ButtonPressed = false;
            }
            
            if (_applyButton != null)
            {
                _applyButton.Disabled = true;
            }
            
            _isUpdating = false;
        }

        private void UpdateUIFromTileData()
        {
            if (_currentTileData == null)
            {
                ClearSelection();
                return;
            }
            
            _isUpdating = true;
            
            var tile = _currentTileData.Value;
            
            if (_coordinatesLabel != null)
            {
                _coordinatesLabel.Text = $"Tile [{_selectedTile.X}, {_selectedTile.Y}]";
                _coordinatesLabel.AddThemeColorOverride("font_color", Colors.White);
            }
            
            if (_heightEditor != null)
            {
                _heightEditor.Editable = true;
                _heightEditor.Value = tile.Height;
            }
            
            if (_surfaceSelector != null)
            {
                _surfaceSelector.Disabled = false;
                _surfaceSelector.Selected = Mathf.Clamp(tile.Surface, 0, 2);
            }
            
            if (_walkableToggle != null)
            {
                _walkableToggle.Disabled = false;
                _walkableToggle.ButtonPressed = (tile.NoAccess == 0);
            }
            
            if (_applyButton != null)
            {
                _applyButton.Disabled = false;
            }
            
            _isUpdating = false;
        }

        private void UpdateTileProperty(string property, object value)
        {
            if (_currentRenderer == null || _selectedTile.X < 0)
                return;
            
            _currentRenderer.UpdateTileProperty(_selectedTile, property, value);
            
            if (_currentTileData.HasValue)
            {
                var tile = _currentTileData.Value;
                
                switch (property)
                {
                    case "height":
                        tile = new Tile(tile.NoAccess, tile.Surface, (short)value);
                        break;
                    case "surface":
                        tile = new Tile(tile.NoAccess, (ushort)value, tile.Height);
                        break;
                    case "no_access":
                        tile = new Tile((ushort)value, tile.Surface, tile.Height);
                        break;
                }
                
                _currentTileData = tile;
            }
        }

        private void ApplyToAllSelected()
        {
            if (_currentRenderer == null || !_currentTileData.HasValue)
                return;
            
            var selectedTiles = _currentRenderer.GetSelectedTiles();
            
            foreach (var tileCoord in selectedTiles)
            {
                _currentRenderer.UpdateTileProperty(tileCoord, "height", _currentTileData.Value.Height);
                _currentRenderer.UpdateTileProperty(tileCoord, "surface", _currentTileData.Value.Surface);
                _currentRenderer.UpdateTileProperty(tileCoord, "no_access", _currentTileData.Value.NoAccess);
            }
            
            GD.Print($"Applied properties to {selectedTiles.Count} tiles");
        }

        private void RefreshUI()
        {
            if (_currentRenderer != null && _selectedTile.X >= 0)
            {
                var tileData = _currentRenderer.GetTileData(_selectedTile);
                if (tileData.HasValue)
                {
                    _currentTileData = tileData;
                    UpdateUIFromTileData();
                }
            }
            else
            {
                ClearSelection();
            }
        }
    }
}
#endif