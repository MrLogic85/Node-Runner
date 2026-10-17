using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// A beam from <see cref="A"/> to <see cref="B"/>: a flat rod from hub to hub, its
/// ends hidden under the joint rings, or centre to centre when the hubs meet, and, while selected, a <c>halo</c>
/// line along each side. Under the rod it draws its <see cref="Placing"/> mark. The rod is drawn in
/// window pixels (<see cref="UiPixelPen"/>) like the joints, so its edges stay smooth at any zoom.
/// </summary>
public partial class BeamPart : PartVisual
{
    private const float _placingWidth = 2.2f;

    /// <summary>How much of a torn end flashes <c>danger</c>, in creature units.</summary>
    public const float TornEndLength = 8f;

    private Vector2 _a;
    private Vector2 _b;
    private float _radiusA;
    private float _radiusB;
    private bool _danger;
    private bool _tornA;
    private bool _tornB;
    private bool _flashA;
    private bool _flashB;
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

    /// <summary>The drawn radius of the joint at <see cref="A"/>, whose halo the selection lines join.</summary>
    public float RadiusA
    {
        get => _radiusA;
        set => Change(ref _radiusA, value);
    }

    public float RadiusB
    {
        get => _radiusB;
        set => Change(ref _radiusB, value);
    }

    /// <summary>Too short to train: drawn in <c>danger</c> until its joints move apart.</summary>
    public bool Danger
    {
        get => _danger;
        set => Change(ref _danger, value);
    }

    /// <summary>
    /// Whether end A has torn from its joint in a run: with no joint ring over it, the rod runs
    /// out to <see cref="A"/>.
    /// </summary>
    public bool TornA
    {
        get => _tornA;
        set => Change(ref _tornA, value);
    }

    public bool TornB
    {
        get => _tornB;
        set => Change(ref _tornB, value);
    }

    /// <summary>Whether torn end A is flashing: its last <see cref="TornEndLength"/> units are drawn in <c>danger</c>.</summary>
    public bool FlashA
    {
        get => _flashA;
        set => Change(ref _flashA, value);
    }

    public bool FlashB
    {
        get => _flashB;
        set => Change(ref _flashB, value);
    }

    /// <summary>Whether joint A is selected too, so the selection lines end on its halo ring.</summary>
    public bool HaloA
    {
        get => _haloA;
        set => Change(ref _haloA, value);
    }

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
        var (start, end) = JointDrawing.BeamSpan(Theme.JointRingWidth, A, JointDrawing.HubRadius, B, JointDrawing.HubRadius) ?? (A, B);
        start = TornA ? A : start;
        end = TornB ? B : end;
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
            var tornLength = Math.Min(TornEndLength, start.DistanceTo(end));
            if (FlashA && tornLength > 0)
            {
                pen.Line(start, start + (start.DirectionTo(end) * tornLength), Theme.Danger, Theme.BeamWidth);
            }

            if (FlashB && tornLength > 0)
            {
                pen.Line(end, end + (end.DirectionTo(start) * tornLength), Theme.Danger, Theme.BeamWidth);
            }
        }

        if (Selected)
        {
            var (lineStart, lineEnd) = JoinHalos(start, end, Theme.SelectedBeamOffset);
            SelectionDrawing.DrawBeam(this, Transform2D.Identity, Theme.SelectionGlow, Theme.SelectedBeamOffset, Theme.SelectedBeamLineWidth, lineStart, lineEnd);
        }
    }

    // The selection lines from start to end, each end moved onto its joint's halo ring when that
    // joint is selected too, so a group reads as one outline.
    private (Vector2 Start, Vector2 End) JoinHalos(Vector2 start, Vector2 end, float offset)
    {
        var joinedStart = HaloA ? SelectionDrawing.LineEnd(A, B, (float)SelectionMarks.JointHalo(RadiusA), offset) : start;
        var joinedEnd = HaloB ? SelectionDrawing.LineEnd(B, A, (float)SelectionMarks.JointHalo(RadiusB), offset) : end;
        return (joinedEnd - joinedStart).Dot(B - A) > 0 ? (joinedStart, joinedEnd) : (start, end);
    }
}
