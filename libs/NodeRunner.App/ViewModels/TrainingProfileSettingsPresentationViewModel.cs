using System.ComponentModel;

namespace NodeRunner.App.ViewModels;

public sealed record TrainingProfileOptionPresentation(string Name, string Detail);

public sealed class TrainingProfileSettingsPresentationViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<TrainingProfileOptionPresentation> Options { get; private set; } =
        Array.Empty<TrainingProfileOptionPresentation>();

    public int SelectedIndex { get; private set; }

    public void Update(IEnumerable<TrainingProfileOptionPresentation> options, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(options);

        var optionList = options.ToArray();
        if (optionList.Length == 0)
        {
            throw new ArgumentException("At least one training profile option is required.", nameof(options));
        }
        if (selectedIndex < 0 || selectedIndex >= optionList.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        }

        if (SelectedIndex == selectedIndex && Options.SequenceEqual(optionList))
        {
            return;
        }

        Options = Array.AsReadOnly(optionList);
        SelectedIndex = selectedIndex;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
