using Godot;
using NodeRunner.Creature;

namespace NodeRunner;

public partial class Main : Node2D
{
    public override void _Ready()
    {
        AddGround();
        AddCamera();
        AddCreature();
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
            Color = new Color(0.35f, 0.62f, 0.38f),
            Polygon = new[]
            {
                new Vector2(-450, -24),
                new Vector2(450, -24),
                new Vector2(450, 24),
                new Vector2(-450, 24),
            },
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
        creature.Position = new Vector2(250, 260);
        AddChild(creature);
    }
}
