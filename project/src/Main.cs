using System.ComponentModel;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Creature;
using NodeRunner.Domain;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner;

public partial class Main : Node2D
{
    private readonly VisualTheme _theme = VisualTheme.Neon;
    private Creature.Creature? _creature;
    private CreatureInspectorViewModel? _inspector;
    private ConstructionCanvas? _constructionCanvas;
    private Button? _buildModeButton;
    private PanelContainer? _toolPanel;
    private Button? _placeToolButton;
    private Button? _beamToolButton;
    private Button? _coreToolButton;
    private Button? _deleteToolButton;
    private Label? _seedLabel;
    private Label? _inspectorTitle;
    private Label? _inspectorRole;
    private Label? _inspectorValues;

    public SelectionViewModel Selection { get; } = new();

    public ConstructionViewModel Construction { get; } = new();

    public override void _Ready()
    {
        Selection.PropertyChanged += OnSelectionPropertyChanged;
        Construction.PropertyChanged += OnConstructionPropertyChanged;
        AddBackdrop();
        AddGround();
        AddCamera();
        AddCreature();
        AddConstructionCanvas();
        AddHud();
        AddInspector();
    }

    private void AddBackdrop()
    {
        AddChild(new ArenaBackdrop
        {
            Name = "ArenaBackdrop",
            Theme = _theme,
            ZIndex = -100,
        });
    }

