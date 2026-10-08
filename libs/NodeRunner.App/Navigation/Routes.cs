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

/// <summary>Adds a creation someone shared as a code (#899) to Creations.</summary>
public sealed record ImportRoute : SceneRoute;

/// <summary>
/// Build for one saved creation. <see cref="IsNew"/> is true when + New just made it; see
/// docs/BUILD_MODE.md for when Build removes such a creation again (#368).
/// </summary>
public sealed record BuildRoute(Guid CreationId, bool IsNew = false) : SceneRoute;

/// <summary>
/// Train setup for one saved creation (#194): Shadows and Run length before Training. Start opens
/// Training in its place, so Back from Training returns to Build.
/// </summary>
public sealed record TrainSetupRoute(Guid CreationId) : SceneRoute;

/// <summary>
/// Training for one saved creation. To train, it resumes from the creation's last finished
/// generation; leaving drops only the generation in progress. To simulate (#702), it plays the
/// saved brain with one shadow until the player leaves, and saves nothing.
/// </summary>
public sealed record TrainingRoute(Guid CreationId, TrainingRunMode Mode = TrainingRunMode.Train) : SceneRoute;
