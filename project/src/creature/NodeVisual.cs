using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// Draws a node as its ring (<see cref="JointDrawing"/>): rendering only, a child of the node's own physics body.
/// </summary>
public partial class NodeVisual : Node2D, IShadowVisual
{
    private bool _isSelected;
    private bool _isShadow;
    private bool _showsInnerRing;

    /// <summary>A shadow's node is its outer ring at its collision size, without inner ring, tint or selection.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Simplified;

    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    public float Radius { get; set; }

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

    // The camera zooms without redrawing the joint, so it redraws itself when its inner ring
    // should come or go.
    public override void _Process(double delta)
    {
        if (!IsShadow && JointDrawing.ShowsInnerRing(this, Transform2D.Identity, Radius) != _showsInnerRing)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        _showsInnerRing = JointDrawing.ShowsInnerRing(this, Transform2D.Identity, Radius);
        var look = IsSelected && !IsShadow ? JointLook.Selected : JointLook.Plain;
        JointDrawing.DrawPlain(this, Theme, Transform2D.Identity, Vector2.Zero, Radius, look, simplified: IsShadow);
        if (IsSelected && !IsShadow)
        {
            SelectionDrawing.DrawJoint(this, Theme, Transform2D.Identity, Vector2.Zero, Radius * 1.65f);
        }
    }
}
