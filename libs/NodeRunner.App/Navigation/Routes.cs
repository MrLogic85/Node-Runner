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
