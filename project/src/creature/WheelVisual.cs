using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// A Wheel's <see cref="WheelPart"/> in Training (#129): a child of its joint's physics body, so it
/// turns as the wheel rolls and its cue tubes show the turn.
/// </summary>
public partial class WheelVisual : WheelPart, IShadowVisual
{
    /// <summary>A shadow's Wheel is its tyre ring at its collision size, without cue or hub.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Simplified;

    public bool IsShadow
    {
        get => Simplified;
        set => Simplified = value;
    }
}
