namespace NodeRunner.App.Navigation;

/// <summary>How a navigated-to scene joins the back stack.</summary>
public enum SceneLaunchMode
{
    /// <summary>Pushed on top; earlier entries of the same scene stay.</summary>
    Stacked,

    /// <summary>Earlier entries of the same scene are removed before it is pushed.</summary>
    Unique,
}

/// <summary>
/// A request to open a scene. <c>KeepCurrent</c> says whether the current scene stays in the back
/// stack, so Back returns to it.
/// </summary>
public sealed record SceneNavigation(
    SceneRoute Route,
    bool KeepCurrent = true,
    SceneLaunchMode LaunchMode = SceneLaunchMode.Stacked);
