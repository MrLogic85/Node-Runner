using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// The <see cref="HatchPart"/> of one closed triangle on the followed creature in Training (#627),
/// as in Build. A rigid triangle cannot fold, so the hatch rides on one of its beams and never
/// needs to redraw while the creature moves.
/// </summary>
public partial class RigidHatchVisual : HatchPart, IShadowVisual
{
    /// <summary>A shadow draws no hatch: only its beams and joints (#385).</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Hidden;

    public bool IsShadow
    {
        get => !Visible;
        set => Visible = !value;
    }
}
