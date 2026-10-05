namespace NodeRunner.Theme;

/// <summary>
/// The named draw layers of Training's world (#767), as ZIndex values. The world has its own
/// viewport, so these order only the world, never the screen or dialogs. Each creature takes
/// <see cref="CreatureLayers.Count"/> steps from its root's layer.
/// </summary>
public static class ArenaLayers
{
    /// <summary>The best distance marker, behind the ground and every creature.</summary>
    public const int BestMarker = -1;

    /// <summary>The start sign (#848), with the best marker behind the ground and every creature.</summary>
    public const int StartSign = BestMarker;

    /// <summary>The ground's fill and edge, and the ruler on it.</summary>
    public const int Ground = 0;

    /// <summary>The root of every shadow creature (#385): above the ground.</summary>
    public const int Shadows = Ground + 1;

    /// <summary>The root of the followed creature: every part of it above every part of every shadow.</summary>
    public const int Followed = Shadows + CreatureLayers.Count;
}
