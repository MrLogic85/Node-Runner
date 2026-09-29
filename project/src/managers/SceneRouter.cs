using Godot;
using NodeRunner.App.Navigation;

namespace NodeRunner.Managers;

/// <summary>
/// The only code that changes scenes. Each route opens its own scene in place of the current one,
/// which is freed; Back rebuilds the previous scene from its route (#326). The history itself is
/// the plain-C# <see cref="SceneBackStack"/>.
/// </summary>
public partial class SceneRouter : Node, ISceneNavigator
{
    /// <summary>The scene each route opens.</summary>
    public static IReadOnlyDictionary<Type, string> ScenePaths { get; } = new Dictionary<Type, string>
    {
        [typeof(CreationsRoute)] = "res://scenes/hosts/CreationsHost.tscn",
        [typeof(ExamplesRoute)] = "res://scenes/hosts/ExamplesHost.tscn",
        [typeof(BuildRoute)] = "res://scenes/hosts/BuildHost.tscn",
        [typeof(TrainingRoute)] = "res://scenes/hosts/TrainingHost.tscn",
        [typeof(ComponentGalleryRoute)] = "res://scenes/screens/ComponentGalleryScreen.tscn",
        [typeof(ToolbarsRoute)] = "res://scenes/screens/ToolbarsScreen.tscn",
        [typeof(ColorsAndStylesRoute)] = "res://scenes/screens/ColorsAndStylesScreen.tscn",
        [typeof(PopupGalleryRoute)] = "res://scenes/screens/PopupGalleryScreen.tscn",
    };

    private readonly SceneBackStack _history = new(new CreationsRoute());

    // The outgoing scene leaves at once but the new one only joins at the end of the frame; a second
    // request in between (a double tap) is dropped rather than stacking two scene changes.
    private bool _changing;

    public override void _Ready()
    {
        GetTree().SceneChanged += OnSceneChanged;
        if (DevelopmentStartRoute() is { } start)
        {
            Callable.From(() => Navigate(new SceneNavigation(start))).CallDeferred();
        }
    }

    public override void _ExitTree() => GetTree().SceneChanged -= OnSceneChanged;

    public void Navigate(SceneNavigation navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        if (!_changing)
        {
            Open(_history.Navigate(navigation));
        }
    }

    public void Back()
    {
        if (!_changing && _history.Back() is { } previous)
        {
            Open(previous);
        }
    }

    public void ReturnToRoot()
    {
        if (!_changing)
        {
            Open(_history.ReturnToRoot());
        }
    }

    public void ReplaceCurrent(SceneRoute route)
    {
        if (!_changing)
        {
            _history.ReplaceCurrent(route);
        }
    }

    private void Open(SceneRoute route)
    {
        var scene = GD.Load<PackedScene>(ScenePaths[route.GetType()]).Instantiate();
        if (scene is IRoutedScene routed)
        {
            routed.Enter(route, this);
        }

        _changing = true;
        GetTree().ChangeSceneToNode(scene);
    }

    private void OnSceneChanged() => _changing = false;

    // Development exports can start on a component-library page (docs/UI_DIRECTION.md).
    private static SceneRoute? DevelopmentStartRoute()
    {
        if (ProjectSettings.GetSetting("ui/popup_gallery", false).AsBool())
        {
            return new PopupGalleryRoute();
        }

        return ProjectSettings.GetSetting("ui/component_gallery", false).AsBool()
            ? new ComponentGalleryRoute()
            : null;
    }
}
