using Godot;
using NodeRunner.Creature;
using NodeRunner.Theme;

namespace NodeRunner;

public partial class Main : Node2D
{
    private readonly VisualTheme _theme = VisualTheme.Neon;

    public override void _Ready()
    {
        AddBackdrop();
        AddGround();
        AddCamera();
        AddCreature();
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
    }
}
