namespace NodeRunner.App.Navigation;

/// <summary>
/// One scene and the arguments it is built from. Each scene has its own sealed record deriving from
/// this, so the record type is the scene's identity and its properties are the arguments (a creation
/// id, an achievement id, …). Arguments are plain values: a scene is closed when it is left and
/// rebuilt from its route on Back, so a route never holds a live object.
/// </summary>
public abstract record SceneRoute
{
    /// <summary>True when <paramref name="other"/> is the same scene, whatever its arguments.</summary>
    public bool IsSameScene(SceneRoute other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.GetType() == GetType();
    }
}
