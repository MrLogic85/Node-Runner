using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// The <see cref="HatchPart"/> of one closed triangle in Training (#627): hatched on the followed
/// creature as in Build, filled on a shadow. A rigid triangle cannot fold, so the hatch rides on
/// one of its beams and never needs to redraw while the creature moves.
/// </summary>
public partial class RigidHatchVisual : HatchPart, IShadowVisual
{
    /// <summary>A shadow fills its rigid triangles instead of hatching them (#385, #770).</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Simplified;

    public bool IsShadow
    {
        get => Filled;
        set => Filled = value;
    }
}
