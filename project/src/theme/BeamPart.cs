using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// A beam (#767) from <see cref="A"/> to <see cref="B"/>: a flat rod hidden under the joint rings it
/// stops on (#626), or centre to centre when the rings meet, and, while selected, a <c>halo</c>
/// line along each side. Under the rod it draws its <see cref="Placing"/> mark. The rod is drawn in
/// window pixels (<see cref="UiPixelPen"/>) like the joints, so its edges stay smooth at any zoom.
/// </summary>
public partial class BeamPart : PartVisual
{
    private const float _placingWidth = 2.2f;

    private Vector2 _a;
    private Vector2 _b;
    private float _radiusA;
    private float _radiusB;
    private bool _danger;
    private bool _haloA;
    private bool _haloB;
    private PlacingMark _placing;

    public BeamPart()
        : base(DrawSlot.Link)
    {
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

    /// <summary>Too short to train (#593): drawn in <c>danger</c> until its joints move apart.</summary>
    public bool Danger
    {
        get => _danger;
        set => Change(ref _danger, value);
    }

    /// <summary>Whether joint A is selected too, so the selection lines end on its halo ring (#710).</summary>
    public bool HaloA
    {
        get => _haloA;
        set => Change(ref _haloA, value);
    }

    /// <summary>Whether joint B is selected too.</summary>
    public bool HaloB
    {
        get => _haloB;
        set => Change(ref _haloB, value);
    }

    /// <summary>Whether it would take or refuse the sensor being placed or moved: a wide line under the rod.</summary>
    public PlacingMark Placing
    {
        get => _placing;
        set => Change(ref _placing, value);
    }

    public override void _Draw()
    {
        var (start, end) = JointDrawing.BeamSpan(Theme.JointRingWidth, A, RadiusA, B, RadiusB) ?? (A, B);
        using (var pen = UiPixelPen.Begin(this))
        {
            if (Placing == PlacingMark.Takes && start != end)
            {
                pen.Line(start, end, Theme.SelectionGlow, Theme.BeamWidth * _placingWidth);
            }
            else if (Placing == PlacingMark.Refuses && start != end)
            {
                pen.DashedLine(start, end, Theme.Danger, Theme.BeamWidth * _placingWidth, dash: Theme.BeamWidth * 2);
            }

            pen.Line(start, end, Danger ? Theme.Danger : Theme.Beam, Theme.BeamWidth);
        }

        if (Selected)
        {
            var (lineStart, lineEnd) = JoinHalos(start, end, Theme.SelectedBeamOffset);
            SelectionDrawing.DrawBeam(this, Transform2D.Identity, Theme.SelectionGlow, Theme.SelectedBeamOffset, Theme.SelectedBeamLineWidth, lineStart, lineEnd);
        }
    }

    /// <summary>
    /// The selection lines from <paramref name="start"/> to <paramref name="end"/>, each end moved onto
    /// its joint's halo ring when that joint is selected too, so a group reads as one outline (#710).
    /// </summary>
    private (Vector2 Start, Vector2 End) JoinHalos(Vector2 start, Vector2 end, float offset)
    {
        var joinedStart = HaloA ? SelectionDrawing.LineEnd(A, B, (float)SelectionMarks.JointHalo(RadiusA), offset) : start;
        var joinedEnd = HaloB ? SelectionDrawing.LineEnd(B, A, (float)SelectionMarks.JointHalo(RadiusB), offset) : end;
        return (joinedEnd - joinedStart).Dot(B - A) > 0 ? (joinedStart, joinedEnd) : (start, end);
    }
}
