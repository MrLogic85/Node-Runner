using System.ComponentModel;
using System.Runtime.CompilerServices;
using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>Which Build touch interaction is active; see <see cref="ConstructionGestures"/>.</summary>
public enum ConstructionTool
{
    Move,
    Beam,
    Joint,
    Select,

    /// <summary>Transitional: tap a node to add or remove a core, until the Parts tray drags parts onto joints (#376).</summary>
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
    /// <summary>
    /// Where joints may go, in canvas units: about six screens wide at 1×,
    /// centred on the origin. Placing and moving keep a joint's disc inside;
    /// the Build view shows it plus <see cref="BuildViewMargin"/>. Its sides
    /// are whole multiples of <see cref="BuildGridStep"/> so the grid's cells
    /// fill it exactly.
    /// </summary>
    public static readonly CanvasRect BuildArea = new(
        new Vector2D(-24 * BuildGridStep, -12 * BuildGridStep),
        new Vector2D(24 * BuildGridStep, 12 * BuildGridStep));

    /// <summary>The Build grid's cell size in canvas units.</summary>
    public const double BuildGridStep = 48;

    /// <summary>The smallest factor one Scale drag can shrink a selection by.</summary>
    public const double MinSelectionScale = 0.25;

    /// <summary>The largest factor one Scale drag can grow a selection by.</summary>
    public const double MaxSelectionScale = 4;

    /// <summary>How far past <see cref="BuildArea"/> the Build view can show, in canvas units, at any zoom.</summary>
    public const double BuildViewMargin = BuildGridStep;

    /// <summary>The part of the canvas the Build view can show: <see cref="BuildArea"/> plus <see cref="BuildViewMargin"/>.</summary>
    public static readonly CanvasRect BuildViewBounds = new(
        new Vector2D(BuildArea.Min.X - BuildViewMargin, BuildArea.Min.Y - BuildViewMargin),
        new Vector2D(BuildArea.Max.X + BuildViewMargin, BuildArea.Max.Y + BuildViewMargin));

    private CreatureBuilder _builder;
    private bool _isActive;
    private ConstructionTool _activeTool = ConstructionTool.Move;
    private string? _statusMessage;
    private bool _moveOnly;
    private int _maxCores = 1;
    private readonly HashSet<int> _selectedNodeIndices = [];
    private BrainShapeDef _brainShape = BrainShapeDef.Default;
    private string _creationName = NewCreationWorkflow.UntitledName;
    private int? _trainingGeneration;
    private double? _bestFitness;
    private int? _selectedBeamIndex;

    public ConstructionViewModel(CreatureBuilder? builder = null)
    {
        _builder = builder ?? new CreatureBuilder();
    }

