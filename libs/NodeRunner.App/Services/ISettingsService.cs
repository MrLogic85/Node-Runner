using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>The player's settings, loaded once at startup and saved on every change.</summary>
public interface ISettingsService
{
    /// <summary>Raised after <see cref="Current"/> changes, with the new settings.</summary>
    event EventHandler<SettingsDef>? Changed;

    SettingsDef Current { get; }

    /// <summary>
    /// Saves <paramref name="settings"/> and makes them current, then raises <see cref="Changed"/>.
    /// Settings equal to <see cref="Current"/> are neither saved nor raised. When saving throws,
    /// <see cref="Current"/> stays as it was.
    /// </summary>
    void Update(SettingsDef settings);
}
