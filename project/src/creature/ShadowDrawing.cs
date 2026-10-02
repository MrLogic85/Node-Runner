namespace NodeRunner.Creature;

/// <summary>How a creature visual draws on a shadow other than the followed one (#385).</summary>
public enum ShadowDrawing
{
    /// <summary>Not drawn at all.</summary>
    Hidden,

    /// <summary>The same shape and colours without detail.</summary>
    Simplified,

    /// <summary>Drawn as on the followed creature.</summary>
    Same,
}

/// <summary>
/// Every creature visual declares how it draws as a shadow (docs/CREATURE_MODEL.md → "Drawing as a
/// shadow"), so a new part cannot forget it; a test checks every visual kind implements this.
/// </summary>
public interface IShadowVisual
{
    static abstract ShadowDrawing AsShadow { get; }

    /// <summary>True while the creature is a shadow that is not followed.</summary>
    bool IsShadow { get; set; }
}
