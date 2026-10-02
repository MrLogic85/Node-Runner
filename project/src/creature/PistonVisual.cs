using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// A Piston in Training (#451), redrawn every frame from its two node bodies, so the rod slides
/// in and out of its cylinder live. It draws over the beams and under the joints.
/// </summary>
public partial class PistonVisual : Node2D, IShadowVisual
{
    private bool _isSelected;

    /// <summary>Links draw on a shadow as on the followed creature (docs/CREATURE_MODEL.md).</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Same;

    public required VisualTheme Theme { get; init; }

    public required PistonLink Link { get; init; }

    public required float RadiusA { get; init; }

    public required float RadiusB { get; init; }

    public bool IsShadow { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            QueueRedraw();
        }
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var built = Link.BuiltLength;
        var stroke = Link.Definition.Stroke;
        PistonDrawing.Draw(
            this,
            Transform2D.Identity,
            Theme,
            ToLocal(Link.NodeA.GlobalPosition),
            ToLocal(Link.NodeB.GlobalPosition),
            RadiusA,
            RadiusB,
            (float)Domain.Piston.ShortestLength(built, stroke),
            (float)Domain.Piston.LongestLength(built, stroke),
            Theme.MotorAccent,
            IsSelected && !IsShadow);
    }
}
