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
                "Tap a node or beam to inspect it.",
                "The creature is built from nodes and beams; sensors sit on beams.");
            return;
        }

        switch (selection.Kind)
        {
            case CreatureElementKind.Node:
                var nodeIndex = _creature.NodeIndexOf(selection.Id);
                var node = _creature.Nodes[nodeIndex];
                SetContent(
                    $"Node {nodeIndex + 1}",
                    "A physical attachment point. Beams meet here and can rotate relative to each other.",
                    $"Position: ({node.Position.X:0.#}, {node.Position.Y:0.#})\nRadius: {node.Radius:0.#}");
                break;
            case CreatureElementKind.Beam:
                var beamIndex = _creature.BeamIndexOf(selection.Id);
                var beam = _creature.Beams[beamIndex];
                var nodeAIndex = _creature.NodeIndexOf(beam.NodeA);
                var nodeBIndex = _creature.NodeIndexOf(beam.NodeB);
                var length = Distance(_creature.Nodes[nodeAIndex].Position, _creature.Nodes[nodeBIndex].Position);
                SetContent(
                    $"Beam {beamIndex + 1}",
                    "A rigid, fixed-length connection. It never stretches or compresses.",
                    $"Connects: Node {nodeAIndex + 1} to Node {nodeBIndex + 1}\nLength: {length:0.#}");
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
