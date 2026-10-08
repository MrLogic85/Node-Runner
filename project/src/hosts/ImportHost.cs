using Godot;
using NodeRunner.App.Navigation;
using NodeRunner.App.Repositories;
using NodeRunner.App.ViewModels;
using NodeRunner.Managers;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Hosts;

/// <summary>
/// The Import scene (#899): Paste reads the clipboard only when tapped, and Add to Creations saves
/// the previewed build under the name the player gave it and opens it in Build in place of Import,
/// so Back from Build lands on Creations, like a copied example.
/// </summary>
public partial class ImportHost : Node, IRoutedScene
{
    private ISceneNavigator? _navigator;
    private ImportPresentation _presentation = ImportPresentation.Waiting;

    public void Enter(SceneRoute route, ISceneNavigator navigator) => _navigator = navigator;

    private SaveManager Saves => GetNode<SaveManager>("/root/SaveManager");

    private ImportScreen Screen => GetNode<ImportScreen>("%ImportScreen");

    public override void _Ready()
    {
        Screen.Bind(_presentation);
        Screen.BackRequested += () => _navigator?.Back();
        Screen.PasteRequested += Paste;
        Screen.AddRequested += Add;
    }

    private void Paste()
    {
        _presentation = ImportPresentation.For(CreationShareCode.Read(DisplayServer.ClipboardGet()));
        Screen.Bind(_presentation);
    }

    private void Add(string name)
    {
        if (_presentation.Build is not { } build || name.Length == 0)
        {
            return;
        }

        if (CreationActions.TryImport(this, name, () => Saves.Import(build, name)) is not { } creation)
        {
            return;
        }

        _navigator?.Navigate(new SceneNavigation(new BuildRoute(creation.Id), KeepCurrent: false));
    }
}
