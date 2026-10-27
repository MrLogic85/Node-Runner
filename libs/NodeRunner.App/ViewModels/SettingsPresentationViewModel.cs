using System.ComponentModel;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>The Settings screen: the chosen theme and Build grid, kept in step with the saved settings however they change.</summary>
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

    /// <summary>The Grid row's sizes in metres, in the order of <see cref="SettingsDef.GridSizesCm"/>.</summary>
    public static IReadOnlyList<UiText> GridSizeLabels { get; } =
        [.. SettingsDef.GridSizesCm.Select(size => UiText.Format("{0} m", new FixedNumber(size / 100.0, DecimalsOf(size))))];

    /// <summary>The chosen grid size's place in <see cref="SettingsDef.GridSizesCm"/>.</summary>
    public int GridSizeIndex => SettingsDef.GridSizesCm.ToList().IndexOf(_settings.Current.GridSizeCm);

    public bool SnapToGrid => _settings.Current.SnapToGrid;

    /// <summary>Saves <paramref name="theme"/>; when saving throws, <see cref="Theme"/> stays as it was.</summary>
    public void ChooseTheme(ThemeChoice theme) => _settings.Update(_settings.Current with { Theme = theme });

    /// <summary>Saves the grid size at <paramref name="index"/> in <see cref="SettingsDef.GridSizesCm"/>; when saving throws, it stays as it was.</summary>
    public void ChooseGridSize(int index) =>
        _settings.Update(_settings.Current with { GridSizeCm = SettingsDef.GridSizesCm[index] });

    /// <summary>Saves whether Build snaps to the grid; when saving throws, it stays as it was.</summary>
    public void ChooseSnapToGrid(bool snap) => _settings.Update(_settings.Current with { SnapToGrid = snap });

    public void Dispose() => _settings.Changed -= OnSettingsChanged;

    // As few as the size needs: 0.25, 0.5 and 1.
    private static int DecimalsOf(int sizeCm) => sizeCm % 100 == 0 ? 0 : sizeCm % 10 == 0 ? 1 : 2;

    private void OnSettingsChanged(object? sender, SettingsDef settings) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
