using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Theme;

/// <summary>A Spring from joint <see cref="A"/> to joint <see cref="B"/>, drawn by <see cref="SpringDrawing"/>.</summary>
public partial class SpringPart : PartVisual
{
    private Vector2 _a;
    private Vector2 _b;
    private float _radiusA;
    private float _radiusB;
    private float _coveredA = JointDrawing.HubRadius;
    private float _coveredB = JointDrawing.HubRadius;
    private float _shortest;
    private float _longest;
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

    /// <summary>How far what is drawn over joint A reaches (<see cref="ServoGeometry.LinkCover"/>), where its parts start.</summary>
    public float CoveredA
    {
        get => _coveredA;
        set => Change(ref _coveredA, value);
    }

    public float CoveredB
    {
        get => _coveredB;
        set => Change(ref _coveredB, value);
    }

    /// <summary>Its shortest length, centre to centre, which sets its telescope's sizes.</summary>
    public float Shortest
    {
        get => _shortest;
        set => Change(ref _shortest, value);
    }

    /// <summary>Its longest length, centre to centre: its telescope is fully out there.</summary>
    public float Longest
    {
        get => _longest;
        set => Change(ref _longest, value);
    }

    /// <summary>Its rest length, centre to centre, which sets its coil's turns.</summary>
    public float Rest
    {
        get => _rest;
        set => Change(ref _rest, value);
    }

    /// <summary>Its Stiffness in N/m, which sets its coil's wire.</summary>
    public double Stiffness
    {
        get => _stiffness;
        set => Change(ref _stiffness, value);
    }

    /// <summary>Too short to train: drawn in <c>danger</c>.</summary>
    public bool Danger
    {
        get => _danger;
        set => Change(ref _danger, value);
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

    public override void _Draw() =>
        SpringDrawing.Draw(this, Transform2D.Identity, Theme, A, B, RadiusA, RadiusB, CoveredA, CoveredB, Shortest, Longest, Rest, Stiffness, Danger, Selected, HaloA, HaloB);
}
