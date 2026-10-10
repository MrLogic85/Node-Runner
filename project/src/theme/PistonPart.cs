using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Theme;

/// <summary>A Piston (#767) from joint <see cref="A"/> to joint <see cref="B"/>, drawn by <see cref="PistonDrawing"/>.</summary>
public partial class PistonPart : PartVisual
{
    private Vector2 _a;
    private Vector2 _b;
    private float _radiusA;
    private float _radiusB;
    private float _travel;
    private bool _danger;
    private bool _haloA;
    private bool _haloB;

    public PistonPart()
        : base(DrawSlot.Link)
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

    /// <summary>Its travel, longest length less shortest, which sets its cylinder's length.</summary>
    public float Travel
    {
        get => _travel;
        set => Change(ref _travel, value);
    }

    /// <summary>Too short to train (#593): drawn in <c>danger</c> instead of <c>accent</c>.</summary>
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

    public override void _Draw() =>
        PistonDrawing.Draw(this, Transform2D.Identity, Theme, A, B, RadiusA, RadiusB, Travel, Danger ? Theme.Danger : Theme.MotorAccent, Selected, HaloA, HaloB);
}
