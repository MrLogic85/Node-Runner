using Godot;
using NodeRunner.Creature;
using NodeRunner.Theme;

namespace NodeRunner;

public partial class Main : Node2D
{
    private readonly VisualTheme _theme = VisualTheme.Neon;
    private Creature.Creature? _creature;
    private Label? _seedLabel;

    public override void _Ready()
    {
        AddBackdrop();
        AddGround();
        AddCamera();
        AddCreature();
        AddHud();
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
