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

    public float Width { get; set; }

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
        // Round ends, since a beam's ends show inside an open joint ring (#626). Behind the parent so
        // the selection lines drawn below stay on top.
        AddChild(new Line2D
        {
            Points = [new Vector2(-HalfLength, 0), new Vector2(HalfLength, 0)],
            Width = Width,
            DefaultColor = Color,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
            Antialiased = false,
            ShowBehindParent = true,
        });
    }

    public override void _Draw()
    {
        var start = new Vector2(-HalfLength, 0);
        var end = new Vector2(HalfLength, 0);

        if (IsSelected && !IsShadow)
        {
            SelectionDrawing.DrawBeam(this, Transform2D.Identity, SelectionColor, SelectionOffset, SelectionLineWidth, start, end);
        }
    }
}
