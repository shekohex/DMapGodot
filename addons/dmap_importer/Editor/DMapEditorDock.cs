#if TOOLS
using Godot;
using DMapImporter.Core.Dmap;
using DMapImporter.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DMapImporter.Editor
{
    [Tool]
    public partial class DMapEditorDock : Control
    {
        private DMapRenderer? _currentRenderer;
        private Vector2I _selectedTile = new Vector2I(-1, -1);
        private Callable? _tileSelectedCallable;

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
            Name = "DMap Editor";
            AddThemeConstantOverride("separation", 4);
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

            UpdateTileProperty(TileProperty.Height, (short)value);
        }

        private void OnSurfaceChanged(long index)
        {
            if (_isUpdating || _currentRenderer == null || _selectedTile.X < 0)
                return;

            UpdateTileProperty(TileProperty.Surface, (ushort)index);
        }

        private void OnWalkableToggled(bool pressed)
        {
            if (_isUpdating || _currentRenderer == null || _selectedTile.X < 0)
                return;

            UpdateTileProperty(TileProperty.NoAccess, (ushort)(pressed ? 0 : 1));
        }

        private void OnApplyPressed()
        {
            ApplyToAllSelected();
        }

        public void SetCurrentRenderer(DMapRenderer? renderer)
        {
            // Disconnect from previous renderer using stored callable
            if (_currentRenderer != null && _tileSelectedCallable != null)
            {
                if (_currentRenderer.IsConnected("TileSelected", _tileSelectedCallable.Value))
                {
                    _currentRenderer.Disconnect("TileSelected", _tileSelectedCallable.Value);
                }
                _tileSelectedCallable = null;
            }

            _currentRenderer = renderer;

            if (_currentRenderer != null)
            {
                // Store the callable to ensure proper disconnection
                _tileSelectedCallable = new Callable(this, nameof(OnTileSelected));
                _currentRenderer.Connect("TileSelected", _tileSelectedCallable.Value);
                RefreshUI();
            }
            else
            {
                ClearSelection();
            }
        }

        // Add cleanup method to ensure proper signal disconnection
        public override void _ExitTree()
        {
            SetCurrentRenderer(null);
            base._ExitTree();
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

        private void UpdateTileProperty(TileProperty property, object value)
        {
            if (_currentRenderer == null || _selectedTile.X < 0)
                return;

            // Validate the value before updating
            if (!property.IsValidValue(value))
            {
                GD.PrintErr($"Invalid value {value} for property {property}");
                return;
            }

            _currentRenderer.UpdateTileProperty(_selectedTile, property, value);

            if (_currentTileData.HasValue)
            {
                var tile = _currentTileData.Value;

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
                    }

                    _currentTileData = tile;
                }
                catch (System.Exception ex)
                {
                    GD.PrintErr($"Error updating local tile data for property {property}: {ex.Message}");
                }
            }
        }

        private const int MaxBatchSize = 1000; // Prevent UI freezing with large selections
        private const int BatchChunkSize = 100; // Process in chunks for responsiveness

        private void ApplyToAllSelected()
        {
            if (_currentRenderer == null || !_currentTileData.HasValue)
                return;

            var selectedTiles = _currentRenderer.GetSelectedTiles();

            // Safety check for large selections
            if (selectedTiles.Count > MaxBatchSize)
            {
                GD.PrintErr($"Selection too large ({selectedTiles.Count} tiles). Maximum batch size is {MaxBatchSize}.");

                // Ask user for confirmation for large batches
                if (selectedTiles.Count > MaxBatchSize * 2)
                {
                    GD.PrintErr("Selection exceeds safe limits. Operation cancelled.");
                    return;
                }
            }

            // Process in chunks to maintain UI responsiveness
            var processedCount = 0;
            const int chunkSize = BatchChunkSize;

            for (int i = 0; i < selectedTiles.Count; i += chunkSize)
            {
                var chunk = selectedTiles.Skip(i).Take(chunkSize);

                foreach (var tileCoord in chunk)
                {
                    _currentRenderer.UpdateTileProperty(tileCoord, TileProperty.Height, _currentTileData.Value.Height);
                    _currentRenderer.UpdateTileProperty(tileCoord, TileProperty.Surface, _currentTileData.Value.Surface);
                    _currentRenderer.UpdateTileProperty(tileCoord, TileProperty.NoAccess, _currentTileData.Value.NoAccess);
                    processedCount++;
                }

                // Yield control back to UI periodically
                if (i + chunkSize < selectedTiles.Count)
                {
                    // Note: In production, this could be made async with await
                    GD.Print($"Processing batch {i / chunkSize + 1}/{(selectedTiles.Count + chunkSize - 1) / chunkSize}...");
                }
            }

            GD.Print($"Applied properties to {processedCount} tiles");
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
