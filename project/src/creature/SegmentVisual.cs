using Godot;

namespace NodeRunner.Creature;

public partial class SegmentVisual : Node2D
{
    private RigidBody2D? _jointA;
    private RigidBody2D? _jointB;
    private bool _isSelected;

    public Color Color { get; set; }

    public float Width { get; set; }

    public Color SelectionColor { get; set; }

    public float SelectionWidth { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            QueueRedraw();
        }
    }

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

        var start = ToLocal(_jointA.GlobalPosition);
        var end = ToLocal(_jointB.GlobalPosition);
        if (IsSelected)
        {
            DrawLine(start, end, SelectionColor, Width + SelectionWidth, antialiased: true);
        }

        DrawLine(start, end, Color, Width, antialiased: true);
    }
}
