using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// A beam's <see cref="BeamPart"/> in Training, along its body's local X axis. Beams are rigid
/// bodies, so it never needs to redraw while the creature moves: it moves with its parent.
/// </summary>
public partial class BeamVisual : BeamPart, IShadowVisual
{
    /// <summary>A shadow's beam is its line, without angle marks, hatch, labels or selection.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Simplified;

    /// <summary>Nothing to switch: <see cref="Creature"/> never selects a shadow's beam.</summary>
    public bool IsShadow { get; set; }
}
