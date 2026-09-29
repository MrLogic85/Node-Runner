namespace NodeRunner.App.Navigation;

/// <summary>
/// A scene the router opens. The router calls <see cref="Enter"/> after instantiating the scene and
/// before it joins the tree, so the scene builds itself from its route in <c>_Ready</c>.
/// </summary>
public interface IRoutedScene
{
    void Enter(SceneRoute route, ISceneNavigator navigator);
}
