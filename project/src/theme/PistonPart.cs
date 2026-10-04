using Godot;

namespace NodeRunner.Theme;

/// <summary>A Piston (#767) from joint <see cref="A"/> to joint <see cref="B"/>, drawn by <see cref="PistonDrawing"/>.</summary>
public partial class PistonPart : PartVisual
{
    private Vector2 _a;
    private Vector2 _b;
    private float _radiusA;
    private float _radiusB;
    private float _shortest;
    private float _longest;
    private bool _danger;
    private bool _showStroke = true;
    private bool _haloA;
    private bool _haloB;

    public PistonPart()
        : base(CreatureLayers.Pistons, CreatureLayers.SelectedLinks)
    {
    }

    /// <summary>Joint A's centre, where the cylinder sits.</summary>
    public Vector2 A
    {
        get => _a;
        set => Change(ref _a, value);
    }

    /// <summary>Joint B's centre, where the rod ends in its cap.</summary>
    public Vector2 B
    {
        get => _b;
        set => Change(ref _b, value);
    }

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

    /// <summary>The shortest length, centre to centre.</summary>
    public float Shortest
    {
        get => _shortest;
        set => Change(ref _shortest, value);
    }

    /// <summary>The longest length, centre to centre.</summary>
    public float Longest
    {
        get => _longest;
        set => Change(ref _longest, value);
    }

    /// <summary>Too short to train (#593): drawn in <c>danger</c> instead of <c>accent</c>.</summary>
    public bool Danger
    {
        get => _danger;
        set => Change(ref _danger, value);
    }

    /// <summary>Whether, selected, it also shows its stroke ticks: only while its Stroke can be set (#704).</summary>
    public bool ShowStroke
    {
        get => _showStroke;
        set => Change(ref _showStroke, value);
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

    public override void _Draw() =>
        PistonDrawing.Draw(this, Transform2D.Identity, Theme, A, B, RadiusA, RadiusB, Shortest, Longest, Danger ? Theme.Danger : Theme.MotorAccent, Selected, ShowStroke, HaloA, HaloB);
}
