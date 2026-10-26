using System.ComponentModel;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>The Settings screen: the chosen theme, kept in step with the saved settings however they change.</summary>
public sealed class SettingsPresentationViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ISettingsService _settings;

    public SettingsPresentationViewModel(ISettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        _settings.Changed += OnSettingsChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ThemeChoice Theme => _settings.Current.Theme;

    /// <summary>Saves <paramref name="theme"/>; when saving throws, <see cref="Theme"/> stays as it was.</summary>
    public void ChooseTheme(ThemeChoice theme) => _settings.Update(_settings.Current with { Theme = theme });

    public void Dispose() => _settings.Changed -= OnSettingsChanged;

    private void OnSettingsChanged(object? sender, SettingsDef settings) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
