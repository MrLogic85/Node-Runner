using NodeRunner.Domain;

namespace NodeRunner.App.Repositories;

public sealed class InMemorySettingsRepository : ISettingsRepository
{
    private SettingsDef _settings = SettingsDef.Default;

    public SettingsDef Load() => _settings;

    public void Save(SettingsDef settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }
}
