using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
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
/// etc.) this view-model exposes. The selection and its transforms are in
/// BuildViewModel.Selection.cs. See `docs/BUILD_MODE.md`.
/// </summary>
public sealed partial class BuildViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Where joints may go, in canvas units: 12 × 6 m, centred on the origin,
    /// so a creature always fits the Training view without zooming out far
    /// (#884). Placing and moving keep a joint's ring inside; the Build view
    /// shows it plus <see cref="BuildViewMargin"/>. Its sides are whole
    /// multiples of <see cref="BuildGridStep"/> so the grid's cells fill it
    /// exactly.
    /// </summary>
    public static readonly CanvasRect BuildArea = new(
        new Vector2D(-12 * BuildGridStep, -6 * BuildGridStep),
        new Vector2D(12 * BuildGridStep, 6 * BuildGridStep));

    /// <summary>The Build grid's cell size in canvas units: half a metre.</summary>
    public const double BuildGridStep = Metres.WorldUnitsPerMetre / 2;

    /// <summary>How far past <see cref="BuildArea"/> the Build view can show, in canvas units, at any zoom.</summary>
    public const double BuildViewMargin = BuildGridStep;

    /// <summary>The part of the canvas the Build view can show: <see cref="BuildArea"/> plus <see cref="BuildViewMargin"/>.</summary>
    public static readonly CanvasRect BuildViewBounds = new(
        new Vector2D(BuildArea.Min.X - BuildViewMargin, BuildArea.Min.Y - BuildViewMargin),
        new Vector2D(BuildArea.Max.X + BuildViewMargin, BuildArea.Max.Y + BuildViewMargin));

    private CreatureBuilder _builder;
    private bool _isActive;
    private BuildTool _activeTool = BuildTool.Joint;
    private BuildLink _pickedLink = BuildLink.Beam;
    private BuildPart? _pickedPart;
    private bool _locked;
    private string _creationName = string.Empty;
    private int? _trainingGeneration;
    private CanvasNote? _placementNote;
    // What a Play tap pointed at, each with its joint: loose joints (Node) and Servos missing a link
    // (Servo). By joint, as picking a Servo's link gives it a new id (#911).
    private readonly HashSet<(CreatureElementKind Kind, int JointId)> _shownBlockers = [];
    private bool _showPieces;
    // The notes a refused Copy or Delete tap put on the parts that block it (#937, #987), until the
    // selection changes, the Creation is unlocked or the canvas is touched.
    private IReadOnlyList<CanvasNote> _shownRefusalNotes = [];
    private bool _advancedSettingsOpen;
    private readonly BuildHistory _history;
    private BrainDef? _openedBrain;
    private bool _shownCanUndo;
    private bool _shownCanRedo;

    /// <summary>Why a sensor dropped on anything but a beam is refused.</summary>
    public static UiText SensorsGoOnABeamReason { get; } = UiText.Plain("Sensors go on a beam");

    /// <summary>Why a locked Creation refuses an edit that would change its model (#896): see <see cref="IsLocked"/>.</summary>
    public static UiText LockedReason { get; } = UiText.Plain("Locked: the model is trained for these parts.");

    // On each part that makes a locked Creation refuse Copy (#990) or Delete (#987).
    private static readonly UiText _wouldChangeModelNote = UiText.Plain("Locked: would change the model");

    public BuildViewModel(CreatureBuilder? builder = null)
    {
        _builder = builder ?? new CreatureBuilder();
        _history = new BuildHistory(Snapshot);
        _history.Changed += (_, _) => NotifyHistoryChanged();
    }

    public void Load(CreatureDef creature, bool locked = false, string? creationName = null, TrainingStateDef? training = null)
    {
        ArgumentNullException.ThrowIfNull(creature);
        _builder = new CreatureBuilder(creature);
        ClearSelectionSets();
        _creationName = creationName ?? string.Empty;
        _trainingGeneration = training?.Generation;
        _openedBrain = training?.Brain;
        _locked = locked;
        _history.Clear();
        _shownBlockers.Clear();
        _showPieces = false;
        _shownRefusalNotes = [];
        _advancedSettingsOpen = false;
        ActiveTool = BuildTool.Joint;
        SetPickedLink(BuildLink.Beam);
        ClearPickedPart();
        PlacementNote = null;
        RaiseAnatomyChanged();
    }

    /// <summary>
    /// Opens a saved Creation. It is fully editable until it is locked (<see cref="CreationLock"/>);
    /// a locked one refuses every edit that would change its model until <see cref="Unlock"/>.
    /// </summary>
    public void LoadCreation(CreationDef creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        Load(creation.Creature, CreationLock.IsLocked(creation), creation.Name, creation.Training);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised whenever the placed anatomy (nodes, beams, or sensors) changes, so the UI can redraw.</summary>
    public event EventHandler? AnatomyChanged;

    /// <summary>
    /// True for a locked Creation (#896): it refuses every edit that would change its model, adding or
    /// deleting a part with brain ports or clearing a Servo's link. Joints, beams and Springs have no
    /// ports, so they can still be added and deleted.
    /// </summary>
    public bool IsLocked => _locked;

    /// <summary>
    /// Unlocks a locked Creation for this Build visit (#371), so its body can change. The training
    /// is kept: each saved edit refits the brain to the new body (#516). Nothing about the unlock is
    /// saved, so the Creation is locked again the next time Build opens it.
    /// </summary>
    public void Unlock()
    {
        if (!_locked)
        {
            return;
        }

        _locked = false;
        OnPropertyChanged(nameof(IsLocked));
        if (_shownRefusalNotes.Count > 0)
        {
            // Copy and Delete notes shown while locked may blame the lock, which no longer holds (#990, #987).
            _shownRefusalNotes = [];
            OnPropertyChanged(nameof(CanvasNotes));
        }
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

    /// <summary>Puts the body back as it was before the last step, and selects again what a delete removed (#878).</summary>
    public void Undo()
    {
        if (_history.Undo() is { } step)
        {
            Restore(step.Body, step.Reselect);
        }
    }

    /// <summary>Applies the last undone step again.</summary>
    public void Redo()
    {
        if (_history.Redo() is { } body)
        {
            Restore(body, null);
        }
    }

    // Part ids are never reused (#220): the restored body keeps the highest NextPartId this visit reached.
    // A selected Servo stays selected through a link change's new id (#911), unless the step reselects its own parts.
    private void Restore(CreatureDef body, PartSet? reselect)
    {
        var nextPartId = Math.Max(body.NextPartId, _builder.NextPartId);
        var selectedServoJoints = reselect is null ? SelectedServoJoints() : [];
        _builder = new CreatureBuilder(new CreatureDef(body.Nodes, body.Beams, body.Sensors, body.Servos, body.Pistons, body.Springs, nextPartId));
        if (reselect is not null)
        {
            ClearSelectionSets();
            foreach (var kind in Enum.GetValues<CreatureElementKind>())
            {
                SelectedSet(kind).UnionWith(reselect.SetOf(kind));
            }
        }

        PruneSelection(selectedServoJoints);
        PlacementNote = null;
        NotifySelectionChanged();
        RaiseAnatomyChanged();
    }

    // A creation joined into one piece forgets the tap's piece notes (#930): a later split waits for the next tap.
    private void RaiseAnatomyChanged()
    {
        if (_showPieces && Pieces().Count <= 1)
        {
            _showPieces = false;
        }

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
            OnPropertyChanged();
            ClearPickedPart();
        }
    }

    /// <summary>
    /// The link the Links tool draws when a drag starts from an unselected joint (#705). Beam on
    /// <see cref="Load"/>; a pick then stays for the visit, across tool switches (#874).
    /// </summary>
    public BuildLink PickedLink => _pickedLink;

    /// <summary>Picks the link the Links tool draws. Locked and future links do nothing.</summary>
    public void PickLink(BuildLink link)
    {
        if (_activeTool != BuildTool.Beam || !BuildLinkList.IsAvailable(link) || (_locked && BuildLinkList.HasBrainPorts(link)))
        {
            return;
        }

        SetPickedLink(link);
    }

    /// <summary>
    /// Whether the settings panels show their Advanced section (#903): one flag for the whole Build
    /// visit, kept across selections, closed again by <see cref="Load"/> and never saved.
    /// </summary>
    public bool AdvancedSettingsOpen
    {
        get => _advancedSettingsOpen;
        set
        {
            if (_advancedSettingsOpen == value)
            {
                return;
            }

            _advancedSettingsOpen = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// The tray part a canvas tap places (#805), or null. Picked in the Parts tool and kept after each
    /// placement, so several can be placed. A switch of tool or tray tab, a collapsed side panel, a
    /// selection, Back, or <see cref="Load"/> clears it, so it is never picked out of sight.
    /// </summary>
    public BuildPart? PickedPart => _pickedPart;

    /// <summary>Picks a tray part, or clears it if it is the picked one (#805). Parts that cannot be placed now do nothing.</summary>
    public void PickPart(BuildPart part)
    {
        if (_activeTool != BuildTool.Parts || !PartTray.IsAvailable(part) || _locked)
        {
            return;
        }

        SetPickedPart(_pickedPart == part ? null : part);
    }

    /// <summary>Clears <see cref="PickedPart"/>; true if one was picked, so Back takes that step first.</summary>
    public bool ClearPickedPart()
    {
        if (_pickedPart is null)
        {
            return false;
        }

        SetPickedPart(null);
        return true;
    }

    private void SetPickedPart(BuildPart? part)
    {
        _pickedPart = part;
        OnPropertyChanged(nameof(PickedPart));
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

    public IReadOnlyList<ServoDef> Servos => _builder.Servos;

    public IReadOnlyList<PistonDef> Pistons => _builder.Pistons;

    public IReadOnlyList<SpringDef> Springs => _builder.Springs;

    /// <summary>
    /// The messages Build shows in the drawing, each beside the part it is about: why the last
    /// dropped part was refused (#376), each loose joint and Servo missing a link
    /// <see cref="ShowTrainingBlockers"/> pointed at (#844, #1006), each separate piece's joint nearest
    /// another piece (#930) and each part a dimmed Copy or a locked Delete pointed at (#937, #987), then each beam or link too
    /// short to train (#593). Listed most important first:
    /// notes that would overlap stack, the first listed nearest its part.
    /// </summary>
    public IReadOnlyList<CanvasNote> CanvasNotes()
    {
        var notes = new List<CanvasNote>();
        if (_placementNote is { } placement && Exists(placement.Target))
        {
            notes.Add(placement);
        }

        foreach (var node in Nodes)
        {
            if (_shownBlockers.Contains((CreatureElementKind.Node, node.Id)) && IsLoose(node.Id))
            {
                notes.Add(NotConnected(node.Id));
            }
        }

        if (_showPieces && Pieces() is { Count: > 1 } pieces)
        {
            notes.AddRange(pieces.Select(piece => NotConnected(NearestOtherPiece(piece, pieces))));
        }

        notes.AddRange(_shownRefusalNotes.Where(note => Exists(note.Target)));

        foreach (var beam in Beams)
        {
            AddTooShortNote(CreatureElementKind.Beam, beam.Id, beam.NodeA, beam.NodeB);
        }

        foreach (var servo in Servos.Where(servo => _shownBlockers.Contains((CreatureElementKind.Servo, servo.NodeId)) && CreatureReadiness.IsMissingALink(servo)))
        {
            var text = ServoNeedsTwoLinks(servo.NodeId)
                ? CreatureBuilder.ServoNeedsTwoLinksReason
                : (servo.FixedLinkId is null, servo.TargetLinkId is null) switch
                {
                    (true, true) => UiText.Plain("Pick two links"),
                    (true, false) => UiText.Plain("Pick a Fixed link"),
                    _ => UiText.Plain("Pick a Target link"),
                };
            notes.Add(new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Servo, servo.Id), text));
        }

        foreach (var piston in Pistons)
        {
            AddTooShortNote(CreatureElementKind.Piston, piston.Id, piston.NodeA, piston.NodeB);
        }

        foreach (var spring in Springs)
        {
            AddTooShortNote(CreatureElementKind.Spring, spring.Id, spring.NodeA, spring.NodeB);
        }

        return notes;

        // The piece's joint nearest another piece, where a joining link would go; the first on a tie.
        int NearestOtherPiece(IReadOnlyList<int> piece, IReadOnlyList<IReadOnlyList<int>> pieces)
        {
            var nearest = piece[0];
            var nearestDistanceSquared = double.PositiveInfinity;
            foreach (var id in piece)
            {
                var position = Nodes[NodeIndexOf(id)].Position;
                foreach (var other in pieces)
                {
                    if (other == piece)
                    {
                        continue;
                    }

                    foreach (var otherId in other)
                    {
                        var otherPosition = Nodes[NodeIndexOf(otherId)].Position;
                        var dx = position.X - otherPosition.X;
                        var dy = position.Y - otherPosition.Y;
                        var distanceSquared = (dx * dx) + (dy * dy);
                        if (distanceSquared < nearestDistanceSquared)
                        {
                            nearestDistanceSquared = distanceSquared;
                            nearest = id;
                        }
                    }
                }
            }

            return nearest;
        }

        static CanvasNote NotConnected(int nodeId) =>
            new(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, nodeId), UiText.Plain("Not connected"));

        void AddTooShortNote(CreatureElementKind kind, int id, int nodeA, int nodeB)
        {
            var a = Nodes[NodeIndexOf(nodeA)];
            var b = Nodes[NodeIndexOf(nodeB)];
            if (CreatureReadiness.IsTooShort(a, b, NodeRadius(a.Id), NodeRadius(b.Id)))
            {
                notes.Add(new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(kind, id), UiText.Plain("Too short")));
            }
        }
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

    /// <summary>Hides <see cref="PlacementNote"/> once it has been read.</summary>
    public void DismissPlacementNote() => PlacementNote = null;

    /// <summary>
    /// Hides the notes a tap brought up, on the next canvas touch (#991): <see cref="PlacementNote"/>,
    /// <see cref="ShowTrainingBlockers"/>' notes and a dimmed Copy's or a locked Delete's notes. Too-short parts' notes
    /// stay, as they mark the drawing itself.
    /// </summary>
    public void DismissTapNotes()
    {
        PlacementNote = null;
        if (_shownBlockers.Count == 0 && !_showPieces && _shownRefusalNotes.Count == 0)
        {
            return;
        }

        _shownBlockers.Clear();
        _showPieces = false;
        _shownRefusalNotes = [];
        OnPropertyChanged(nameof(CanvasNotes));
    }

    private void ShowRefusalNotes(IReadOnlyList<CanvasNote> notes)
    {
        _shownRefusalNotes = notes;
        OnPropertyChanged(nameof(CanvasNotes));
    }

    /// <summary>Joined to nothing by a beam or a link, so the creature cannot train (<see cref="CreatureReadiness.IsAttached"/>).</summary>
    public bool IsLoose(int nodeId) =>
        !Beams.Any(beam => beam.NodeA == nodeId || beam.NodeB == nodeId)
        && !Pistons.Any(piston => piston.NodeA == nodeId || piston.NodeB == nodeId)
        && !Springs.Any(spring => spring.NodeA == nodeId || spring.NodeB == nodeId);

    /// <summary>The pieces the links join the joints into (<see cref="CreatureReadiness.Pieces"/>); more than one cannot train.</summary>
    public IReadOnlyList<IReadOnlyList<int>> Pieces() => CreatureReadiness.Pieces(Nodes, LinkRef.All(Beams, Pistons, Springs));

    /// <summary>
    /// Answers a tap on the dimmed play button (#844): every joint loose now gets a "Not connected"
    /// note in <see cref="CanvasNotes"/>, kept until it is joined or removed, and so does every
    /// piece until the creation is one piece (#930) and every Servo missing a link until it has
    /// both (#1006); the next canvas touch hides them all (<see cref="DismissTapNotes"/>). Too-short
    /// parts already have theirs. A blocker that appears later waits for the next tap.
    /// </summary>
    public void ShowTrainingBlockers()
    {
        _showPieces = Pieces().Count > 1;
        _shownBlockers.Clear();
        _shownBlockers.UnionWith(Nodes.Where(node => IsLoose(node.Id)).Select(node => (CreatureElementKind.Node, node.Id)));
        _shownBlockers.UnionWith(Servos.Where(CreatureReadiness.IsMissingALink).Select(servo => (CreatureElementKind.Servo, servo.NodeId)));

        OnPropertyChanged(nameof(CanvasNotes));
    }

    private bool Exists(CreatureElementSelection element) => element.Kind switch
    {
        CreatureElementKind.Node => _builder.Nodes.Any(node => node.Id == element.Id),
        CreatureElementKind.Beam => _builder.Beams.Any(beam => beam.Id == element.Id),
        CreatureElementKind.Servo => _builder.Servos.Any(servo => servo.Id == element.Id),
        CreatureElementKind.Piston => _builder.Pistons.Any(piston => piston.Id == element.Id),
        CreatureElementKind.Spring => _builder.Springs.Any(spring => spring.Id == element.Id),
        _ => _builder.Sensors.Any(sensor => sensor.Id == element.Id),
    };

    /// <summary>Places a new node, moved inside <see cref="BuildArea"/>, and returns its id. A joint has no brain ports, so a locked Creation places one too (#896).</summary>
    public int PlaceNode(Vector2D position)
    {
        var id = _history.Change(() => _builder.AddNode(BuildArea.Clamp(position, NodeDef.PlainJointRadius)));
        RaiseAnatomyChanged();
        return id;
    }

    /// <summary>Moves an already-placed node to a new position, as far as <see cref="BuildArea"/> reaches.</summary>
    public void MoveNode(int nodeId, Vector2D position)
    {
        _history.Change(() => _builder.MoveNode(nodeId, BuildArea.Clamp(position, NodeRadius(nodeId))));
        RaiseAnatomyChanged();
    }

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
            RaiseAnatomyChanged();
        }
    }

    private void RequireEditable(PartParameterId parameter)
    {
        if (!CanEdit(parameter))
        {
            throw new InvalidOperationException($"The selection cannot change {parameter}.");
        }
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

    /// <summary>The name a part shows: its own name if it has one, else <see cref="DefaultPartName"/>.</summary>
    public UiText PartDisplayName(int partId) => PartNames.Display(_builder.Nodes, _builder.Beams, _builder.Sensors, _builder.Servos, _builder.Pistons, _builder.Springs, partId);

    /// <summary>The name a part shows until it is renamed: "Joint 2", "Beam 1", "Piston 1", "Spring 1", "Accel 1" or "Camera 1".</summary>
    public UiText DefaultPartName(int partId) => PartNames.Default(_builder.Nodes, _builder.Beams, _builder.Sensors, _builder.Servos, _builder.Pistons, _builder.Springs, partId);

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
        && !_builder.Servos.Any(servo => servo.Id == partId)
        && !_builder.Pistons.Any(piston => piston.Id == partId)
            && !_builder.Springs.Any(spring => spring.Id == partId))
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
        RaiseAnatomyChanged();
    }

    private string? PartName(int partId) => PartNames.Own(_builder.Nodes, _builder.Beams, _builder.Sensors, _builder.Servos, _builder.Pistons, _builder.Springs, partId);

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
            var reach = NodeRadius(node.Id) + margin;
            if (distanceSquared <= reach * reach && distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                nodeId = node.Id;
            }
        }

        return nodeId >= 0;
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

    /// <summary>
    /// The closest Piston or Spring within <paramref name="maxDistance"/> of
    /// <paramref name="position"/>, measured to the line between its nodes.
    /// </summary>
    public bool TryFindLinkNear(Vector2D position, double maxDistance, [NotNullWhen(true)] out CreatureElementSelection? link)
    {
        CreatureElementSelection? best = null;
        var bestDistanceSquared = maxDistance * maxDistance;
        var links = _builder.Pistons.Select(piston => (Kind: CreatureElementKind.Piston, piston.Id, piston.NodeA, piston.NodeB))
            .Concat(_builder.Springs.Select(spring => (Kind: CreatureElementKind.Spring, spring.Id, spring.NodeA, spring.NodeB)));
        foreach (var (kind, id, nodeA, nodeB) in links)
        {
            var distanceSquared = DistanceSquaredToSegment(position, NodeById(nodeA).Position, NodeById(nodeB).Position);
            if (distanceSquared <= bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                best = new CreatureElementSelection(kind, id);
            }
        }

        link = best;
        return link is not null;
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
    public bool TryLeave(out CreatureDef? creature, out IReadOnlyList<UiText> errors)
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

    public int ServoIndexOf(int servoId) => _builder.ServoIndexOf(servoId);

    public int SpringIndexOf(int springId) => _builder.SpringIndexOf(springId);

    public double NodeRadius(int nodeId) => _builder.NodeRadius(nodeId);

    public bool ServoNeedsTwoLinks(int nodeId) => _builder.ServoNeedsTwoLinks(nodeId);

    public int? ServoAtNode(int nodeId) => _builder.Servos.FirstOrDefault(servo => servo.NodeId == nodeId)?.Id;

    public IReadOnlyList<LinkRef> LinksAt(int nodeId) => _builder.LinksAt(nodeId);

    public LinkRef Link(int linkId) => _builder.Link(linkId);

    public int? SetServoLink(int servoId, bool fixedRole, int linkId)
    {
        if (_locked)
        {
            return null;
        }

        var newId = _history.Change(() =>
        {
            var id = _builder.SetServoLink(servoId, fixedRole, linkId);

            // Within the step: its Undo row refresh must not find the Servo's old id still selected.
            ClearSelectionSets();
            _selectedServoIds.Add(id);
            return id;
        });
        SelectionChanged();
        return newId;
    }

    private NodeDef NodeById(int nodeId) => _builder.Nodes[_builder.NodeIndexOf(nodeId)];

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
