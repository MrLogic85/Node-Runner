using Godot;
using NodeRunner.App.Navigation;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
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
        _presentation = new SettingsPresentationViewModel(GetNode<SettingsManager>("/root/SettingsManager"));
        var screen = GetNode<SettingsScreen>("%SettingsScreen");
        screen.Setup(_presentation);
        screen.BackRequested += () => _navigator?.Back();
        screen.ThemePicked += ChooseTheme;
    }

    public override void _ExitTree() => _presentation?.Dispose();

    private void ChooseTheme(ThemeChoice theme)
    {
        if (_presentation is null)
        {
            return;
        }

        if (!FileOperations.TryRun(() => _presentation.ChooseTheme(theme), $"Saving the theme {theme}"))
        {
            UiNotificationLayer.Enqueue(this, new UiNotificationSpec(UiPopupType.Danger, "Not saved", "Could not save the theme. Try again.", Icon: new(UiIconId.Warn))
            {
                Id = "settings.save.failed",
            });
        }
    }
}
