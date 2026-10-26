using Godot;
using NodeRunner.Domain;
using NodeRunner.Managers;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Hosts;

/// <summary>
/// Applies the player's saved settings to the UI autoloads, at start and whenever they change.
/// Registered as an autoload after <see cref="SettingsManager"/> and the UI autoloads it feeds,
/// so the first scene opens with them applied.
/// </summary>
public sealed partial class SettingsApplier : Node
{
    private SettingsManager Settings => GetNode<SettingsManager>("/root/SettingsManager");

    public override void _Ready()
    {
        Settings.Changed += OnChanged;
        Apply(Settings.Current);
    }

    public override void _ExitTree() => Settings.Changed -= OnChanged;

    private void OnChanged(object? sender, SettingsDef settings) => Apply(settings);

    private void Apply(SettingsDef settings) => UiAppTheme.Of(this).Choose(ThemeChoices.ToUi(settings.Theme));
}
