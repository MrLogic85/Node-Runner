using Godot;
using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.Managers;

/// <summary>
/// Composition-root facade for the player's settings, loaded once from <c>user://settings.json</c>
/// (docs/SAVE_FORMAT.md). It only keeps and saves them; each consumer applies its own.
/// </summary>
public partial class SettingsManager : Node, ISettingsService
{
    private ISettingsService? _settings;

    public event EventHandler<SettingsDef>? Changed;

    public SettingsDef Current => Settings.Current;

    public override void _Ready()
    {
        var settings = new SettingsService(new FileSettingsRepository(new GodotStorageLocation(ProjectSettings.GlobalizePath("user://"))));
        settings.Changed += (_, current) => Changed?.Invoke(this, current);
        _settings = settings;
    }

    public void Update(SettingsDef settings) => Settings.Update(settings);

    private ISettingsService Settings =>
        _settings ?? throw new InvalidOperationException("SettingsManager is not ready.");
}
