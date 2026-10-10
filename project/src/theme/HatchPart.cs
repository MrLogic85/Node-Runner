using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// The rigid hatch (#612) of closed triangles (#767), under every other part. Where the lines would
/// come too close on screen to read, or when <see cref="Filled"/>, it fills the triangles instead (#770).
/// </summary>
public partial class HatchPart : PartVisual
{
    private Vector2[] _lines = [];
    private Vector2[] _triangles = [];
    private bool _filled;

    public HatchPart()
        : base(CreatureLayers.Hatch)
    {
    }

    /// <summary>The hatch lines as start and end pairs (<see cref="TriangleHatch"/>).</summary>
    public Vector2[] Lines
    {
        get => _lines;
        set => Change(ref _lines, value);
    }

    /// <summary>The hatched triangles' corners, three per triangle, for the fill.</summary>
    public Vector2[] Triangles
    {
        get => _triangles;
        set => Change(ref _triangles, value);
    }

    /// <summary>Fills the triangles at any zoom, as a shadow in Training does.</summary>
    public bool Filled
    {
        get => _filled;
        set => Change(ref _filled, value);
    }

    // Width -1 keeps the hatch one pixel wide at any zoom, so it stays faint (#400). The fill's hard
    // edges lie under the triangle's beams.
    public override void _Draw()
    {
        using (var pen = UiPixelPen.Begin(this))
        {
            if (Filled || TriangleHatch.IsTooDense(Theme.RigidHatchSpacing, pen.Scale))
            {
                for (var corner = 0; corner + 2 < Triangles.Length; corner += 3)
                {
                    pen.Polygon(Triangles[corner..(corner + 3)], Theme.RigidFill);
                }

                return;
            }
        }

        if (Lines.Length > 0)
        {
            DrawMultiline(Lines, Theme.RigidHatch, -1);
        }
    }
}
