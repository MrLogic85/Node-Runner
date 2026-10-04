using Godot;

namespace NodeRunner.Theme;

/// <summary>
/// A beam (#767) from <see cref="A"/> to <see cref="B"/>: a flat rod hidden under the joint rings it
/// stops on (#626) and, while selected, a <c>halo</c> line along each side.
/// </summary>
public partial class BeamPart : PartVisual
{
    // A Line2D keeps the rod's edges crisp and its ends flat; behind the part, so the selection
    // lines stay on top.
    private readonly Line2D _rod = new() { Antialiased = false, ShowBehindParent = true, Visible = false };
    private Vector2 _a;
    private Vector2 _b;
    private float _radiusA;
    private float _radiusB;

    public BeamPart()
        : base(CreatureLayers.Beams, CreatureLayers.SelectedLinks)
    {
        AddChild(_rod, @internal: InternalMode.Front);
    }

    public Vector2 A
    {
        get => _a;
        set => Change(ref _a, value);
    }

    public Vector2 B
    {
        get => _b;
        set => Change(ref _b, value);
    }

    /// <summary>The radius of the joint at <see cref="A"/>, where the drawn beam stops.</summary>
    public float RadiusA
    {
        get => _radiusA;
        set => Change(ref _radiusA, value);
    }

    /// <summary>The radius of the joint at <see cref="B"/>, where the drawn beam stops.</summary>
    public float RadiusB
    {
        get => _radiusB;
        set => Change(ref _radiusB, value);
    }

    public override void _Draw()
    {
        if (Selected && Span() is (var start, var end))
        {
            SelectionDrawing.DrawBeam(this, Transform2D.Identity, Theme.SelectionGlow, Theme.SelectedBeamOffset, Theme.SelectedBeamLineWidth, start, end);
        }
    }

    protected override void Changed()
    {
        base.Changed();
        var span = Span();
        _rod.Visible = span is not null;
        if (span is (var start, var end))
        {
            _rod.Points = [start, end];
            _rod.Width = Theme.BeamWidth;
            _rod.DefaultColor = Theme.Beam;
        }
    }

    private (Vector2 Start, Vector2 End)? Span() => JointDrawing.BeamSpan(Theme.JointRingWidth, A, RadiusA, B, RadiusB);
}
