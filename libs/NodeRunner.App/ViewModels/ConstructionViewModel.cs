using System.ComponentModel;
using System.Runtime.CompilerServices;
using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// Drives 0.3.0's construction mode: whether it is active, and the nodes
/// placed so far via a <see cref="CreatureBuilder"/>. UI (see
/// `project/src/ui/AGENTS.md`) binds to this instead of mutating the
/// builder directly; it may still read the Domain DTOs (<see cref="NodeDef"/>
/// etc.) this view-model exposes. See `docs/CONSTRUCTION_MODE.md`.
/// </summary>
public sealed class ConstructionViewModel : INotifyPropertyChanged
{
    private readonly CreatureBuilder _builder;
    private bool _isActive;

    public ConstructionViewModel(CreatureBuilder? builder = null)
    {
        _builder = builder ?? new CreatureBuilder();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised whenever a node is added or moved, so the UI can redraw.</summary>
    public event EventHandler? NodesChanged;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<NodeDef> Nodes => _builder.Nodes;

    /// <summary>Places a new node and returns its index.</summary>
    public int PlaceNode(Vector2D position, double radius)
    {
        var index = _builder.AddNode(position, radius);
        NodesChanged?.Invoke(this, EventArgs.Empty);
        return index;
    }

    /// <summary>Moves an already-placed node to a new position.</summary>
    public void MoveNode(int nodeIndex, Vector2D position)
    {
        _builder.MoveNode(nodeIndex, position);
        NodesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Finds the closest placed node within <paramref name="maxDistance"/> of
    /// <paramref name="position"/>, if any. Used to decide whether a touch
    /// should start dragging an existing node instead of placing a new one.
    /// </summary>
    public bool TryFindNodeNear(Vector2D position, double maxDistance, out int nodeIndex)
    {
        nodeIndex = -1;
        var bestDistanceSquared = maxDistance * maxDistance;

        for (var i = 0; i < _builder.Nodes.Count; i++)
        {
            var dx = _builder.Nodes[i].Position.X - position.X;
            var dy = _builder.Nodes[i].Position.Y - position.Y;
            var distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared <= bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                nodeIndex = i;
            }
        }

        return nodeIndex >= 0;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
