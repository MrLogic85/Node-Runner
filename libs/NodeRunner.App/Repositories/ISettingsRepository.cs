using NodeRunner.Domain;

namespace NodeRunner.App.Repositories;

public interface ISettingsRepository
{
    /// <summary>The saved settings, or <see cref="SettingsDef.Default"/> when there are none or they fail to load.</summary>
    SettingsDef Load();

    void Save(SettingsDef settings);
}
