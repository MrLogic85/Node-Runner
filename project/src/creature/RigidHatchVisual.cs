using Godot;

namespace NodeRunner.Creature;

/// <summary>
/// The rigid hatch (#612) of one closed triangle on the followed creature in Training (#627), as
/// in Build. A rigid triangle cannot fold, so the hatch rides on one of its beams and never needs
/// to redraw while the creature moves.
/// </summary>
public partial class RigidHatchVisual : Node2D, IShadowVisual
{
    private bool _isShadow;

    /// <summary>A shadow draws no hatch: only its beams and joints (#385).</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Hidden;

    /// <summary>The hatch lines as start and end pairs, in the beam's space.</summary>
    public Vector2[] Lines { get; set; } = [];

    public Color Color { get; set; }

    public bool IsShadow
    {
        get => _isShadow;
        set
        {
            _isShadow = value;
            Visible = !value;
        }
    }

    // Width -1 keeps the hatch one pixel wide at any zoom, so it stays faint (#400).
    public override void _Draw()
    {
        if (Lines.Length > 0)
        {
            DrawMultiline(Lines, Color, -1);
        }
    }
}
