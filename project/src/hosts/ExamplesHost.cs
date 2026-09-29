using Godot;
using NodeRunner.App.Navigation;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Managers;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Hosts;

/// <summary>
/// The Examples scene: wires <see cref="ExamplesScreen"/> to the built-in examples. Copy saves the
/// example as a new creation and opens it in Build in place of Examples, so Back from Build lands on
/// Creations, where the copy now has its own card.
/// </summary>
public partial class ExamplesHost : Node, IRoutedScene
{
    private ISceneNavigator? _navigator;

    public void Enter(SceneRoute route, ISceneNavigator navigator) => _navigator = navigator;

    public override void _Ready()
    {
        var screen = GetNode<ExamplesScreen>("%ExamplesScreen");
        screen.Setup(new ExamplesPresentationViewModel());
        screen.BackRequested += () => _navigator?.Back();
        screen.CopyRequested += CopyExample;
    }

    private void CopyExample(string key, string name)
    {
        if (!CreationActions.TryParseId(key, name, "copy example", out var id))
        {
            return;
        }

        var saves = GetNode<SaveManager>("/root/SaveManager");
        CreationDef copy = null!;
        if (!CreationActions.TryRunFileOperation(() => copy = saves.CopyExample(id), $"Copying example '{name}'"))
        {
            UiNotificationLayer.Enqueue(this, new UiNotificationSpec(
                UiPopupType.Default, "Examples", $"Could not copy {name}. Try again."));
            return;
        }

        _navigator?.Navigate(new SceneNavigation(new BuildRoute(copy.Id), KeepCurrent: false));
    }
}
