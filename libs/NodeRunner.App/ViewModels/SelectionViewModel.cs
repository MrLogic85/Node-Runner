using System.ComponentModel;
using System.Runtime.CompilerServices;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

public sealed class SelectionViewModel : INotifyPropertyChanged
{
    private CreatureElementSelection? _selectedElement;

    public event PropertyChangedEventHandler? PropertyChanged;

    public CreatureElementSelection? SelectedElement
    {
        get => _selectedElement;
        private set
        {
            if (_selectedElement == value)
            {
                return;
            }

            _selectedElement = value;
            OnPropertyChanged();
        }
    }

    public void Select(CreatureElementSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        SelectedElement = selection;
    }

    public void Clear()
    {
        SelectedElement = null;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
