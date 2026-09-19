using System.ComponentModel;
using System.Runtime.CompilerServices;
using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>Which construction-mode touch interaction is active.</summary>
public enum ConstructionTool
{
    Place,
    Beam,
    Core,
}

/// <summary>
/// Drives 0.3.0's construction mode: whether it is active, which tool is
/// selected, and the anatomy placed so far via a <see cref="CreatureBuilder"/>.
/// UI (see `project/src/ui/AGENTS.md`) binds to this instead of mutating the
/// builder directly; it may still read the Domain DTOs (<see cref="NodeDef"/>
/// etc.) this view-model exposes. See `docs/CONSTRUCTION_MODE.md`.
/// </summary>
public sealed class ConstructionViewModel : INotifyPropertyChanged
{
    private readonly CreatureBuilder _builder;
    private bool _isActive;
    private ConstructionTool _activeTool = ConstructionTool.Place;
    private int? _pendingBeamStartNode;
    private string? _statusMessage;

    public ConstructionViewModel(CreatureBuilder? builder = null)
    {
        _builder = builder ?? new CreatureBuilder();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised whenever the placed anatomy (nodes/beams/cores) changes, so the UI can redraw.</summary>
    public event EventHandler? AnatomyChanged;

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

    public ConstructionTool ActiveTool
    {
        get => _activeTool;
        set
        {
            if (_activeTool == value)
            {
                return;
            }

            _activeTool = value;
            PendingBeamStartNode = null;
            StatusMessage = null;
            OnPropertyChanged();
        }
    }

    /// <summary>The first node tapped while connecting a beam, awaiting a second node.</summary>
    public int? PendingBeamStartNode
    {
        get => _pendingBeamStartNode;
        private set
        {
            if (_pendingBeamStartNode == value)
            {
                return;
            }

            _pendingBeamStartNode = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Feedback for the current tool: instructions, confirmations, or rejection messages.</summary>
    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<NodeDef> Nodes => _builder.Nodes;

    public IReadOnlyList<BeamDef> Beams => _builder.Beams;

    public IReadOnlyList<CoreDef> Cores => _builder.Cores;

    /// <summary>Places a new node and returns its index.</summary>
    public int PlaceNode(Vector2D position, double radius)
    {
        var index = _builder.AddNode(position, radius);
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
        return index;
    }

    /// <summary>Moves an already-placed node to a new position.</summary>
    public void MoveNode(int nodeIndex, Vector2D position)
    {
        _builder.MoveNode(nodeIndex, position);
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
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

    /// <summary>
    /// Advances beam connection: the first call selects a start node, the
    /// second call (on a different node) attempts to connect them. Rejected
    /// attempts (self-connect, duplicate beam) surface via
    /// <see cref="StatusMessage"/> instead of throwing.
    /// </summary>
    public void SelectNodeForBeam(int nodeIndex)
    {
        if (PendingBeamStartNode is null)
        {
            PendingBeamStartNode = nodeIndex;
            StatusMessage = $"Node {nodeIndex} selected. Tap another node to connect.";
            return;
        }

        if (PendingBeamStartNode == nodeIndex)
        {
            PendingBeamStartNode = null;
            StatusMessage = "Beam selection cleared.";
            return;
        }

        var startNode = PendingBeamStartNode.Value;
        PendingBeamStartNode = null;

        try
        {
            _builder.AddBeam(startNode, nodeIndex);
            StatusMessage = $"Connected node {startNode} to node {nodeIndex}.";
            AnatomyChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (ArgumentException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    /// <summary>Attaches a core to <paramref name="nodeIndex"/>, or removes it if one is already there.</summary>
    public void ToggleCoreOnNode(int nodeIndex)
    {
        var existingCoreIndex = FindCoreIndexForNode(nodeIndex);
        if (existingCoreIndex >= 0)
        {
            _builder.RemoveCore(existingCoreIndex);
            StatusMessage = $"Removed core from node {nodeIndex}.";
        }
        else
        {
            _builder.AddCore(nodeIndex);
            StatusMessage = $"Attached core to node {nodeIndex}.";
        }

        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    private int FindCoreIndexForNode(int nodeIndex)
    {
        for (var i = 0; i < _builder.Cores.Count; i++)
        {
            if (_builder.Cores[i].NodeIndex == nodeIndex)
            {
                return i;
            }
        }

        return -1;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
