using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// Draws a beam as a static rod along its own local X axis. Beams are rigid
/// bodies, so unlike the old muscle/bone springs this never needs to redraw
/// per-frame — it moves with its parent automatically.
/// </summary>
public partial class BeamVisual : Node2D, IShadowVisual
{
    private bool _isSelected;
    private bool _isShadow;

    /// <summary>A shadow's beam is its line, without angle marks, hatch, labels or selection.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Simplified;

    public float HalfLength { get; set; }

    /// <summary>The radius of the joint at the start, -<see cref="HalfLength"/>, where the drawn beam stops.</summary>
    public float RadiusA { get; set; }

    /// <summary>The radius of the joint at the end, <see cref="HalfLength"/>, where the drawn beam stops.</summary>
    public float RadiusB { get; set; }

    public float Width { get; set; }

    /// <summary>The width of the joint rings the beam stops under.</summary>
    public float RingWidth { get; set; }

    public Color Color { get; set; }

    public Color SelectionColor { get; set; }

    /// <summary>How far each selection line runs from the beam's centre line.</summary>
    public float SelectionOffset { get; set; }

    public float SelectionLineWidth { get; set; }

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

    public bool IsShadow
    {
        get => _isShadow;
        set
        {
            if (_isShadow == value)
            {
                return;
            }

            _isShadow = value;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        if (Span() is not (var start, var end))
        {
            return;
        }

        // Flat ends, hidden under the joint rings it stops on (#626). Behind the parent so the
        // selection lines drawn below stay on top.
        AddChild(new Line2D
        {
            Points = [start, end],
            Width = Width,
            DefaultColor = Color,
            Antialiased = false,
            ShowBehindParent = true,
        });
    }

    public override void _Draw()
    {
        if (IsSelected && !IsShadow && Span() is (var start, var end))
        {
            SelectionDrawing.DrawBeam(this, Transform2D.Identity, SelectionColor, SelectionOffset, SelectionLineWidth, start, end);
        }
    }

    private (Vector2 Start, Vector2 End)? Span() =>
        JointDrawing.BeamSpan(RingWidth, new Vector2(-HalfLength, 0), RadiusA, new Vector2(HalfLength, 0), RadiusB);
}
