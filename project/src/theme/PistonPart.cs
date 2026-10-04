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

    public override void _Draw() =>
        PistonDrawing.Draw(this, Transform2D.Identity, Theme, A, B, RadiusA, RadiusB, Shortest, Longest, Theme.MotorAccent, Selected);
}
