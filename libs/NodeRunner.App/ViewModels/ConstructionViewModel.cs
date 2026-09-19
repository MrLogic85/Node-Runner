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
    Delete,
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
    private CreatureBuilder _builder;
    private bool _isActive;
    private ConstructionTool _activeTool = ConstructionTool.Place;
    private int? _pendingBeamStartNode;
    private string? _statusMessage;
    private bool _moveOnly;

    public ConstructionViewModel(CreatureBuilder? builder = null)
    {
        _builder = builder ?? new CreatureBuilder();
    }

    public void Load(CreatureDef creature, bool moveOnly = false)
    {
        ArgumentNullException.ThrowIfNull(creature);
        _builder = new CreatureBuilder(creature);
        _moveOnly = moveOnly;
        PendingBeamStartNode = null;
        StatusMessage = null;
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised whenever the placed anatomy (nodes/beams/cores) changes, so the UI can redraw.</summary>
    public event EventHandler? AnatomyChanged;

    public bool IsMoveOnly => _moveOnly;

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
        if (_moveOnly)
        {
            throw new InvalidOperationException("Edit mode can only move existing nodes.");
        }

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
        if (_moveOnly)
        {
            StatusMessage = "Edit mode only allows moving existing nodes.";
            return;
        }

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
        if (_moveOnly)
        {
            StatusMessage = "Edit mode only allows moving existing nodes.";
            return;
        }

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

    /// <summary>
    /// Finds the closest beam within <paramref name="maxDistance"/> of
    /// <paramref name="position"/> (measured to the beam's line segment), if
    /// any. Used to hit-test beams for the Delete tool, since a beam has no
    /// single point like a node does.
    /// </summary>
    public bool TryFindBeamNear(Vector2D position, double maxDistance, out int beamIndex)
    {
        beamIndex = -1;
        var bestDistanceSquared = maxDistance * maxDistance;

        for (var i = 0; i < _builder.Beams.Count; i++)
        {
            var beam = _builder.Beams[i];
            var distanceSquared = DistanceSquaredToSegment(position, _builder.Nodes[beam.NodeA].Position, _builder.Nodes[beam.NodeB].Position);
            if (distanceSquared <= bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                beamIndex = i;
            }
        }

        return beamIndex >= 0;
    }

    /// <summary>Removes a node, cascading to any beams/cores attached to it (see <see cref="CreatureBuilder.RemoveNode"/>).</summary>
    public void DeleteNode(int nodeIndex)
    {
        if (_moveOnly)
        {
            StatusMessage = "Edit mode only allows moving existing nodes.";
            return;
        }

        _builder.RemoveNode(nodeIndex);
        StatusMessage = $"Removed node {nodeIndex} and anything attached to it.";
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes a beam, leaving both of its nodes in place.</summary>
    public void DeleteBeam(int beamIndex)
    {
        if (_moveOnly)
        {
            StatusMessage = "Edit mode only allows moving existing nodes.";
            return;
        }

        _builder.RemoveBeam(beamIndex);
        StatusMessage = "Removed beam.";
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Checks whether the current anatomy is valid enough to leave
    /// construction mode. An empty anatomy (nothing placed yet) is always
    /// allowed, so a user who opens Build mode without editing anything can
    /// freely return to Simulate; in that case <paramref name="creature"/>
    /// is null and the caller should keep whatever creature is already
    /// running. Otherwise this defers to <see cref="CreatureBuilder.TryBuild"/>'s
    /// validation and, on success, returns the built <see cref="CreatureDef"/>
    /// for the caller to instantiate (see #72).
    /// </summary>
    public bool TryLeave(out CreatureDef? creature, out IReadOnlyList<string> errors)
    {
        if (_builder.Nodes.Count == 0)
        {
            creature = null;
            errors = [];
            return true;
        }

        return _builder.TryBuild(out creature, out errors);
    }

    /// <summary>Surfaces why leaving Build mode was blocked, via <see cref="StatusMessage"/>.</summary>
    public void SetBlockedLeaveMessage(IReadOnlyList<string> errors)
    {
        StatusMessage = $"Not ready to simulate yet: {string.Join(" ", errors)}";
    }

    public void SetCompletedMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        StatusMessage = message;
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

    private static double DistanceSquaredToSegment(Vector2D point, Vector2D segmentStart, Vector2D segmentEnd)
    {
        var segmentX = segmentEnd.X - segmentStart.X;
        var segmentY = segmentEnd.Y - segmentStart.Y;
        var segmentLengthSquared = (segmentX * segmentX) + (segmentY * segmentY);

        var pointX = point.X - segmentStart.X;
        var pointY = point.Y - segmentStart.Y;

        var t = segmentLengthSquared > 0 ? Math.Clamp(((pointX * segmentX) + (pointY * segmentY)) / segmentLengthSquared, 0, 1) : 0;

        var closestX = segmentStart.X + (t * segmentX);
        var closestY = segmentStart.Y + (t * segmentY);

        var dx = point.X - closestX;
        var dy = point.Y - closestY;
        return (dx * dx) + (dy * dy);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
