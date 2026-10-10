using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Theme;

/// <summary>A Spring (#453) from joint <see cref="A"/> to joint <see cref="B"/>, drawn by <see cref="SpringDrawing"/>.</summary>
public partial class SpringPart : PartVisual
{
    private Vector2 _a;
    private Vector2 _b;
    private float _radiusA;
    private float _radiusB;
    private float _travel;
    private float _rest;
    private double _stiffness = SpringDef.DefaultStiffness;
    private bool _danger;
    private bool _haloA;
    private bool _haloB;

    public SpringPart()
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

    /// <summary>Its travel, longest length less shortest, which sets its damper body's length (#835).</summary>
    public float Travel
    {
        get => _travel;
        set => Change(ref _travel, value);
    }

    /// <summary>Its rest length, centre to centre, which sets its coil's turns (#835).</summary>
    public float Rest
    {
        get => _rest;
        set => Change(ref _rest, value);
    }

    /// <summary>Its Stiffness in N/m, which sets its coil's wire (#835).</summary>
    public double Stiffness
    {
        get => _stiffness;
        set => Change(ref _stiffness, value);
    }

    /// <summary>Too short to train (#593): drawn in <c>danger</c>.</summary>
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
        SpringDrawing.Draw(this, Transform2D.Identity, Theme, A, B, RadiusA, RadiusB, Travel, Rest, Stiffness, Danger, Selected, HaloA, HaloB);
}
