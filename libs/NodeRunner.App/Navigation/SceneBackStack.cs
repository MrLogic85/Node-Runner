namespace NodeRunner.App.Navigation;

/// <summary>
/// The navigation history: the root scene at the bottom and the current scene on top. The root
/// (Creations) is never removed; opening the root's scene again returns to it.
/// </summary>
public sealed class SceneBackStack
{
    private readonly List<SceneRoute> _entries;

    public SceneBackStack(SceneRoute root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _entries = [root];
    }

    public SceneRoute Root => _entries[0];

    public SceneRoute Current => _entries[^1];

    /// <summary>Bottom to top; the last entry is the current scene.</summary>
    public IReadOnlyList<SceneRoute> Entries => _entries.AsReadOnly();

    /// <summary>False on the root, where Back leaves the app.</summary>
    public bool CanGoBack => _entries.Count > 1;

    /// <summary>Opens a scene and returns the route to build it from.</summary>
    public SceneRoute Navigate(SceneNavigation navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);

        var route = navigation.Route;
        if (route.IsSameScene(Root))
        {
            _entries.RemoveRange(1, _entries.Count - 1);
            _entries[0] = route;
            return route;
        }

        if (!navigation.KeepCurrent && CanGoBack)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }

        if (navigation.LaunchMode == SceneLaunchMode.Unique)
        {
            _entries.RemoveAll(entry => entry.IsSameScene(route));
        }

        _entries.Add(route);
        return route;
    }

    /// <summary>
    /// Updates the current entry's arguments without reopening its scene, for a scene whose state
    /// changed under it: a new creation's Build stops being new once it starts training. Only the
    /// same scene.
    /// </summary>
    public void ReplaceCurrent(SceneRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (!route.IsSameScene(Current))
        {
            throw new ArgumentException($"{route.GetType().Name} is not the current scene, {Current.GetType().Name}.", nameof(route));
        }

        _entries[^1] = route;
    }

    /// <summary>
    /// Closes the current scene and returns the previous one to rebuild, or null on the root, where
    /// Back belongs to the platform (Android leaves the app).
    /// </summary>
    public SceneRoute? Back()
    {
        if (!CanGoBack)
        {
            return null;
        }

        _entries.RemoveAt(_entries.Count - 1);
        return Current;
    }

    /// <summary>Closes every scene above the root and returns the root to rebuild.</summary>
    public SceneRoute ReturnToRoot()
    {
        _entries.RemoveRange(1, _entries.Count - 1);
        return Root;
    }
}
