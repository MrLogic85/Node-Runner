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
    Parts,
    Beam,
    Joint,
    Select,
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
    private BuildTool _activeTool = BuildTool.Parts;
    private BuildLink _pickedLink = BuildLink.Beam;
    private bool _moveOnly;
    private readonly HashSet<int> _selectedNodeIds = [];
    private string _creationName = NewCreationWorkflow.UntitledName;
    private int? _trainingGeneration;
    private double? _latestDistance;
    private readonly HashSet<int> _selectedBeamIds = [];
    private readonly HashSet<int> _selectedSensorIds = [];
    private readonly HashSet<int> _selectedPistonIds = [];
    private CanvasNote? _placementNote;
    private readonly BuildHistory _history;
    private BrainDef? _openedBrain;
    private bool _shownCanUndo;
    private bool _shownCanRedo;

    /// <summary>Why a sensor dropped on a joint was not placed.</summary>
    public const string SensorsGoOnABeamReason = "Sensors go on a beam";

    public BuildViewModel(CreatureBuilder? builder = null)
    {
        _builder = builder ?? new CreatureBuilder();
        _history = new BuildHistory(Snapshot);
        _history.Changed += (_, _) => NotifyHistoryChanged();
    }

    public void Load(CreatureDef creature, bool moveOnly = false, string? creationName = null, TrainingStateDef? training = null)
    {
        ArgumentNullException.ThrowIfNull(creature);
        _builder = new CreatureBuilder(creature);
        ClearSelectionSets();
        _creationName = string.IsNullOrWhiteSpace(creationName) ? NewCreationWorkflow.UntitledName : creationName;
        _trainingGeneration = training?.Generation;
        _latestDistance = training?.Latest.ShownDistance;
        _openedBrain = training?.Brain;
        _moveOnly = moveOnly;
        _history.Clear();
        ActiveTool = BuildTool.Parts;
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

    /// <summary>
    /// The trained brain as it was when Build opened, or null for an untrained Creation. Saves refit
    /// this brain rather than the last saved one, so an undone delete gets its trained weights back (#689).
    /// </summary>
    public BrainDef? OpenedBrain => _openedBrain;

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    /// <summary>
    /// Opens an edit for <paramref name="owner"/>, such as a drag, that lasts until its
    /// <see cref="EndEdit"/>: all its changes are one undo step (#689). An edit another owner left open
    /// ends first, so one source never merges into, closes or drops another's edit.
    /// </summary>
    public void BeginEdit(object owner) => _history.Begin(owner);

    /// <summary>Ends <paramref name="owner"/>'s open edit: one undo step if it changed the body.</summary>
    public void EndEdit(object owner) => _history.End(owner);

    /// <summary>Drops <paramref name="owner"/>'s open edit without a step, once it has put the body back.</summary>
    public void CancelEdit(object owner) => _history.Cancel(owner);

    /// <summary>Puts the body back as it was before the last step.</summary>
    public void Undo()
    {
        if (_history.Undo() is { } body)
        {
            Restore(body);
        }
    }

    /// <summary>Applies the last undone step again.</summary>
    public void Redo()
    {
        if (_history.Redo() is { } body)
        {
            Restore(body);
        }
    }

    // Part ids are never reused (#220): the restored body keeps the highest NextPartId this visit reached.
    private void Restore(CreatureDef body)
    {
        var nextPartId = Math.Max(body.NextPartId, _builder.NextPartId);
        _builder = new CreatureBuilder(new CreatureDef(body.Nodes, body.Beams, body.Sensors, body.Pistons, nextPartId));
        foreach (var kind in Enum.GetValues<CreatureElementKind>())
        {
            SelectedSet(kind).RemoveWhere(id => !Exists(new CreatureElementSelection(kind, id)));
        }

        PlacementNote = null;
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyHistoryChanged()
    {
        if (_shownCanUndo != CanUndo)
        {
            _shownCanUndo = CanUndo;
            OnPropertyChanged(nameof(CanUndo));
        }

        if (_shownCanRedo != CanRedo)
        {
            _shownCanRedo = CanRedo;
            OnPropertyChanged(nameof(CanRedo));
        }
    }

    public int? TrainingGeneration => _trainingGeneration;

    /// <summary>How far the latest finished generation's best run got (#479); it can go down.</summary>
    public double? LatestDistance => _latestDistance;

    public int SelectedNodeCount => _selectedNodeIds.Count;

    public int SelectedBeamCount => _selectedBeamIds.Count;

    public int SelectedSensorCount => _selectedSensorIds.Count;

    public int SelectedPistonCount => _selectedPistonIds.Count;

    public int SelectedPartCount => SelectedNodeCount + SelectedBeamCount + SelectedSensorCount + SelectedPistonCount;

    public int? SingleSelectedNodeId => Single(_selectedNodeIds);

    public int? SingleSelectedBeamId => Single(_selectedBeamIds);

    public int? SingleSelectedSensorId => Single(_selectedSensorIds);

    public int? SingleSelectedPistonId => Single(_selectedPistonIds);

    /// <summary>A copy of everything selected (#704).</summary>
    public PartSet Selection => new(
        _selectedNodeIds.ToHashSet(),
        _selectedBeamIds.ToHashSet(),
        _selectedSensorIds.ToHashSet(),
        _selectedPistonIds.ToHashSet());

    /// <summary>The Camera whose aim handle shows (#594): Aim can be set, so it is the one selected part.</summary>
    public int? AimableCameraId => CanEdit(PartParameterId.Aim) ? SingleSelectedSensorId : null;

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
            if (value == BuildTool.Beam)
            {
                SetPickedLink(BuildLink.Beam);
            }

            OnPropertyChanged();
        }
    }

    /// <summary>The link the Beams tool draws when a drag starts from an unselected joint (#705).</summary>
    public BuildLink PickedLink => _pickedLink;

    /// <summary>Picks the link the Beams tool draws. Locked and future links do nothing.</summary>
    public void PickLink(BuildLink link)
    {
        if (_moveOnly || _activeTool != BuildTool.Beam || !BuildLinkList.IsAvailable(link))
        {
            return;
        }

        SetPickedLink(link);
    }

    private void SetPickedLink(BuildLink link)
    {
        if (_pickedLink == link)
        {
            return;
        }

        _pickedLink = link;
        OnPropertyChanged(nameof(PickedLink));
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
            // Reasons are still finished English until #758 makes them UiText.
            reason = PartTray.ComingLater.Message;
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
            if (!_moveOnly)
            {
                PlacementNote = new CanvasNote(CanvasNoteKind.Danger, target, reason);
            }

            return null;
        }

        var sensorId = _history.Change(() =>
        {
            _builder.AddSensor(target.Id, PartTray.SensorKindOf(part)!.Value, out var id, out _);
            return id;
        });
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

        var id = _history.Change(() => _builder.AddNode(BuildArea.Clamp(position, NodeDef.PlainJointRadius)));
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
        return id;
    }

    /// <summary>Moves an already-placed node to a new position, as far as <see cref="BuildArea"/> reaches.</summary>
    public void MoveNode(int nodeId, Vector2D position)
    {
        _history.Change(() => _builder.MoveNode(nodeId, BuildArea.Clamp(position, NodeById(nodeId).Radius)));
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds the part to the selection, or removes it if it is already selected (#704).</summary>
    public void ToggleSelected(CreatureElementSelection element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!Exists(element))
        {
            throw new ArgumentException($"No {element.Kind} {element.Id}.", nameof(element));
        }

        var set = SelectedSet(element.Kind);
        if (!set.Add(element.Id))
        {
            set.Remove(element.Id);
        }

        SelectionChanged();
    }

    public void SelectBeam(int beamId) => SelectOnly(CreatureElementKind.Beam, beamId);

    public void SelectSensor(int sensorId) => SelectOnly(CreatureElementKind.Sensor, sensorId);

    public void SelectPiston(int pistonId) => SelectOnly(CreatureElementKind.Piston, pistonId);

    private void SelectOnly(CreatureElementKind kind, int id) => ReplaceSelection(PartSetOf(kind, id));

    /// <summary>
    /// The settings the selection can change now (#704): one part's own, or those every selected part
    /// has and can share. Like a Camera's aim (#638), they tune the body without changing the brain's
    /// ports, so a locked Creation can change them too.
    /// </summary>
    public IReadOnlyList<PartParameterId> EditableParameters
    {
        get
        {
            var parts = SelectedPartIds().ToList();
            if (parts.Count == 0)
            {
                return [];
            }

            var shared = parts.Skip(1).Aggregate(
                _builder.ParametersOf(parts[0]).AsEnumerable(),
                (common, part) => common.Intersect(_builder.ParametersOf(part)));
            return [.. parts.Count == 1 ? shared : shared.Where(id => PartParameters.Of(id).MultiEditable)];
        }
    }

    public bool CanEdit(PartParameterId parameter) => EditableParameters.Contains(parameter);

    /// <summary>Each selected part's <paramref name="parameter"/>, in world units.</summary>
    public IReadOnlyList<double> SelectedValuesOf(PartParameterId parameter)
    {
        RequireEditable(parameter);
        return [.. SelectedPartIds().Select(part => _builder.ParameterValue(part, parameter))];
    }

    /// <summary>Sets <paramref name="parameter"/> to <paramref name="value"/>, in world units, on every selected part (#704).</summary>
    public void SetParameter(PartParameterId parameter, double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "A setting must be finite.");
        }

        RequireEditable(parameter);
        var changed = _history.Change(() =>
        {
            var any = false;
            foreach (var part in SelectedPartIds().Where(part => _builder.ParameterValue(part, parameter) != value))
            {
                _builder.SetParameter(part, parameter, value);
                any = true;
            }

            return any;
        });

        if (changed)
        {
            AnatomyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RequireEditable(PartParameterId parameter)
    {
        if (!CanEdit(parameter))
        {
            throw new InvalidOperationException($"The selection cannot change {parameter}.");
        }
    }

    private IEnumerable<int> SelectedPartIds() =>
        _selectedNodeIds.Concat(_selectedBeamIds).Concat(_selectedSensorIds).Concat(_selectedPistonIds);

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

    /// <summary>The name a part shows: its own name if it has one, else <see cref="DefaultPartName"/>.</summary>
    public UiText PartDisplayName(int partId) => PartNames.Display(_builder.Nodes, _builder.Beams, _builder.Sensors, _builder.Pistons, partId);

    /// <summary>The name a part shows until it is renamed: "Node 2", "Beam 1", "Piston 1" or its sensor kind.</summary>
    public UiText DefaultPartName(int partId) => PartNames.Default(_builder.Nodes, _builder.Beams, _builder.Sensors, _builder.Pistons, partId);

    /// <summary>
    /// Renames a part by id, so an edit lands on the part it started on even if the selection
    /// moved meanwhile; a part deleted since is ignored. A blank name, or the
    /// <paramref name="shownDefault"/> left as it is, clears its own name so it shows the default
    /// again. Only the screen knows the default in the player's language, so it passes the one it
    /// showed. Names are labels only (#220), so a locked Creation can be renamed too.
    /// </summary>
    public void RenamePart(int partId, string name, string? shownDefault)
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
        var newName = trimmed.Length == 0 || trimmed == shownDefault ? null : trimmed;
        if (newName == PartName(partId))
        {
            return;
        }

        _history.Change(() => _builder.Rename(partId, newName));
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    private string? PartName(int partId) => PartNames.Own(_builder.Nodes, _builder.Beams, _builder.Sensors, _builder.Pistons, partId);

    public void ClearSelection()
    {
        if (SelectedPartCount == 0)
        {
            return;
        }

        ClearSelectionSets();
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReplaceSelection(IEnumerable<int> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        ReplaceSelection(PartSet.None with { Nodes = nodeIds.ToHashSet() });
    }

    /// <summary>Selects exactly the <paramref name="parts"/> that still exist.</summary>
    public void ReplaceSelection(PartSet parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ClearSelectionSets();
        foreach (var kind in Enum.GetValues<CreatureElementKind>())
        {
            SelectedSet(kind).UnionWith(parts.SetOf(kind).Where(id => Exists(new CreatureElementSelection(kind, id))));
        }

        SelectionChanged();
    }

    private static PartSet PartSetOf(CreatureElementKind kind, int id)
    {
        var one = new HashSet<int> { id };
        return kind switch
        {
            CreatureElementKind.Node => PartSet.None with { Nodes = one },
            CreatureElementKind.Beam => PartSet.None with { Beams = one },
            CreatureElementKind.Sensor => PartSet.None with { Sensors = one },
            _ => PartSet.None with { Pistons = one },
        };
    }

    private HashSet<int> SelectedSet(CreatureElementKind kind) => kind switch
    {
        CreatureElementKind.Node => _selectedNodeIds,
        CreatureElementKind.Beam => _selectedBeamIds,
        CreatureElementKind.Sensor => _selectedSensorIds,
        CreatureElementKind.Piston => _selectedPistonIds,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private void ClearSelectionSets()
    {
        _selectedNodeIds.Clear();
        _selectedBeamIds.Clear();
        _selectedSensorIds.Clear();
        _selectedPistonIds.Clear();
    }

    private int? Single(HashSet<int> set) => SelectedPartCount == 1 && set.Count == 1 ? set.First() : null;

    private void SelectionChanged()
    {
        NotifySelectionChanged();
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The selected joints' positions and pivot, for a Select drag to transform: the given
    /// <paramref name="pivot"/>, or else the middle of the joints' centres.
    /// </summary>
    public SelectionSnapshot SnapshotSelection(Vector2D? pivot = null)
    {
        if (_selectedNodeIds.Count == 0)
        {
            throw new InvalidOperationException("Nothing is selected.");
        }

        var positions = _selectedNodeIds.ToDictionary(id => id, id => NodeById(id).Position);
        pivot ??= new Vector2D(
            (positions.Values.Min(p => p.X) + positions.Values.Max(p => p.X)) / 2,
            (positions.Values.Min(p => p.Y) + positions.Values.Max(p => p.Y)) / 2);
        return new SelectionSnapshot(positions, pivot.Value);
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

    /// <summary>
    /// Turns the snapshot's joints <paramref name="radians"/> about its pivot; a turn that would
    /// leave <see cref="BuildArea"/> is ignored. Returns whether the joints turned.
    /// </summary>
    public bool RotateSelection(SelectionSnapshot start, double radians)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (!double.IsFinite(radians))
        {
            throw new ArgumentOutOfRangeException(nameof(radians));
        }

        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var pivot = start.Pivot;
        return PlaceSelection(start, (_, position) =>
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

    private bool PlaceSelection(SelectionSnapshot start, Func<int, Vector2D, Vector2D> place, bool keepInBuildArea = true)
    {
        var placed = start.Positions.ToDictionary(entry => entry.Key, entry => place(entry.Key, entry.Value));
        if (keepInBuildArea && placed.Any(entry => BuildArea.Clamp(entry.Value, NodeById(entry.Key).Radius) != entry.Value))
        {
            return false;
        }

        _history.Change(() =>
        {
            foreach (var (id, position) in placed)
            {
                _builder.MoveNode(id, position);
            }
        });
        AnatomyChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Finds the closest placed node whose ring, grown by <paramref name="margin"/>,
    /// contains <paramref name="position"/>, if any. Used to hit-test nodes.
    /// </summary>
    public bool TryFindNodeNear(Vector2D position, double margin, out int nodeId)
    {
        nodeId = -1;
        var bestDistanceSquared = double.PositiveInfinity;

        for (var i = 0; i < _builder.Nodes.Count; i++)
        {
            var node = _builder.Nodes[i];
            var dx = node.Position.X - position.X;
            var dy = node.Position.Y - position.Y;
            var distanceSquared = (dx * dx) + (dy * dy);
            var reach = node.Radius + margin;
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
    /// Creation, self-connect, duplicate beam) change nothing and return false
    /// instead of throwing.
    /// </summary>
    public bool ConnectBeam(int nodeIdA, int nodeIdB)
    {
        if (_moveOnly)
        {
            return false;
        }

        try
        {
            _history.Change(() => _builder.AddBeam(nodeIdA, nodeIdB));
        }
        catch (ArgumentException)
        {
            return false;
        }

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
            if (!_moveOnly && nodeIdA != nodeIdB && _builder.Nodes.Any(node => node.Id == nodeIdB))
            {
                PlacementNote = new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, nodeIdB), reason);
            }

            return null;
        }

        var pistonId = _history.Change(() => _builder.AddPiston(nodeIdA, nodeIdB));
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
        var nodeId = _history.Change(() =>
        {
            var id = _builder.AddNode(splitPoint);
            _builder.SplitBeamAtNode(beamId, id);
            ClearSelectionSets();
            return id;
        });
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
        if (_moveOnly || SelectedPartCount == 0)
        {
            return;
        }

        _history.Change(() =>
        {
            // Parts first, so none is already gone with a deleted beam or joint.
            foreach (var sensorId in _selectedSensorIds)
            {
                _builder.RemoveSensor(sensorId);
            }

            foreach (var pistonId in _selectedPistonIds)
            {
                _builder.RemovePiston(pistonId);
            }

            foreach (var beamId in _selectedBeamIds)
            {
                _builder.RemoveBeam(beamId);
            }

            foreach (var nodeId in _selectedNodeIds)
            {
                _builder.RemoveNode(nodeId);
            }

            // Within the step: its Undo row refresh must find no deleted part still selected.
            ClearSelectionSets();
        });
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
    /// The creature for Start training, if it can train (<see cref="CreatureReadiness"/>). Build's
    /// readiness line already shows why one cannot, so a refusal says nothing more.
    /// </summary>
    public bool TryGetTrainableCreature(out CreatureDef? creature)
    {
        if (!TryLeave(out creature, out _))
        {
            return false;
        }

        if (creature is null || !CreatureReadiness.CanTrain(creature))
        {
            creature = null;
            return false;
        }

        return true;
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
