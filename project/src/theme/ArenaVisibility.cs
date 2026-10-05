namespace NodeRunner.Theme;

/// <summary>
/// The Training arena's visibility layers (#818): the bits a viewport's <c>canvas_cull_mask</c>
/// picks. The shadows are drawn only by <c>ArenaShadows</c>' own viewport, so they fade together
/// as one picture; everything else only by the arena's. A viewport draws an item only if the item
/// and every canvas item above it share a bit with its mask, so a creature's root decides which,
/// and everything above and inside a creature, the world included, is on both.
/// </summary>
public static class ArenaVisibility
{
    /// <summary>Drawn by the arena's viewport: Godot's default layer.</summary>
    public const uint Arena = 1u << 0;

    /// <summary>Drawn by the shadows' viewport.</summary>
    public const uint Shadows = 1u << 1;

    /// <summary>Every item inside a creature or above one, so its root alone decides.</summary>
    public const uint Both = Arena | Shadows;
}
