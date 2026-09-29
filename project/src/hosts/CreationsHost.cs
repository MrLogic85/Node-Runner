using Godot;
using NodeRunner.App.Navigation;
using NodeRunner.Managers;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Hosts;

/// <summary>
/// The Creations scene, the root of the app's navigation: wires <see cref="CreationsScreen"/> to the
/// saved creations and opens the other scenes through the router.
/// </summary>
public partial class CreationsHost : Node, IRoutedScene
{
    private ISceneNavigator? _navigator;
    private UiDialog? _deleteDialog;

    private SaveManager Saves => GetNode<SaveManager>("/root/SaveManager");

    public void Enter(SceneRoute route, ISceneNavigator navigator) => _navigator = navigator;

    public override void _Ready()
    {
        // Creations is the main scene, so at startup Godot opens it without the router.
        _navigator ??= GetNode<SceneRouter>("/root/SceneRouter");
        var screen = GetNode<CreationsScreen>("%CreationsScreen");
        screen.ShowComponentLibraryLink = OS.IsDebugBuild()
            && ProjectSettings.GetSetting("ui/show_component_library_link", true).AsBool();
        screen.Setup(Saves.CreationsPresentation);
        screen.OpenRequested += OpenCreation;
        screen.NewRequested += () => Navigate(new BuildRoute());
        screen.DuplicateRequested += DuplicateCreation;
        screen.DeleteRequested += RequestDelete;
        screen.AchievementsRequested += ShowAchievementsCue;
        screen.ExamplesRequested += () => Navigate(new ExamplesRoute());
        screen.ComponentLibraryRequested += () => Navigate(new ComponentGalleryRoute());

        _deleteDialog = new UiDialog();
        AddChild(_deleteDialog);
        Refresh();
    }

    private void Navigate(SceneRoute route) => _navigator?.Navigate(new SceneNavigation(route));

    private void Refresh()
    {
        Saves.CreationsPresentation.Refresh();
        if (Saves.CreationsPresentation.LoadError is { } error)
        {
            GD.PrintErr($"Loading Creations failed: {error}");
        }
    }

    private void OpenCreation(string key, string name)
    {
        if (!CreationActions.TryParseId(key, name, "open Creation", out var id))
        {
            return;
        }

        if (Saves.Get(id) is null)
        {
            GD.PrintErr($"Creation '{name}' ({id}) was not found.");
            Refresh();
            return;
        }

        Navigate(new BuildRoute(id));
    }

    private void DuplicateCreation(string key, string name)
    {
        if (CreationActions.TryParseId(key, name, "duplicate Creation", out var id))
        {
            CreationActions.TryRunFileOperation(() => Saves.Duplicate(id), $"Duplicating Creation '{name}'");
            Refresh();
        }
    }

    private void RequestDelete(string key, string name)
    {
        if (!CreationActions.TryParseId(key, name, "delete Creation", out var id)
            || _deleteDialog is null || _deleteDialog.IsOpen)
        {
            return;
        }

        _deleteDialog.Open(CreationActions.DeleteDialog(name, () => Delete(id, name)));
    }

    private bool Delete(Guid id, string name)
    {
        var deleted = CreationActions.TryDelete(Saves, id, name);
        Refresh();
        return deleted;
    }

    private void ShowAchievementsCue() =>
        UiNotificationLayer.Enqueue(this, new UiNotificationSpec(
            UiPopupType.Default,
            "Achievements",
            "Achievements open in milestone 0.13.0.",
            Icon: new(UiIconId.Trophy)));
}
