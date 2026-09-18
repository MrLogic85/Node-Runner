using System.ComponentModel;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Creature;
using NodeRunner.Theme;

namespace NodeRunner;

public partial class Main : Node2D
{
    private readonly VisualTheme _theme = VisualTheme.Neon;
    private Creature.Creature? _creature;
    private CreatureInspectorViewModel? _inspector;
    private Label? _seedLabel;
    private Label? _inspectorTitle;
    private Label? _inspectorRole;
    private Label? _inspectorValues;

    public SelectionViewModel Selection { get; } = new();

    public override void _Ready()
    {
        Selection.PropertyChanged += OnSelectionPropertyChanged;
        AddBackdrop();
        AddGround();
        AddCamera();
        AddCreature();
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
            Position = new Vector2(360, 220),
            Zoom = new Vector2(1.15f, 1.15f),
            Enabled = true,
        });
    }

    private void AddCreature()
    {
        var scene = GD.Load<PackedScene>("res://scenes/Creature.tscn");
        var creature = scene.Instantiate<Creature.Creature>();
        creature.Name = "HardcodedWorm";
        creature.Definition = HardcodedWormFactory.Create();
        creature.Theme = _theme;
        creature.Position = new Vector2(250, 260);
        AddChild(creature);
        if (!creature.IsBuilt)
        {
            creature.BuildFrom(creature.Definition);
        }

        _creature = creature;
        _inspector = new CreatureInspectorViewModel(creature.Definition, Selection);
        _inspector.PropertyChanged += OnInspectorPropertyChanged;
    }

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
            CustomMinimumSize = new Vector2(300, 36),
        };

        var button = new Button
        {
            Name = "RandomizeButton",
            Text = "Randomize",
            CustomMinimumSize = new Vector2(120, 32),
        };
        button.AddThemeColorOverride("font_color", _theme.GroundEdge);
        button.Pressed += RandomizeCreatureBrain;

        _seedLabel = new Label
        {
            Name = "SeedLabel",
            Text = SeedText(),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _seedLabel.AddThemeColorOverride("font_color", _theme.Bone);

        row.AddChild(button);
        row.AddChild(_seedLabel);
        panel.AddChild(row);
        layer.AddChild(panel);
        AddChild(layer);
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

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!TryGetPressedPointerPosition(inputEvent, out var screenPosition))
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
        if (_inspector is not null)
        {
            _inspector.PropertyChanged -= OnInspectorPropertyChanged;
        }

        _inspector?.Dispose();
    }

    private static bool TryGetPressedPointerPosition(InputEvent inputEvent, out Vector2 screenPosition)
    {
        switch (inputEvent)
        {
            case InputEventScreenTouch { Pressed: true } touch:
                screenPosition = touch.Position;
                return true;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseButton:
                screenPosition = mouseButton.Position;
                return true;
            default:
                screenPosition = Vector2.Zero;
                return false;
        }
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
            AnchorTop = 0.64f,
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
        _inspectorTitle = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _inspectorTitle.AddThemeColorOverride("font_color", _theme.SelectionGlow);
        _inspectorRole = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _inspectorRole.AddThemeColorOverride("font_color", _theme.GroundEdge);
        _inspectorValues = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _inspectorValues.AddThemeColorOverride("font_color", _theme.Bone);
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
        if (_inspector is null || _inspectorTitle is null || _inspectorRole is null || _inspectorValues is null)
        {
            return;
        }

        _inspectorTitle.Text = _inspector.Title;
        _inspectorRole.Text = _inspector.Role;
        _inspectorValues.Text = _inspector.Values;
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
