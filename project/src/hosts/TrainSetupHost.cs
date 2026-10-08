using Godot;
using NodeRunner.App.Navigation;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Managers;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Hosts;

/// <summary>
/// The Train setup scene (#194): wires <see cref="TrainSetupScreen"/> to the saved creation. Start
/// opens Training in place of Train setup, so Back from Training returns to Build. To train, it
/// first saves the Shadows and Run length on the creation (#617); Simulate (#702) saves nothing.
/// Back discards the changes.
/// </summary>
public partial class TrainSetupHost : Node, IRoutedScene
{
    private ISceneNavigator? _navigator;
    private TrainSetupRoute? _route;
    private TrainSetupPresentationViewModel? _setup;

    public void Enter(SceneRoute route, ISceneNavigator navigator)
    {
        _route = (TrainSetupRoute)route;
        _navigator = navigator;
    }

    public override void _Ready()
    {
        var screen = GetNode<TrainSetupScreen>("%TrainSetupScreen");
        screen.BackRequested += () => _navigator?.Back();
        if (_route is null)
        {
            // Run on its own (F6) there is no creation to set up.
            return;
        }

        if (GetNode<SaveManager>("/root/SaveManager").Get(_route.CreationId) is not { } creation)
        {
            ShowMissing();
            return;
        }

        _setup = new TrainSetupPresentationViewModel(creation);
        screen.Setup(_setup);
        screen.StartRequested += Start;
    }

    private void Start()
    {
        if (_setup is null)
        {
            return;
        }

        if (_setup.Mode == TrainingRunMode.Simulate)
        {
            _navigator?.Navigate(new SceneNavigation(new TrainingRoute(_setup.CreationId, TrainingRunMode.Simulate), KeepCurrent: false));
            return;
        }

        var settings = _setup.Settings;
        var saves = GetNode<SaveManager>("/root/SaveManager");
        CreationDef? saved = null;
        if (!CreationActions.TryRunFileOperation(
                () => saved = saves.UpdateIfPresent(_setup.CreationId, creation => creation.WithTrainSettings(settings)),
                "Saving the Train setup"))
        {
            UiNotificationLayer.Enqueue(this, new UiNotificationSpec(
                UiPopupType.Danger, "Start failed", "Could not start training. Try again.", Icon: new(UiIconId.Play)));
            return;
        }

        if (saved is null)
        {
            ShowMissing();
            return;
        }

        _navigator?.Navigate(new SceneNavigation(new TrainingRoute(_setup.CreationId), KeepCurrent: false));
    }

    // Like Build, a creation that is gone sends the player back to Creations with a message.
    private void ShowMissing()
    {
        GD.PrintErr($"Creation {_route?.CreationId} was not found.");
        Notify("That creation could not be found.");
        Callable.From(() => _navigator?.ReturnToRoot()).CallDeferred();
    }

    private void Notify(string message) =>
        UiNotificationLayer.Enqueue(this, new UiNotificationSpec(UiPopupType.Default, "Train setup", message));
}