    public void Load(CreatureDef creature, bool moveOnly = false, BrainShapeDef? brainShape = null, string? creationName = null, TrainingStateDef? training = null)
    {
        ArgumentNullException.ThrowIfNull(creature);
        _builder = new CreatureBuilder(creature);
        _selectedNodeIndices.Clear();
        _selectedBeamIndex = null;
        _brainShape = brainShape ?? BrainShapeDef.Default;
        _creationName = string.IsNullOrWhiteSpace(creationName) ? NewCreationWorkflow.UntitledName : creationName;
        _trainingGeneration = training?.Generation;
        _bestFitness = training?.BestFitness;
        _moveOnly = moveOnly;
        ActiveTool = ConstructionTool.Move;
        StatusMessage = null;
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Opens a saved Creation. It is fully editable until it is locked (<see cref="CreationLock"/>);
    /// a locked one only moves its nodes, so its trained brain still fits.
    /// </summary>
    public void LoadCreation(CreationDef creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        Load(creation.Creature, CreationLock.IsLocked(creation), creation.BrainShape, creation.Name, creation.Training);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised whenever the placed anatomy (nodes/beams/cores) changes, so the UI can redraw.</summary>
    public event EventHandler? AnatomyChanged;

    /// <summary>True for a locked Creation: parts and brain shape are fixed, and only nodes move.</summary>
    public bool IsMoveOnly => _moveOnly;

    public string CreationName => _creationName;

    public int? TrainingGeneration => _trainingGeneration;

    public double? BestFitness => _bestFitness;

    public int SelectedNodeCount => _selectedNodeIndices.Count;

    public int SelectedBeamCount => _selectedBeamIndex is null ? 0 : 1;

    public int SelectedPartCount => SelectedNodeCount + SelectedBeamCount;

    public int SelectedCoreCount => _builder.Cores.Count(core => _selectedNodeIndices.Contains(core.NodeIndex));

    public int? SingleSelectedNodeIndex => _selectedNodeIndices.Count == 1
        ? _selectedNodeIndices.First()
        : null;

    public int? SingleSelectedBeamIndex => SelectedPartCount == 1 ? _selectedBeamIndex : null;

    public bool SingleSelectionHasCore => SingleSelectedNodeIndex is { } index
        && _builder.Cores.Any(core => core.NodeIndex == index);

    public void SetCreationName(string creationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(creationName);
        if (_creationName == creationName)
        {
            return;
        }

        _creationName = creationName;
        OnPropertyChanged(nameof(CreationName));
    }

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
            StatusMessage = null;
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

    public IReadOnlyCollection<int> SelectedNodeIndices => _selectedNodeIndices;

    public int MaxCores => _maxCores;

    public BrainShapeDef BrainShape => _brainShape;

    public void SetBrainShape(BrainShapeDef brainShape)
    {
        ArgumentNullException.ThrowIfNull(brainShape);
        if (_brainShape == brainShape)
        {
            return;
        }

        _brainShape = brainShape;
        OnPropertyChanged(nameof(BrainShape));
    }

    public void SetMaxCores(int maxCores)
    {
        if (maxCores < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCores));
        }

        _maxCores = maxCores;
        OnPropertyChanged(nameof(MaxCores));
    }

    /// <summary>Places a new node, moved inside <see cref="BuildArea"/>, and returns its index.</summary>
    public int PlaceNode(Vector2D position, double radius)
    {
        if (_moveOnly)
        {
            throw new InvalidOperationException("Edit mode can only move existing nodes.");
        }

        var index = _builder.AddNode(BuildArea.Clamp(position, radius), radius);
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
        return index;
    }

