using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Theme;

/// <summary>A joint (#767): its ring (<see cref="JointDrawing"/>) at its origin and, while selected, its halo.</summary>
public partial class JointPart : PartVisual
{
    private float _radius;
    private bool _simplified;

    public JointPart()
        : base(CreatureLayers.Joints, CreatureLayers.SelectedJoints)
    {
    }

    public float Radius
    {
        get => _radius;
        set => Change(ref _radius, value);
    }

    /// <summary>The outer ring only, without inner ring or tint.</summary>
    public bool Simplified
    {
        get => _simplified;
        set => Change(ref _simplified, value);
    }

    public override void _Draw()
    {
        JointDrawing.DrawPlain(this, Theme, Transform2D.Identity, Vector2.Zero, Radius, Selected ? JointLook.Selected : JointLook.Plain, Simplified);
        if (Selected)
        {
            SelectionDrawing.DrawJoint(this, Theme, Transform2D.Identity, Vector2.Zero, (float)SelectionMarks.JointHalo(Radius));
        }
    }
}
