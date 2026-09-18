using System.ComponentModel;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

public sealed class CreatureInspectorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly CreatureDef _creature;
    private readonly SelectionViewModel _selection;

    public CreatureInspectorViewModel(CreatureDef creature, SelectionViewModel selection)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(selection);

        _creature = creature;
        _selection = selection;
        _selection.PropertyChanged += OnSelectionPropertyChanged;
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; private set; } = string.Empty;

    public string Role { get; private set; } = string.Empty;

    public string Values { get; private set; } = string.Empty;

    public void Dispose()
    {
        _selection.PropertyChanged -= OnSelectionPropertyChanged;
    }

    private void OnSelectionPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(SelectionViewModel.SelectedElement))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var selection = _selection.SelectedElement;
        if (selection is null)
        {
            SetContent(
                "Creature inspector",
                "Tap a node, beam, or core to inspect it.",
                "The creature is built from nodes, beams, and cores.");
            return;
        }

        switch (selection.Kind)
        {
            case CreatureElementKind.Node:
                var node = _creature.Nodes[selection.Index];
                SetContent(
                    $"Node {selection.Index + 1}",
                    "A physical attachment point. Beams meet here and can rotate relative to each other.",
                    $"Position: ({node.Position.X:0.#}, {node.Position.Y:0.#})\nRadius: {node.Radius:0.#}");
                break;
            case CreatureElementKind.Beam:
                var beam = _creature.Beams[selection.Index];
                var length = Distance(_creature.Nodes[beam.NodeA].Position, _creature.Nodes[beam.NodeB].Position);
                SetContent(
                    $"Beam {selection.Index + 1}",
                    "A rigid, fixed-length connection. It never stretches or compresses.",
                    $"Connects: Node {beam.NodeA + 1} to Node {beam.NodeB + 1}\nLength: {length:0.#}");
                break;
            case CreatureElementKind.Core:
                var core = _creature.Cores[selection.Index];
                SetContent(
                    $"Core {selection.Index + 1}",
                    "A sensor package. Not the brain itself — it feeds sensor readings (rays, pitch, elevation, speed) to the model.",
                    $"Mounted on: Node {core.NodeIndex + 1}");
                break;
        }
    }

    private void SetContent(string title, string role, string values)
    {
        Title = title;
        Role = role;
        Values = values;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private static double Distance(Vector2D a, Vector2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
