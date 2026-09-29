namespace NodeRunner.App.Navigation;

/// <summary>Opens scenes and goes back through the history. The <c>SceneRouter</c> autoload implements it.</summary>
public interface ISceneNavigator
{
    void Navigate(SceneNavigation navigation);

    /// <summary>Returns to the previous scene; does nothing on the root.</summary>
    void Back();

    void ReturnToRoot();
}
