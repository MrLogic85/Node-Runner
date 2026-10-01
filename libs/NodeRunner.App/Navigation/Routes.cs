namespace NodeRunner.App.Navigation;

/// <summary>Home: the list of the player's creations, and the root of the back stack.</summary>
public sealed record CreationsRoute : SceneRoute;

/// <summary>The theme and debug bounds a component-library page opens with, handed from page to page.</summary>
public interface IGalleryRoute
{
    /// <summary>Selected theme-switch segment: 0 Neon, 1 Paper.</summary>
    int ThemeIndex { get; }

    bool ShowDebugBounds { get; }
}

public sealed record ComponentGalleryRoute(int ThemeIndex = 0, bool ShowDebugBounds = false) : SceneRoute, IGalleryRoute;

public sealed record ToolbarsRoute(int ThemeIndex = 0, bool ShowDebugBounds = false) : SceneRoute, IGalleryRoute;

public sealed record ColorsAndStylesRoute(int ThemeIndex = 0, bool ShowDebugBounds = false) : SceneRoute, IGalleryRoute;

public sealed record PopupGalleryRoute(int ThemeIndex = 0, bool ShowDebugBounds = false) : SceneRoute, IGalleryRoute;

/// <summary>Ready-made creations the player copies into Creations.</summary>
public sealed record ExamplesRoute : SceneRoute;

/// <summary>
/// Build for one saved creation. <see cref="IsNew"/> is true when + New just made it; see
/// docs/BUILD_MODE.md for when Build removes such a creation again (#368).
/// </summary>
public sealed record BuildRoute(Guid CreationId, bool IsNew = false) : SceneRoute;

/// <summary>
/// Training for one saved creation. It resumes from the creation's last finished generation; leaving
/// drops only the generation in progress.
/// </summary>
public sealed record TrainingRoute(Guid CreationId) : SceneRoute;