    private void AddGround()
    {
        var ground = new StaticBody2D
        {
            Name = "Ground",
            Position = new Vector2(360, 420),
        };

        ground.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(900, 48) },
        });

        ground.AddChild(new Polygon2D
        {
            Color = _theme.GroundFill,
            Polygon = new[]
            {
                new Vector2(-450, -24),
                new Vector2(450, -24),
                new Vector2(450, 24),
                new Vector2(-450, 24),
            },
        });

        ground.AddChild(new Line2D
        {
            Points = new[]
            {
                new Vector2(-450, -24),
                new Vector2(450, -24),
            },
            DefaultColor = _theme.GroundEdge,
            Width = _theme.GroundEdgeWidth,
        });

        AddChild(ground);
    }

    private void AddCamera()
    {
        AddChild(new Camera2D
        {
            Name = "Camera",
            // Y is tuned so the ground/creature sit above the inspector
            // panel's reserved area (see AddInspector) instead of behind it.
            Position = new Vector2(360, 316),
            Zoom = new Vector2(1.15f, 1.15f),
            Enabled = true,
        });
    }

    private void AddCreature()
    {
        var scene = GD.Load<PackedScene>("res://scenes/Creature.tscn");
        var creature = scene.Instantiate<Creature.Creature>();
        creature.Name = "Creature";
        creature.Definition = HardcodedCreatureFactory.Create();
        creature.Theme = _theme;
        creature.Position = new Vector2(250, 260);
        AddChild(creature);
        if (!creature.IsBuilt)
        {
            creature.BuildFrom(creature.Definition);
        }

        _creature = creature;
        SetActiveInspector(creature.Definition);
    }

    // Swaps the inspector so it reads the currently active CreatureDef.
    // Needed both at startup and whenever construction mode replaces the
    // running creature (see ToggleConstructionMode / #72): the inspector
    // holds its CreatureDef by reference and won't pick up a new one on its
    // own.
    private void SetActiveInspector(CreatureDef definition)
    {
        if (_inspector is not null)
        {
            _inspector.PropertyChanged -= OnInspectorPropertyChanged;
            _inspector.Dispose();
        }

        _inspector = new CreatureInspectorViewModel(definition, Selection);
        _inspector.PropertyChanged += OnInspectorPropertyChanged;
    }

    private void AddConstructionCanvas()
    {
        var canvas = new ConstructionCanvas
        {
            Name = "ConstructionCanvas",
            Theme = _theme,
            ViewModel = Construction,
            Position = new Vector2(250, 260),
            Visible = false,
        };
        AddChild(canvas);
        _constructionCanvas = canvas;
    }

    // Base font size (Godot's default is 16px) and minimum touch target
    // height (Android's recommended ~48dp) for HUD/inspector controls. Sized
    // for the 720-tall design viewport (see [display] in project.godot).
    private const int _hudFontSize = 26;
    private const float _touchTargetHeight = 56f;

    private void AddHud()
    {
        var layer = new CanvasLayer
        {
            Name = "Hud",
        };

        var panel = new PanelContainer
        {
            Position = new Vector2(16, 16),
        };
        panel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle());

        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(600, _touchTargetHeight),
        };
        row.AddThemeConstantOverride("separation", 20);

        var button = new Button
        {
            Name = "RandomizeButton",
            Text = "Randomize",
            CustomMinimumSize = new Vector2(180, _touchTargetHeight),
        };
        button.AddThemeColorOverride("font_color", _theme.GroundEdge);
        button.AddThemeFontSizeOverride("font_size", _hudFontSize);
        button.Pressed += RandomizeCreatureBrain;

        _buildModeButton = new Button
        {
            Name = "BuildModeButton",
            Text = BuildModeButtonText(),
            CustomMinimumSize = new Vector2(180, _touchTargetHeight),
        };
        _buildModeButton.AddThemeColorOverride("font_color", _theme.SelectionGlow);
        _buildModeButton.AddThemeFontSizeOverride("font_size", _hudFontSize);
        _buildModeButton.Pressed += ToggleConstructionMode;

        _seedLabel = new Label
        {
            Name = "SeedLabel",
            Text = SeedText(),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _seedLabel.AddThemeColorOverride("font_color", _theme.Beam);
        _seedLabel.AddThemeFontSizeOverride("font_size", _hudFontSize);

        row.AddChild(button);
        row.AddChild(_buildModeButton);
        row.AddChild(_seedLabel);
        panel.AddChild(row);
        layer.AddChild(panel);
        AddChild(layer);

        AddConstructionToolRow(layer);
    }

    private void AddConstructionToolRow(CanvasLayer layer)
    {
        var panel = new PanelContainer
        {
            Position = new Vector2(16, 16 + _touchTargetHeight + 12),
            Visible = false,
        };
        panel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle());

        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(800, _touchTargetHeight),
        };
        row.AddThemeConstantOverride("separation", 20);

        _placeToolButton = CreateToolButton("PlaceToolButton", "Place");
        _placeToolButton.Pressed += () => Construction.ActiveTool = ConstructionTool.Place;

        _beamToolButton = CreateToolButton("BeamToolButton", "Beam");
        _beamToolButton.Pressed += () => Construction.ActiveTool = ConstructionTool.Beam;

        _coreToolButton = CreateToolButton("CoreToolButton", "Core");
        _coreToolButton.Pressed += () => Construction.ActiveTool = ConstructionTool.Core;

        _deleteToolButton = CreateToolButton("DeleteToolButton", "Delete");
        _deleteToolButton.Pressed += () => Construction.ActiveTool = ConstructionTool.Delete;

        row.AddChild(_placeToolButton);
        row.AddChild(_beamToolButton);
        row.AddChild(_coreToolButton);
        row.AddChild(_deleteToolButton);
        panel.AddChild(row);
        layer.AddChild(panel);

        _toolPanel = panel;
        UpdateToolButtonHighlight();
    }

    private Button CreateToolButton(string name, string text)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            CustomMinimumSize = new Vector2(180, _touchTargetHeight),
        };
        button.AddThemeFontSizeOverride("font_size", _hudFontSize);
        return button;
    }

    private void RandomizeCreatureBrain()
    {
        if (_creature is null)
        {
            return;
        }

        var seed = Random.Shared.Next(int.MinValue, int.MaxValue);
        _creature.RandomizeBrain(seed);
        if (_seedLabel is not null)
        {
            _seedLabel.Text = SeedText();
        }
    }

    // 0.3.0 construction mode: toggling swaps the running creature for an
    // editable node canvas. Leaving Build mode with a non-empty, valid
    // anatomy replaces the running creature with the edited one (#72); an
    // empty anatomy leaves the current creature untouched, which is how the
    // original hardcoded worm keeps working for a user who never edits
    // anything (see docs/CONSTRUCTION_MODE.md).
    private void ToggleConstructionMode()
    {
        if (Construction.IsActive)
        {
            if (!Construction.TryLeave(out var editedCreature, out var errors))
            {
                Construction.SetBlockedLeaveMessage(errors);
                return;
            }

            if (editedCreature is not null)
            {
                ApplyEditedCreature(editedCreature);
            }
        }

        Construction.IsActive = !Construction.IsActive;
    }

    private void ApplyEditedCreature(CreatureDef editedCreature)
    {
        if (_creature is null)
        {
            return;
        }

        // Clear any selection from the old creature before rebuilding the
        // inspector: CreatureInspectorViewModel reads Selection.SelectedElement
        // as soon as it's constructed, and a stale index into the old
        // definition could point past the end of (or at a different element
        // in) the newly edited one.
        Selection.Clear();
        _creature.BuildFrom(editedCreature);
        SetActiveInspector(editedCreature);
        if (_seedLabel is not null)
        {
            _seedLabel.Text = SeedText();
        }
    }


    private void OnConstructionPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        switch (eventArgs.PropertyName)
        {
            case nameof(ConstructionViewModel.IsActive):
                if (_creature is not null)
                {
                    _creature.Visible = !Construction.IsActive;
                }

                if (_constructionCanvas is not null)
                {
                    _constructionCanvas.Visible = Construction.IsActive;
                }

                if (_toolPanel is not null)
                {
                    _toolPanel.Visible = Construction.IsActive;
                }

                if (_buildModeButton is not null)
                {
                    _buildModeButton.Text = BuildModeButtonText();
                }

                UpdateInspector();
                break;
            case nameof(ConstructionViewModel.ActiveTool):
                UpdateToolButtonHighlight();
                UpdateInspector();
                break;
            case nameof(ConstructionViewModel.StatusMessage):
                UpdateInspector();
                break;
        }
    }

    private void UpdateToolButtonHighlight()
    {
        if (_placeToolButton is null || _beamToolButton is null || _coreToolButton is null || _deleteToolButton is null)
        {
            return;
        }

        HighlightToolButton(_placeToolButton, Construction.ActiveTool == ConstructionTool.Place);
        HighlightToolButton(_beamToolButton, Construction.ActiveTool == ConstructionTool.Beam);
        HighlightToolButton(_coreToolButton, Construction.ActiveTool == ConstructionTool.Core);
        HighlightToolButton(_deleteToolButton, Construction.ActiveTool == ConstructionTool.Delete);
    }

    private void HighlightToolButton(Button button, bool isActive)
    {
        button.AddThemeColorOverride("font_color", isActive ? _theme.SelectionGlow : _theme.GroundEdge);
    }

    private string BuildModeButtonText()
    {
        return Construction.IsActive ? "Simulate" : "Build";
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (Construction.IsActive)
        {
            return;
        }

        if (!PointerInput.TryGetPressPosition(inputEvent, out var screenPosition))
        {
            return;
        }

        var worldPosition = GetGlobalTransformWithCanvas().AffineInverse() * screenPosition;
        if (_creature is not null && _creature.TrySelectPart(worldPosition, out var selection) && selection is not null)
        {
            Selection.Select(selection);
        }
        else
        {
            Selection.Clear();
        }

        GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        Selection.PropertyChanged -= OnSelectionPropertyChanged;
        Construction.PropertyChanged -= OnConstructionPropertyChanged;
        if (_inspector is not null)
        {
            _inspector.PropertyChanged -= OnInspectorPropertyChanged;
        }

        _inspector?.Dispose();
    }

    private void OnSelectionPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(SelectionViewModel.SelectedElement))
        {
            _creature?.SetSelectedElement(Selection.SelectedElement);
        }
    }

    private void OnInspectorPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        UpdateInspector();
    }

    private void AddInspector()
    {
        var layer = new CanvasLayer
        {
            Name = "Inspector",
        };

        var panel = new PanelContainer
        {
            AnchorsPreset = (int)Control.LayoutPreset.BottomWide,
            // Reserve less of the screen than before (was 0.64, which,
            // combined with the camera framing, covered the creature's
            // resting position). Paired with the camera Y in AddCamera().
            AnchorTop = 0.78f,
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Begin,
        };
        panel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 14);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 6);
        _inspectorTitle = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _inspectorTitle.AddThemeColorOverride("font_color", _theme.SelectionGlow);
        _inspectorTitle.AddThemeFontSizeOverride("font_size", _hudFontSize);
        _inspectorRole = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _inspectorRole.AddThemeColorOverride("font_color", _theme.GroundEdge);
        _inspectorRole.AddThemeFontSizeOverride("font_size", _hudFontSize);
        _inspectorValues = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _inspectorValues.AddThemeColorOverride("font_color", _theme.Beam);
        _inspectorValues.AddThemeFontSizeOverride("font_size", _hudFontSize);
        content.AddChild(_inspectorTitle);
        content.AddChild(_inspectorRole);
        content.AddChild(_inspectorValues);
        margin.AddChild(content);
        panel.AddChild(margin);
        layer.AddChild(panel);
        AddChild(layer);
        UpdateInspector();
    }

    private void UpdateInspector()
    {
        if (_inspectorTitle is null || _inspectorRole is null || _inspectorValues is null)
        {
            return;
        }

        if (Construction.IsActive)
        {
            _inspectorTitle.Text = "Building";
            _inspectorRole.Text = $"Tool: {Construction.ActiveTool}";
            _inspectorValues.Text = Construction.StatusMessage ?? ConstructionToolHint(Construction.ActiveTool);
            return;
        }

        if (_inspector is null)
        {
            return;
        }

        _inspectorTitle.Text = _inspector.Title;
        _inspectorRole.Text = _inspector.Role;
        _inspectorValues.Text = _inspector.Values;
    }

    private static string ConstructionToolHint(ConstructionTool tool)
    {
        return tool switch
        {
            ConstructionTool.Place => "Tap empty space to place a node. Drag a node to move it.",
            ConstructionTool.Beam => "Tap a node, then another node, to connect them with a beam.",
            ConstructionTool.Core => "Tap a node to attach a core, tap again to remove it.",
            ConstructionTool.Delete => "Tap a node or beam to delete it.",
            _ => string.Empty,
        };
    }

    private string SeedText()
    {
        return _creature is null ? "Seed: -" : $"Seed: {_creature.BrainSeed}";
    }

    private StyleBoxFlat CreateHudPanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = _theme.ArenaBackground with { A = 0.82f },
            BorderColor = _theme.GroundEdge,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 10,
            ContentMarginTop = 8,
            ContentMarginRight = 10,
            ContentMarginBottom = 8,
        };
    }
}
