using Godot;

namespace NodeRunner.Creature;

/// <summary>
/// Draws a beam as a static rod along its own local X axis. Beams are rigid
/// bodies, so unlike the old muscle/bone springs this never needs to redraw
/// per-frame — it moves with its parent automatically.
/// </summary>
public partial class BeamVisual : Node2D
{
    private bool _isSelected;

    public float HalfLength { get; set; }

    public float Width { get; set; }

    public Color Color { get; set; }

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

    public override void _Draw()
    {
        var start = new Vector2(-HalfLength, 0);
        var end = new Vector2(HalfLength, 0);

        if (IsSelected)
        {
            DrawLine(start, end, SelectionColor, Width + SelectionWidth, antialiased: true);
        }

        DrawLine(start, end, Color, Width, antialiased: true);
    }
}
