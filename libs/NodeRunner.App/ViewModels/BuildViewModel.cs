using System.ComponentModel;
using System.Runtime.CompilerServices;
using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>Which Build touch interaction is active; see <see cref="BuildGestures"/>.</summary>
public enum BuildTool
{
    Move,
    Beam,
    Joint,
    Select,

    /// <summary>Picked from the tray's Links tab (#451): drag from one joint to another to place a Piston.</summary>
    Piston,
}

/// <summary>
/// Drives 0.3.0's Build mode: whether it is active, which tool is
/// selected, and the anatomy placed so far via a <see cref="CreatureBuilder"/>.
/// UI (see `project/src/ui/AGENTS.md`) binds to this instead of mutating the
/// builder directly; it may still read the Domain DTOs (<see cref="NodeDef"/>
/// etc.) this view-model exposes. See `docs/BUILD_MODE.md`.
/// </summary>
public sealed class BuildViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Where joints may go, in canvas units: about six screens wide at 1×,
    /// centred on the origin. Placing and moving keep a joint's ring inside;
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
    private BuildTool _activeTool = BuildTool.Move;
    private string? _statusMessage;
    private bool _moveOnly;
    private readonly HashSet<int> _selectedNodeIds = [];
    private string _creationName = NewCreationWorkflow.UntitledName;
    private int? _trainingGeneration;
    private double? _latestDistance;
    private int? _selectedBeamId;
    private int? _selectedSensorId;
    private int? _selectedPistonId;
    private CanvasNote? _placementNote;

    /// <summary>Why a sensor dropped on a joint was not placed.</summary>
    public const string SensorsGoOnABeamReason = "Sensors go on a beam";

    public BuildViewModel(CreatureBuilder? builder = null)
    {
        _builder = builder ?? new CreatureBuilder();
    }

    public void Load(CreatureDef creature, bool moveOnly = false, string? creationName = null, TrainingStateDef? training = null)
    {
        ArgumentNullException.ThrowIfNull(creature);
        _builder = new CreatureBuilder(creature);
        _selectedNodeIds.Clear();
        _selectedBeamId = null;
        _selectedSensorId = null;
        _selectedPistonId = null;
        _creationName = string.IsNullOrWhiteSpace(creationName) ? NewCreationWorkflow.UntitledName : creationName;
        _trainingGeneration = training?.Generation;
        _latestDistance = training?.Latest.Distance;
        _moveOnly = moveOnly;
        ActiveTool = BuildTool.Move;
        StatusMessage = null;
        PlacementNote = null;
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Opens a saved Creation. It is fully editable until it is locked (<see cref="CreationLock"/>);
    /// a locked one only moves its nodes until <see cref="Unlock"/>.
    /// </summary>
    public void LoadCreation(CreationDef creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        Load(creation.Creature, CreationLock.IsLocked(creation), creation.Name, creation.Training);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised whenever the placed anatomy (nodes, beams, or sensors) changes, so the UI can redraw.</summary>
    public event EventHandler? AnatomyChanged;

    /// <summary>True for a locked Creation: parts are fixed, and only nodes move.</summary>
    public bool IsMoveOnly => _moveOnly;

    /// <summary>
    /// Unlocks a locked Creation for this Build visit (#371), so its body can change. The training
    /// is kept: each saved edit refits the brain to the new body (#516). Nothing about the unlock is
    /// saved, so the Creation is locked again the next time Build opens it.
    /// </summary>
    public void Unlock()
    {
        if (!_moveOnly)
        {
            return;
        }

        _moveOnly = false;
        OnPropertyChanged(nameof(IsMoveOnly));
    }

    public string CreationName => _creationName;

    public int? TrainingGeneration => _trainingGeneration;

    /// <summary>How far the latest finished generation's best run got (#479); it can go down.</summary>
    public double? LatestDistance => _latestDistance;

    public int SelectedNodeCount => _selectedNodeIds.Count;

    public int SelectedBeamCount => _selectedBeamId is null ? 0 : 1;

    public int SelectedSensorCount => _selectedSensorId is null ? 0 : 1;

    public int SelectedPistonCount => _selectedPistonId is null ? 0 : 1;

    public int SelectedPartCount => SelectedNodeCount + SelectedBeamCount + SelectedSensorCount + SelectedPistonCount;

    public int? SingleSelectedNodeId => _selectedNodeIds.Count == 1
        ? _selectedNodeIds.First()
        : null;

    public int? SingleSelectedBeamId => SelectedPartCount == 1 ? _selectedBeamId : null;

    public int? SingleSelectedSensorId => SelectedPartCount == 1 ? _selectedSensorId : null;

    public int? SingleSelectedPistonId => SelectedPartCount == 1 ? _selectedPistonId : null;

    /// <summary>The Camera whose aim can be turned now (#594): the single selection, even on a locked Creation, since aim does not change the model (#638).</summary>
    public int? AimableCameraId => SingleSelectedSensorId is { } sensorId
        && _builder.Sensors[_builder.SensorIndexOf(sensorId)].Kind == SensorKind.Camera
            ? sensorId
            : null;

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

    public BuildTool ActiveTool
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

    /// <summary>
    /// Picks a link from the tray (#451): its tool stays active for several placements, and picking
    /// it again puts the Move tool back. A locked Creation places nothing, so it keeps Move.
    /// </summary>
    public void PickPart(BuildPart part)
    {
        if (_moveOnly || !PartTray.IsAvailable(part) || PartTray.ToolOf(part) is not { } tool)
        {
            return;
        }

        ActiveTool = ActiveTool == tool ? BuildTool.Move : tool;
    }

    /// <summary>Why a Piston drag that ended away from a joint placed nothing: a Piston never makes a joint.</summary>
    public void PistonDropMissed() => StatusMessage = "Drop it on another node.";

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

    public IReadOnlyList<SensorDef> Sensors => _builder.Sensors;

    public IReadOnlyList<PistonDef> Pistons => _builder.Pistons;

    /// <summary>
    /// The messages Build shows in the drawing, each beside the part it is about: why the last
    /// dropped part was refused (#376), then each beam or Piston too short to train (#593). Listed most
    /// important first: notes that would overlap stack, the first listed nearest its part.
    /// </summary>
    public IReadOnlyList<CanvasNote> CanvasNotes()
    {
        var notes = new List<CanvasNote>();
        if (_placementNote is { } placement && Exists(placement.Target))
        {
            notes.Add(placement);
        }

        foreach (var beam in Beams)
        {
            var a = Nodes[NodeIndexOf(beam.NodeA)];
            var b = Nodes[NodeIndexOf(beam.NodeB)];
            if (a.Position != b.Position && CreatureReadiness.IsTooShort(a, b))
            {
                notes.Add(new CanvasNote(
                    CanvasNoteKind.Danger,
                    new CreatureElementSelection(CreatureElementKind.Beam, beam.Id),
                    "Too short"));
            }
        }

        foreach (var piston in Pistons)
        {
            var a = Nodes[NodeIndexOf(piston.NodeA)];
            var b = Nodes[NodeIndexOf(piston.NodeB)];
            if (a.Position != b.Position && CreatureReadiness.IsTooShort(a, b))
            {
                notes.Add(new CanvasNote(
                    CanvasNoteKind.Danger,
                    new CreatureElementSelection(CreatureElementKind.Piston, piston.Id),
                    "Too short"));
            }
        }

        return notes;
    }

    /// <summary>Why the last part dropped from the tray was refused, shown at the part it was dropped on; null when there is none.</summary>
    public CanvasNote? PlacementNote
    {
        get => _placementNote;
        private set
        {
            if (_placementNote == value)
            {
                return;
            }

            _placementNote = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Hides <see cref="PlacementNote"/>: the canvas does so on the next touch, or once it has been read.</summary>
    public void DismissPlacementNote() => PlacementNote = null;

    /// <summary>
    /// Whether a tray part dropped on <paramref name="target"/> would be placed there (#376); if
    /// not, <paramref name="reason"/> says why. Sensors go on a beam that has none yet.
    /// </summary>
    public bool CanPlacePart(BuildPart part, CreatureElementSelection target, out string reason)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (_moveOnly)
        {
            reason = "Edit mode only allows moving existing nodes.";
            return false;
        }

        if (!PartTray.IsAvailable(part) || PartTray.SensorKindOf(part) is null)
        {
            reason = PartTray.ComingLater;
            return false;
        }

        if (target.Kind != CreatureElementKind.Beam)
        {
            reason = SensorsGoOnABeamReason;
            return false;
        }

        _builder.BeamIndexOf(target.Id);
        if (_builder.Sensors.Any(sensor => sensor.BeamId == target.Id))
        {
            reason = CreatureBuilder.OneSensorPerBeamReason;
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Places a tray part dropped on <paramref name="target"/> and returns its new id (#376). A drop
    /// on empty canvas (<paramref name="target"/> null) changes nothing and says nothing; a refused
    /// drop changes nothing and shows why as <see cref="PlacementNote"/> at that part.
    /// </summary>
    public int? PlacePart(BuildPart part, CreatureElementSelection? target)
    {
        PlacementNote = null;
        if (target is null)
        {
            return null;
        }

        if (!CanPlacePart(part, target, out var reason))
        {
            StatusMessage = reason;
            if (!_moveOnly)
            {
                PlacementNote = new CanvasNote(CanvasNoteKind.Danger, target, reason);
            }

            return null;
        }

        _builder.AddSensor(target.Id, PartTray.SensorKindOf(part)!.Value, out var sensorId, out _);
        StatusMessage = $"Placed {SensorName(PartTray.SensorKindOf(part)!.Value)} on beam {_builder.BeamIndexOf(target.Id) + 1}.";
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
        return sensorId;
    }

    private bool Exists(CreatureElementSelection element) => element.Kind switch
    {
        CreatureElementKind.Node => _builder.Nodes.Any(node => node.Id == element.Id),
        CreatureElementKind.Beam => _builder.Beams.Any(beam => beam.Id == element.Id),
        CreatureElementKind.Piston => _builder.Pistons.Any(piston => piston.Id == element.Id),
        _ => _builder.Sensors.Any(sensor => sensor.Id == element.Id),
    };

    public IReadOnlyCollection<int> SelectedNodeIds => _selectedNodeIds;

    /// <summary>Places a new node, moved inside <see cref="BuildArea"/>, and returns its id.</summary>
    public int PlaceNode(Vector2D position)
    {
        if (_moveOnly)
        {
            throw new InvalidOperationException("Edit mode can only move existing nodes.");
        }

        var id = _builder.AddNode(BuildArea.Clamp(position, NodeDef.PlainJointRadius));
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
        return id;
    }

    /// <summary>Moves an already-placed node to a new position, as far as <see cref="BuildArea"/> reaches.</summary>
    public void MoveNode(int nodeId, Vector2D position)
    {
        _builder.MoveNode(nodeId, BuildArea.Clamp(position, NodeById(nodeId).Radius));
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleSelectedNode(int nodeId)
    {
        _builder.NodeIndexOf(nodeId);

        if (!_selectedNodeIds.Add(nodeId))
        {
            _selectedNodeIds.Remove(nodeId);
        }

        _selectedBeamId = null;

        _selectedSensorId = null;

        _selectedPistonId = null;
        StatusMessage = _selectedNodeIds.Count == 0
            ? "Selection cleared."
            : $"{_selectedNodeIds.Count} selected. Drag one selected node to move them together.";
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectBeam(int beamId)
    {
        var beamIndex = _builder.BeamIndexOf(beamId);

        _selectedNodeIds.Clear();
        _selectedBeamId = beamId;
        _selectedSensorId = null;
        _selectedPistonId = null;
        StatusMessage = $"Beam {beamIndex + 1} selected.";
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectSensor(int sensorId)
    {
        var sensor = _builder.Sensors[_builder.SensorIndexOf(sensorId)];

        _selectedNodeIds.Clear();
        _selectedBeamId = null;
        _selectedSensorId = sensorId;
        _selectedPistonId = null;
        StatusMessage = $"{SensorName(sensor.Kind)} selected.";
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectPiston(int pistonId)
    {
        _builder.PistonIndexOf(pistonId);

        _selectedNodeIds.Clear();
        _selectedBeamId = null;
        _selectedSensorId = null;
        _selectedPistonId = pistonId;
        StatusMessage = $"{PartDisplayName(pistonId)} selected.";
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Changes a Piston's Strength, stroke and max speed (#451). Like a Camera's aim, these tune
    /// the body without changing the brain's ports, so a locked Creation can change them too.
    /// </summary>
    public void SetPistonSettings(int pistonId, double strength, double stroke, double maxSpeed)
    {
        var piston = _builder.Pistons[_builder.PistonIndexOf(pistonId)];
        if (piston.Strength == strength && piston.Stroke == stroke && piston.MaxSpeed == maxSpeed)
        {
            return;
        }

        _builder.SetPistonSettings(pistonId, strength, stroke, maxSpeed);
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Turns a Camera to <paramref name="aim"/>, relative to its beam (see <see cref="SensorDef.Aim"/>).</summary>
    public void SetCameraAim(int sensorId, double aim)
    {
        if (!double.IsFinite(aim))
        {
            throw new ArgumentOutOfRangeException(nameof(aim), "Aim must be finite.");
        }

        if (_builder.Sensors[_builder.SensorIndexOf(sensorId)].Aim == aim)
        {
            return;
        }

        _builder.SetCameraAim(sensorId, aim);
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The sensor whose picture (<see cref="SensorPicture"/>) is under <paramref name="position"/>, if any.</summary>
    public bool TryFindSensorAt(Vector2D position, out int sensorId)
    {
        foreach (var sensor in _builder.Sensors)
        {
            var beam = _builder.Beams[_builder.BeamIndexOf(sensor.BeamId)];
            if (SensorPicture.Contains(sensor.Kind, position, NodeById(beam.NodeA).Position, NodeById(beam.NodeB).Position))
            {
                sensorId = sensor.Id;
                return true;
            }
        }

        sensorId = -1;
        return false;
    }

    public static string SensorName(SensorKind kind) => PartNames.SensorKind(kind);

    /// <summary>The name a part shows: its own name if it has one, else <see cref="DefaultPartName"/>.</summary>
    public string PartDisplayName(int partId) => PartNames.Display(_builder.Nodes, _builder.Beams, _builder.Sensors, _builder.Pistons, partId);

    /// <summary>The name a part shows until it is renamed: "Node 2", "Beam 1", "Piston 1" or its sensor kind.</summary>
    public string DefaultPartName(int partId) => PartNames.Default(_builder.Nodes, _builder.Beams, _builder.Sensors, _builder.Pistons, partId);

    /// <summary>
    /// Renames a part by id, so an edit lands on the part it started on even if the selection
    /// moved meanwhile; a part deleted since is ignored. A blank name, or the part's default name,
    /// clears its own name so it shows the default again. Names are labels only (#220), so a
    /// locked Creation can be renamed too.
    /// </summary>
    public void RenamePart(int partId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_builder.Nodes.Any(node => node.Id == partId)
            && !_builder.Beams.Any(beam => beam.Id == partId)
            && !_builder.Sensors.Any(sensor => sensor.Id == partId)
            && !_builder.Pistons.Any(piston => piston.Id == partId))
        {
            return;
        }

        var trimmed = name.Trim();
        var newName = trimmed.Length == 0 || trimmed == DefaultPartName(partId) ? null : trimmed;
        if (newName == PartName(partId))
        {
            return;
        }

        _builder.Rename(partId, newName);
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    private string? PartName(int partId) => PartNames.Own(_builder.Nodes, _builder.Beams, _builder.Sensors, _builder.Pistons, partId);

    public void ClearSelection()
    {
        if (SelectedPartCount == 0)
        {
            return;
        }

        _selectedNodeIds.Clear();
        _selectedBeamId = null;
        _selectedSensorId = null;
        _selectedPistonId = null;
        StatusMessage = "Selection cleared.";
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReplaceSelection(IEnumerable<int> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        _selectedNodeIds.Clear();
        _selectedBeamId = null;
        _selectedSensorId = null;
        _selectedPistonId = null;
        foreach (var nodeId in nodeIds)
        {
            if (_builder.Nodes.Any(node => node.Id == nodeId))
            {
                _selectedNodeIds.Add(nodeId);
            }
        }

        StatusMessage = _selectedNodeIds.Count == 0
            ? "Selection cleared."
            : $"{_selectedNodeIds.Count} selected. Drag one selected node to move them together.";
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The selected joints' positions and pivot, for a Select drag to transform.</summary>
    public SelectionSnapshot SnapshotSelection()
    {
        if (_selectedNodeIds.Count == 0)
        {
            throw new InvalidOperationException("Nothing is selected.");
        }

        var positions = _selectedNodeIds.ToDictionary(id => id, id => NodeById(id).Position);
        var pivot = new Vector2D(
            (positions.Values.Min(p => p.X) + positions.Values.Max(p => p.X)) / 2,
            (positions.Values.Min(p => p.Y) + positions.Values.Max(p => p.Y)) / 2);
        return new SelectionSnapshot(positions, pivot);
    }

    /// <summary>Moves the snapshot's joints by <paramref name="delta"/>, shortened so the whole group stays inside <see cref="BuildArea"/>.</summary>
    public void TranslateSelection(SelectionSnapshot start, Vector2D delta)
    {
        ArgumentNullException.ThrowIfNull(start);
        foreach (var (id, position) in start.Positions)
        {
            var moved = new Vector2D(position.X + delta.X, position.Y + delta.Y);
            var allowed = BuildArea.Clamp(moved, NodeById(id).Radius);
            delta = new Vector2D(delta.X + allowed.X - moved.X, delta.Y + allowed.Y - moved.Y);
        }

        // Clamping each joint too absorbs the rounding in the shortened delta.
        PlaceSelection(start, (id, position) =>
            BuildArea.Clamp(new Vector2D(position.X + delta.X, position.Y + delta.Y), NodeById(id).Radius));
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
        if (keepInBuildArea && placed.Any(entry => BuildArea.Clamp(entry.Value, NodeById(entry.Key).Radius) != entry.Value))
        {
            return;
        }

        foreach (var (id, position) in placed)
        {
            _builder.MoveNode(id, position);
        }

        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Finds the closest placed node whose centre is within
    /// <paramref name="maxDistance"/> of <paramref name="position"/>, or whose
    /// ring contains it, if any. Used to hit-test nodes.
    /// </summary>
    public bool TryFindNodeNear(Vector2D position, double maxDistance, out int nodeId)
    {
        nodeId = -1;
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
                nodeId = node.Id;
            }
        }

        return nodeId >= 0;
    }

    /// <summary>Whether <see cref="ConnectBeam"/> would join this pair: unlocked, and the builder accepts the beam.</summary>
    public bool CanConnect(int nodeIdA, int nodeIdB) => !_moveOnly && _builder.CanAddBeam(nodeIdA, nodeIdB);

    /// <summary>
    /// Joins two existing nodes with a beam. Rejected attempts (locked
    /// Creation, self-connect, duplicate beam) surface via
    /// <see cref="StatusMessage"/> instead of throwing.
    /// </summary>
    public bool ConnectBeam(int nodeIdA, int nodeIdB)
    {
        if (_moveOnly)
        {
            StatusMessage = "Edit mode only allows moving existing nodes.";
            return false;
        }

        try
        {
            _builder.AddBeam(nodeIdA, nodeIdB);
        }
        catch (ArgumentException exception)
        {
            StatusMessage = exception.Message;
            return false;
        }

        StatusMessage = $"Connected node {nodeIdA} to node {nodeIdB}.";
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Whether <see cref="ConnectPiston"/> would place a Piston between this pair; if not, <paramref name="reason"/> says why.</summary>
    public bool CanConnectPiston(int nodeIdA, int nodeIdB, out string reason)
    {
        if (_moveOnly)
        {
            reason = "Edit mode only allows moving existing nodes.";
            return false;
        }

        return _builder.CanAddPiston(nodeIdA, nodeIdB, out reason);
    }

    /// <summary>
    /// Places a Piston between two nodes (#451) and returns its id. A refused pair changes
    /// nothing and shows why as <see cref="PlacementNote"/> at <paramref name="nodeIdB"/>, the
    /// joint the drag ended on.
    /// </summary>
    public int? ConnectPiston(int nodeIdA, int nodeIdB)
    {
        PlacementNote = null;
        if (!CanConnectPiston(nodeIdA, nodeIdB, out var reason))
        {
            StatusMessage = reason;
            if (!_moveOnly && nodeIdA != nodeIdB && _builder.Nodes.Any(node => node.Id == nodeIdB))
            {
                PlacementNote = new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, nodeIdB), reason);
            }

            return null;
        }

        var pistonId = _builder.AddPiston(nodeIdA, nodeIdB);
        StatusMessage = $"Placed {PartDisplayName(pistonId)}.";
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
        return pistonId;
    }

    /// <summary>
    /// Adds a node at the point on beam <paramref name="beamId"/> closest to
    /// <paramref name="position"/> and replaces the beam with two beams through
    /// it, as one change. Returns the new node's id, or null when the
    /// Creation is locked or the closest point is an end of the beam (or the
    /// beam has no length), where a split would stack two nodes.
    /// </summary>
    public int? SplitBeam(int beamId, Vector2D position)
    {
        var beamIndex = _builder.BeamIndexOf(beamId);

        if (_moveOnly)
        {
            StatusMessage = "Edit mode only allows moving existing nodes.";
            return null;
        }

        var beam = _builder.Beams[beamIndex];
        var start = NodeById(beam.NodeA).Position;
        var end = NodeById(beam.NodeB).Position;
        var t = ClosestPointParameter(position, start, end);
        if (t <= 0 || t >= 1)
        {
            return null;
        }

        var splitPoint = new Vector2D(start.X + (t * (end.X - start.X)), start.Y + (t * (end.Y - start.Y)));
        var nodeId = _builder.AddNode(splitPoint);
        _builder.SplitBeamAtNode(beamId, nodeId);
        _selectedNodeIds.Clear();
        _selectedBeamId = null;
        _selectedSensorId = null;
        _selectedPistonId = null;
        StatusMessage = "Split the beam with a new joint.";
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
        return nodeId;
    }

    /// <summary>
    /// Finds the closest beam within <paramref name="maxDistance"/> of
    /// <paramref name="position"/> (measured to the beam's line segment), if
    /// any. Used to hit-test beams, since a beam has no single point like a
    /// node does.
    /// </summary>
    public bool TryFindBeamNear(Vector2D position, double maxDistance, out int beamId)
    {
        beamId = -1;
        var bestDistanceSquared = maxDistance * maxDistance;

        for (var i = 0; i < _builder.Beams.Count; i++)
        {
            var beam = _builder.Beams[i];
            var distanceSquared = DistanceSquaredToSegment(position, NodeById(beam.NodeA).Position, NodeById(beam.NodeB).Position);
            if (distanceSquared <= bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                beamId = beam.Id;
            }
        }

        return beamId >= 0;
    }

    /// <summary>The closest Piston within <paramref name="maxDistance"/> of <paramref name="position"/>, measured to the line between its nodes.</summary>
    public bool TryFindPistonNear(Vector2D position, double maxDistance, out int pistonId)
    {
        pistonId = -1;
        var bestDistanceSquared = maxDistance * maxDistance;
        foreach (var piston in _builder.Pistons)
        {
            var distanceSquared = DistanceSquaredToSegment(position, NodeById(piston.NodeA).Position, NodeById(piston.NodeB).Position);
            if (distanceSquared <= bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                pistonId = piston.Id;
            }
        }

        return pistonId >= 0;
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

        if (_selectedBeamId is { } beamId)
        {
            _builder.RemoveBeam(beamId);
        }

        if (_selectedSensorId is { } sensorId)
        {
            _builder.RemoveSensor(sensorId);
        }

        if (_selectedPistonId is { } pistonId)
        {
            _builder.RemovePiston(pistonId);
        }

        foreach (var nodeId in _selectedNodeIds)
        {
            _builder.RemoveNode(nodeId);
        }

        _selectedNodeIds.Clear();
        _selectedBeamId = null;
        _selectedSensorId = null;
        _selectedPistonId = null;
        StatusMessage = "Deleted selected parts.";
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Checks whether the current anatomy is valid enough to leave
    /// Build mode. An empty anatomy (nothing placed yet) is always
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
    /// without a moving part, is refused quietly because Build already shows it is not ready.
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

    public int NodeIndexOf(int nodeId) => _builder.NodeIndexOf(nodeId);

    public int BeamIndexOf(int beamId) => _builder.BeamIndexOf(beamId);

    public int PistonIndexOf(int pistonId) => _builder.PistonIndexOf(pistonId);

    private NodeDef NodeById(int nodeId) => _builder.Nodes[_builder.NodeIndexOf(nodeId)];

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedNodeCount));
        OnPropertyChanged(nameof(SelectedBeamCount));
        OnPropertyChanged(nameof(SelectedPartCount));
        OnPropertyChanged(nameof(SingleSelectedNodeId));
        OnPropertyChanged(nameof(SingleSelectedBeamId));
        OnPropertyChanged(nameof(SelectedSensorCount));
        OnPropertyChanged(nameof(SingleSelectedSensorId));
        OnPropertyChanged(nameof(SelectedPistonCount));
        OnPropertyChanged(nameof(SingleSelectedPistonId));
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
