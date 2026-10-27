using Godot;
using NodeRunner.App.Navigation;
using NodeRunner.App.ViewModels;
using NodeRunner.Managers;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Hosts;

/// <summary>
/// The Settings scene: wires <see cref="SettingsScreen"/> to the saved settings, which
/// <see cref="SettingsApplier"/> applies as they change. A choice that cannot be saved is not
/// applied, and a notification says so.
/// </summary>
public partial class SettingsHost : Control, IRoutedScene
{
    private ISceneNavigator? _navigator;
    private SettingsPresentationViewModel? _presentation;

    public void Enter(SceneRoute route, ISceneNavigator navigator) => _navigator = navigator;

    public override void _Ready()
    {
        var presentation = new SettingsPresentationViewModel(GetNode<SettingsManager>("/root/SettingsManager"));
        _presentation = presentation;
        var screen = GetNode<SettingsScreen>("%SettingsScreen");
        screen.Setup(presentation);
        screen.BackRequested += () => _navigator?.Back();
        screen.ThemePicked += theme => Save(() => presentation.ChooseTheme(theme), $"Saving the theme {theme}", "Could not save the theme. Try again.");
        screen.GridSizePicked += index => Save(() => presentation.ChooseGridSize(index), $"Saving grid size {index}", "Could not save the grid size. Try again.");
        screen.SnapToGridToggled += on => Save(() => presentation.ChooseSnapToGrid(on), $"Saving snap to grid {on}", "Could not save snap to grid. Try again.");
    }

    public override void _ExitTree() => _presentation?.Dispose();

    private void Save(Action choose, string operation, string failure)
    {
        if (!FileOperations.TryRun(choose, operation))
        {
            UiNotificationLayer.Enqueue(this, new UiNotificationSpec(UiPopupType.Danger, "Not saved", failure, Icon: new(UiIconId.Warn))
            {
                Id = "settings.save.failed",
            });
        }
    }
}
