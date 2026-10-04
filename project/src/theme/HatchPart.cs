using Godot;

namespace NodeRunner.Theme;

/// <summary>The rigid hatch (#612) of closed triangles (#767), under every other part.</summary>
public partial class HatchPart : PartVisual
{
    private Vector2[] _lines = [];

    public HatchPart()
        : base(CreatureLayers.Hatch, CreatureLayers.Hatch)
    {
    }

    /// <summary>The hatch lines as start and end pairs (<see cref="TriangleHatch"/>).</summary>
    public Vector2[] Lines
    {
        get => _lines;
        set => Change(ref _lines, value);
    }

    // Width -1 keeps the hatch one pixel wide at any zoom, so it stays faint (#400).
    public override void _Draw()
    {
        if (Lines.Length > 0)
        {
            DrawMultiline(Lines, Theme.RigidHatch, -1);
        }
    }
}
