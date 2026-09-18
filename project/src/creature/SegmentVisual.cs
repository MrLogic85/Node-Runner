using Godot;

namespace NodeRunner.Creature;

public partial class SegmentVisual : Node2D
{
    private RigidBody2D? _jointA;
    private RigidBody2D? _jointB;

    public Color Color { get; set; }

    public float Width { get; set; }

    public void Connect(RigidBody2D jointA, RigidBody2D jointB)
    {
        _jointA = jointA;
        _jointB = jointB;
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_jointA is null || _jointB is null)
        {
            return;
        }

        DrawLine(ToLocal(_jointA.GlobalPosition), ToLocal(_jointB.GlobalPosition), Color, Width, antialiased: true);
    }
}
