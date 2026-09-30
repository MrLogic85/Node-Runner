namespace NodeRunner.App.Navigation;

/// <summary>Opens scenes and goes back through the history. The <c>SceneRouter</c> autoload implements it.</summary>
public interface ISceneNavigator
{
    void Navigate(SceneNavigation navigation);

    /// <summary>Returns to the previous scene; does nothing on the root.</summary>
    void Back();

    void ReturnToRoot();

    /// <summary>
    /// Gives the current scene's history entry new arguments without reopening it, so Back later
    /// rebuilds what the scene shows now (a new creation's Build once it starts training).
    /// </summary>
    void ReplaceCurrent(SceneRoute route);
}