    /// <summary>Moves an already-placed node to a new position, as far as <see cref="BuildArea"/> reaches.</summary>
    public void MoveNode(int nodeIndex, Vector2D position)
    {
        _builder.MoveNode(nodeIndex, BuildArea.Clamp(position, _builder.Nodes[nodeIndex].Radius));
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleSelectedNode(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _builder.Nodes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeIndex));
        }

        if (!_selectedNodeIndices.Add(nodeIndex))
        {
            _selectedNodeIndices.Remove(nodeIndex);
        }

        _selectedBeamIndex = null;
        StatusMessage = _selectedNodeIndices.Count == 0
            ? "Selection cleared."
            : $"{_selectedNodeIndices.Count} selected. Drag one selected node to move them together.";
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectBeam(int beamIndex)
    {
        if (beamIndex < 0 || beamIndex >= _builder.Beams.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(beamIndex));
        }

        _selectedNodeIndices.Clear();
        _selectedBeamIndex = beamIndex;
        StatusMessage = $"Beam {beamIndex + 1} selected.";
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSelection()
    {
        if (SelectedPartCount == 0)
        {
            return;
        }

        _selectedNodeIndices.Clear();
        _selectedBeamIndex = null;
        StatusMessage = "Selection cleared.";
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReplaceSelection(IEnumerable<int> nodeIndices)
    {
        ArgumentNullException.ThrowIfNull(nodeIndices);
        _selectedNodeIndices.Clear();
        _selectedBeamIndex = null;
        foreach (var nodeIndex in nodeIndices)
        {
            if (nodeIndex >= 0 && nodeIndex < _builder.Nodes.Count)
            {
                _selectedNodeIndices.Add(nodeIndex);
            }
        }

        StatusMessage = _selectedNodeIndices.Count == 0
            ? "Selection cleared."
            : $"{_selectedNodeIndices.Count} selected. Drag one selected node to move them together.";
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The selected joints' positions and pivot, for a Select drag to transform.</summary>
    public SelectionSnapshot SnapshotSelection()
    {
        if (_selectedNodeIndices.Count == 0)
        {
            throw new InvalidOperationException("Nothing is selected.");
        }

        var positions = _selectedNodeIndices.ToDictionary(index => index, index => _builder.Nodes[index].Position);
        var pivot = new Vector2D(
            (positions.Values.Min(p => p.X) + positions.Values.Max(p => p.X)) / 2,
            (positions.Values.Min(p => p.Y) + positions.Values.Max(p => p.Y)) / 2);
        return new SelectionSnapshot(positions, pivot);
    }

    /// <summary>Moves the snapshot's joints by <paramref name="delta"/>, shortened so the whole group stays inside <see cref="BuildArea"/>.</summary>
    public void TranslateSelection(SelectionSnapshot start, Vector2D delta)
    {
        ArgumentNullException.ThrowIfNull(start);
        foreach (var (index, position) in start.Positions)
        {
            var moved = new Vector2D(position.X + delta.X, position.Y + delta.Y);
            var allowed = BuildArea.Clamp(moved, _builder.Nodes[index].Radius);
            delta = new Vector2D(delta.X + allowed.X - moved.X, delta.Y + allowed.Y - moved.Y);
        }

        // Clamping each joint too absorbs the rounding in the shortened delta.
        PlaceSelection(start, (index, position) =>
            BuildArea.Clamp(new Vector2D(position.X + delta.X, position.Y + delta.Y), _builder.Nodes[index].Radius));
    }

    /// <summary>Turns the snapshot's joints <paramref name="radians"/> about its pivot; a turn that would leave <see cref="BuildArea"/> is ignored.</summary>
    public void RotateSelection(SelectionSnapshot start, double radians)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (!double.IsFinite(radians))
        {
            throw new ArgumentOutOfRangeException(nameof(radians));
        }

        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var pivot = start.Pivot;
        PlaceSelection(start, (_, position) =>
        {
            var dx = position.X - pivot.X;
            var dy = position.Y - pivot.Y;
            return new Vector2D(pivot.X + (dx * cos) - (dy * sin), pivot.Y + (dx * sin) + (dy * cos));
        });
    }

    /// <summary>
    /// Spreads the snapshot's joints from its pivot by <paramref name="factor"/>, clamped to
    /// <see cref="MinSelectionScale"/>..<see cref="MaxSelectionScale"/> so it never collapses or
    /// reflects; a scale that would leave <see cref="BuildArea"/> is ignored. A locked creation
    /// scales too: like a move, it only changes beam lengths, not the parts.
    /// </summary>
    public void ScaleSelection(SelectionSnapshot start, double factor)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (!double.IsFinite(factor))
        {
            throw new ArgumentOutOfRangeException(nameof(factor));
        }

        factor = Math.Clamp(factor, MinSelectionScale, MaxSelectionScale);
        var pivot = start.Pivot;
        PlaceSelection(start, (_, position) => new Vector2D(
            pivot.X + ((position.X - pivot.X) * factor),
            pivot.Y + ((position.Y - pivot.Y) * factor)));
    }

    /// <summary>Puts the snapshot's joints back where they were.</summary>
    public void RestoreSelection(SelectionSnapshot start)
    {
        ArgumentNullException.ThrowIfNull(start);
        PlaceSelection(start, (_, position) => position, keepInBuildArea: false);
    }

    private void PlaceSelection(SelectionSnapshot start, Func<int, Vector2D, Vector2D> place, bool keepInBuildArea = true)
    {
        var placed = start.Positions.ToDictionary(entry => entry.Key, entry => place(entry.Key, entry.Value));
        if (keepInBuildArea && placed.Any(entry => BuildArea.Clamp(entry.Value, _builder.Nodes[entry.Key].Radius) != entry.Value))
        {
            return;
        }

        foreach (var (index, position) in placed)
        {
            _builder.MoveNode(index, position);
        }

        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Finds the closest placed node whose centre is within
    /// <paramref name="maxDistance"/> of <paramref name="position"/>, or whose
    /// disc contains it, if any. Used to hit-test nodes.
    /// </summary>
    public bool TryFindNodeNear(Vector2D position, double maxDistance, out int nodeIndex)
    {
        nodeIndex = -1;
        var bestDistanceSquared = double.PositiveInfinity;

        for (var i = 0; i < _builder.Nodes.Count; i++)
        {
            var node = _builder.Nodes[i];
            var dx = node.Position.X - position.X;
            var dy = node.Position.Y - position.Y;
            var distanceSquared = (dx * dx) + (dy * dy);
            var reach = Math.Max(maxDistance, node.Radius);
            if (distanceSquared <= reach * reach && distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                nodeIndex = i;
            }
        }

        return nodeIndex >= 0;
    }

    /// <summary>Whether <see cref="ConnectBeam"/> would join this pair: unlocked, and the builder accepts the beam.</summary>
    public bool CanConnect(int nodeA, int nodeB) => !_moveOnly && _builder.CanAddBeam(nodeA, nodeB);

    /// <summary>
    /// Joins two existing nodes with a beam. Rejected attempts (locked
    /// Creation, self-connect, duplicate beam) surface via
    /// <see cref="StatusMessage"/> instead of throwing.
    /// </summary>
    public bool ConnectBeam(int nodeA, int nodeB)
    {
        if (_moveOnly)
        {
            StatusMessage = "Edit mode only allows moving existing nodes.";
            return false;
        }

        try
        {
            _builder.AddBeam(nodeA, nodeB);
        }
        catch (ArgumentException exception)
        {
            StatusMessage = exception.Message;
            return false;
        }

        StatusMessage = $"Connected node {nodeA} to node {nodeB}.";
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Adds a node at the point on beam <paramref name="beamIndex"/> closest to
    /// <paramref name="position"/> and replaces the beam with two beams through
    /// it, as one change. Returns the new node's index, or null when the
    /// Creation is locked or the closest point is an end of the beam (or the
    /// beam has no length), where a split would stack two nodes.
    /// </summary>
    public int? SplitBeam(int beamIndex, Vector2D position, double radius)
    {
        if (beamIndex < 0 || beamIndex >= _builder.Beams.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(beamIndex));
        }

        if (_moveOnly)
        {
            StatusMessage = "Edit mode only allows moving existing nodes.";
            return null;
        }

        var beam = _builder.Beams[beamIndex];
        var start = _builder.Nodes[beam.NodeA].Position;
        var end = _builder.Nodes[beam.NodeB].Position;
        var t = ClosestPointParameter(position, start, end);
        if (t <= 0 || t >= 1)
        {
            return null;
        }

        var splitPoint = new Vector2D(start.X + (t * (end.X - start.X)), start.Y + (t * (end.Y - start.Y)));
        var nodeIndex = _builder.AddNode(splitPoint, radius);
        _builder.RemoveBeam(beamIndex);
        _builder.AddBeam(beam.NodeA, nodeIndex);
        _builder.AddBeam(nodeIndex, beam.NodeB);
        _selectedNodeIndices.Clear();
        _selectedBeamIndex = null;
        StatusMessage = "Split the beam with a new joint.";
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
        return nodeIndex;
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
            if (_builder.Cores.Count >= _maxCores)
            {
                StatusMessage = $"Core limit reached ({_maxCores}). Train to unlock another core slot.";
                return;
            }

            _builder.AddCore(nodeIndex);
            StatusMessage = $"Attached core to node {nodeIndex}.";
        }

        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Finds the closest beam within <paramref name="maxDistance"/> of
    /// <paramref name="position"/> (measured to the beam's line segment), if
    /// any. Used to hit-test beams, since a beam has no single point like a
    /// node does.
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

    public void DeleteSelectedParts()
    {
        if (_moveOnly)
        {
            StatusMessage = "Edit mode can only move selected parts.";
            return;
        }

        if (SelectedPartCount == 0)
        {
            StatusMessage = "No selected parts to delete.";
            return;
        }

        if (_selectedBeamIndex is { } beamIndex)
        {
            _builder.RemoveBeam(beamIndex);
        }

        foreach (var nodeIndex in _selectedNodeIndices.OrderByDescending(index => index))
        {
            _builder.RemoveNode(nodeIndex);
        }

        _selectedNodeIndices.Clear();
        _selectedBeamIndex = null;
        StatusMessage = "Deleted selected parts.";
        NotifySelectionChanged();
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

    /// <summary>The drawing as it stands, finished or not: what leaving Build saves on a saved Creation.</summary>
    public CreatureDef Snapshot() => _builder.Build();

    /// <summary>
    /// The creature for Start training, if it can train (<see cref="CreatureReadiness"/>). A creature
    /// that cannot be simulated yet shows why via <see cref="StatusMessage"/>; an empty one, or one
    /// without a motor relation, is refused quietly because Build already shows it is not ready.
    /// </summary>
    public bool TryGetTrainableCreature(out CreatureDef? creature)
    {
        if (!TryLeave(out creature, out var errors))
        {
            SetBlockedLeaveMessage(errors);
            return false;
        }

        if (creature is null || !CreatureReadiness.CanTrain(creature))
        {
            creature = null;
            return false;
        }

        return true;
    }

    /// <summary>Surfaces why leaving Build mode was blocked, via <see cref="StatusMessage"/>.</summary>
    public void SetBlockedLeaveMessage(IReadOnlyList<string> errors)
    {
        StatusMessage = $"Not ready to simulate yet: {string.Join(" ", errors)}";
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

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedNodeCount));
        OnPropertyChanged(nameof(SelectedBeamCount));
        OnPropertyChanged(nameof(SelectedPartCount));
        OnPropertyChanged(nameof(SelectedCoreCount));
        OnPropertyChanged(nameof(SingleSelectedNodeIndex));
        OnPropertyChanged(nameof(SingleSelectedBeamIndex));
        OnPropertyChanged(nameof(SingleSelectionHasCore));
    }

    private static double DistanceSquaredToSegment(Vector2D point, Vector2D segmentStart, Vector2D segmentEnd)
    {
        var t = ClosestPointParameter(point, segmentStart, segmentEnd);
        var closestX = segmentStart.X + (t * (segmentEnd.X - segmentStart.X));
        var closestY = segmentStart.Y + (t * (segmentEnd.Y - segmentStart.Y));

        var dx = point.X - closestX;
        var dy = point.Y - closestY;
        return (dx * dx) + (dy * dy);
    }

    /// <summary>Where along the segment (0 at its start, 1 at its end) the point closest to <paramref name="point"/> lies.</summary>
    private static double ClosestPointParameter(Vector2D point, Vector2D segmentStart, Vector2D segmentEnd)
    {
        var segmentX = segmentEnd.X - segmentStart.X;
        var segmentY = segmentEnd.Y - segmentStart.Y;
        var segmentLengthSquared = (segmentX * segmentX) + (segmentY * segmentY);
        if (segmentLengthSquared <= 0)
        {
            return 0;
        }

        var pointX = point.X - segmentStart.X;
        var pointY = point.Y - segmentStart.Y;
        return Math.Clamp(((pointX * segmentX) + (pointY * segmentY)) / segmentLengthSquared, 0, 1);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
