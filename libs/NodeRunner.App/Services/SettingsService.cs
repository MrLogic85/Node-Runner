using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _repository;

    public SettingsService(ISettingsRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
        Current = repository.Load();
    }

    public event EventHandler<SettingsDef>? Changed;

    public SettingsDef Current { get; private set; }

    public void Update(SettingsDef settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings == Current)
        {
            return;
        }

        _repository.Save(settings);
        Current = settings;
        Changed?.Invoke(this, settings);
    }
}
