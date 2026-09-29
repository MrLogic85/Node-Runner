namespace NodeRunner.App.Navigation;

/// <summary>How a navigated-to scene joins the back stack.</summary>
public enum SceneLaunchMode
{
    /// <summary>Pushed on top; earlier entries of the same scene stay.</summary>
    Stacked,

    /// <summary>Earlier entries of the same scene are removed before it is pushed.</summary>
    Unique,
}

/// <summary>A request to open a scene.</summary>
/// <param name="Route">The scene to open and the arguments it is built from.</param>
/// <param name="KeepCurrent">Whether the current scene stays in the back stack, so Back returns to it.</param>
/// <param name="LaunchMode">Whether earlier entries of the same scene are removed first.</param>
public sealed record SceneNavigation(
    SceneRoute Route,
    bool KeepCurrent = true,
    SceneLaunchMode LaunchMode = SceneLaunchMode.Stacked);
