using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

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
/// Drives Build mode: whether it is active, which tool is
/// selected, and the anatomy placed so far via a <see cref="CreatureBuilder"/>.
/// UI (see `.claude/rules/ui.md`) binds to this instead of mutating the
/// builder directly; it may still read the Domain DTOs (<see cref="NodeDef"/>
/// etc.) this view-model exposes. The selection and its transforms are in
/// BuildViewModel.Selection.cs. See `docs/BUILD_MODE.md`.
/// </summary>
public sealed partial class BuildViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Where joints may go, in canvas units: 12 × 6 m, centred on the origin,
    /// so a creature always fits the Training view without zooming out far.
    /// Placing and moving keep a joint's ring inside; the Build view
    /// shows it plus <see cref="BuildViewMargin"/>. Its sides are whole
    /// multiples of every grid size (<see cref="BuildGridDef.SizesCm"/>) so the grid's cells
    /// fill it exactly.
    /// </summary>
    public static readonly CanvasRect BuildArea = new(
        new Vector2D(-12 * BuildAreaCell, -6 * BuildAreaCell),
        new Vector2D(12 * BuildAreaCell, 6 * BuildAreaCell));

    /// <summary>
    /// The unit <see cref="BuildArea"/>, its margin and its corner marks are measured in, in canvas
    /// units: half a metre, the default <see cref="GridStep"/>. It does not follow the Creation's grid.
    /// </summary>
    public const double BuildAreaCell = Metres.WorldUnitsPerMetre / 2;

    /// <summary>How far past <see cref="BuildArea"/> the Build view can show, in canvas units, at any zoom.</summary>
    public const double BuildViewMargin = BuildAreaCell;

    /// <summary>The part of the canvas the Build view can show: <see cref="BuildArea"/> plus <see cref="BuildViewMargin"/>.</summary>
    public static readonly CanvasRect BuildViewBounds = new(
        new Vector2D(BuildArea.Min.X - BuildViewMargin, BuildArea.Min.Y - BuildViewMargin),
        new Vector2D(BuildArea.Max.X + BuildViewMargin, BuildArea.Max.Y + BuildViewMargin));

    private CreatureBuilder _builder;
    private bool _isActive;
    private BuildTool _activeTool = BuildTool.Joint;
    private BuildLink? _pickedLink = BuildLink.Beam;
    private BuildPart? _pickedPart;
    private bool _locked;
    private string _creationName = string.Empty;
    private int? _trainingGeneration;
    private CanvasNote? _placementNote;
    // What a Play tap pointed at: loose joints and motors or Brakes missing a link.
    private readonly HashSet<CreatureElementSelection> _shownBlockers = [];
    private bool _showPieces;
    // The notes a refused Copy or Delete tap put on the parts that block it, until the
    // selection changes, the Creation is unlocked or the canvas is touched.
    private IReadOnlyList<CanvasNote> _shownRefusalNotes = [];
    private bool _advancedSettingsOpen;
    private readonly BuildHistory _history;
    private NeuralNetworkDef? _openedNetwork;
    private bool _shownCanUndo;
    private bool _shownCanRedo;

    /// <summary>
    /// Why a sensor of <paramref name="kind"/> dropped on anything but a beam is refused: it
    /// names the part, as a later sensor may go elsewhere.
    /// </summary>
    public static UiText GoesOnABeamReason(SensorKind kind) => kind switch
    {
        SensorKind.Accelerometer => UiText.Plain("Accelerometers go on a beam"),
        SensorKind.Camera => UiText.Plain("Cameras go on a beam"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Why a locked Creation refuses an edit that would change its network: see <see cref="IsLocked"/>.</summary>
    public static UiText LockedReason { get; } = UiText.Plain("The creation is locked to avoid changes to the network, unlock to enable.");

    // On each part that makes a locked Creation refuse Copy or Delete.
    private static readonly UiText _wouldChangeNetworkNote = UiText.Plain("Locked: would change the network");

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
        _selected.Clear();
        _creationName = creationName ?? string.Empty;
        _trainingGeneration = training?.Generation;
        _openedNetwork = training?.Network;
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
    /// a locked one refuses every edit that would change its network until <see cref="Unlock"/>.
    /// </summary>
    public void LoadCreation(CreationDef creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        Load(creation.Creature, CreationLock.IsLocked(creation), creation.Name, creation.Training);
        UseGrid(creation.Grid);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised whenever the placed anatomy (nodes, beams, or sensors) changes, so the UI can redraw.</summary>
    public event EventHandler? AnatomyChanged;

    /// <summary>
    /// True for a locked Creation: it refuses every edit that would change its network, adding or
    /// deleting a part with network ports or clearing a Servo's link. Joints, beams, Springs and Wheels
    /// have no ports, so they can still be added and deleted, unless a delete takes a joint's Touch sensor. A Rubber
    /// and a Brake have none either.
    /// </summary>
    public bool IsLocked => _locked;

    /// <summary>
    /// Unlocks a locked Creation for this Build visit, so its body can change. The training
    /// is kept: each saved edit refits the network to the new body. Nothing about the unlock is
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
            // Copy and Delete notes shown while locked may blame the lock, which no longer holds.
            _shownRefusalNotes = [];
            OnPropertyChanged(nameof(CanvasNotes));
        }
    }

    public string CreationName => _creationName;

    /// <summary>
    /// The trained network as it was when Build opened, or null for an untrained Creation. Saves refit
    /// this network rather than the last saved one, so an undone delete gets its trained weights back.
    /// </summary>
    public NeuralNetworkDef? OpenedNetwork => _openedNetwork;

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    /// <summary>
    /// Opens an edit for <paramref name="owner"/>, such as a drag, that lasts until its
    /// <see cref="EndEdit"/>: all its changes are one undo step. An edit another owner left open
    /// ends first, so one source never merges into, closes or drops another's edit.
    /// </summary>
    public void BeginEdit(object owner) => _history.Begin(owner);

    /// <summary>Ends <paramref name="owner"/>'s open edit: one undo step if it changed the body.</summary>
    public void EndEdit(object owner) => _history.End(owner);

    /// <summary>Drops <paramref name="owner"/>'s open edit without a step, once it has put the body back.</summary>
    public void CancelEdit(object owner) => _history.Cancel(owner);

    /// <summary>Puts the body back as it was before the last step, and selects again what a delete removed.</summary>
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

    // Part ids are never reused: the restored body keeps the highest NextPartId this visit reached.
    private void Restore(CreatureDef body, PartSet? reselect)
    {
        var nextPartId = Math.Max(body.NextPartId, _builder.NextPartId);
        _builder = new CreatureBuilder(new CreatureDef(body.Parts, nextPartId));
        if (reselect is not null)
        {
            _selected.Clear();
            _selected.UnionWith(reselect.Parts);
        }

        PruneSelection();
        if (reselect is null)
        {
            SelectJointPartsInsteadOfTheirJoints();
        }

        PlacementNote = null;
        NotifySelectionChanged();
        RaiseAnatomyChanged();
    }

    // A creation joined into one piece forgets the tap's piece notes: a later split waits for the next tap.
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

    /// <summary>The Camera whose aim handle shows: Aim can be set, so it is the one selected part.</summary>
    public int? AimableCameraId => CanEdit(PartParameterId.Aim) ? SingleSelectedId(CreatureElementKind.Sensor) : null;

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

    /// <summary>
    /// A tap on a rail tool, even the active one: clears the selection so the side panel shows the
    /// tool's panel.
    /// </summary>
    public void PickTool(BuildTool tool)
    {
        ClearSelection();
        ActiveTool = tool;
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
    /// The link the Links tool draws when a drag starts from an unselected joint, or null. Beam
    /// on <see cref="Load"/>; a pick, or none, then stays for the visit, across tool switches and
    /// selections. With none, Links works like the other tools.
    /// </summary>
    public BuildLink? PickedLink => _pickedLink;

    /// <summary>Picks the link the Links tool draws, or clears it if it is the picked one. Locked and future links do nothing.</summary>
    public void PickLink(BuildLink link)
    {
        if (_activeTool != BuildTool.Beam || !BuildLinkList.IsAvailable(link) || (_locked && BuildLinkList.HasNetworkPorts(link)))
        {
            return;
        }

        SetPickedLink(_pickedLink == link ? null : link);
    }

    /// <summary>Clears <see cref="PickedLink"/>. Back does not, since Beam is where Links starts.</summary>
    public void ClearPickedLink() => SetPickedLink(null);

    /// <summary>
    /// Whether the settings panels show their Advanced section: one flag for the whole Build
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
    /// The tray part a canvas tap places, or null. Picked in the Parts tool and kept after each
    /// placement, so several can be placed. A switch of tool or tray tab, a collapsed side panel, a
    /// selection, Back, or <see cref="Load"/> clears it, so it is never picked out of sight.
    /// </summary>
    public BuildPart? PickedPart => _pickedPart;

    /// <summary>
    /// Picks a tray part, or clears it if it is the picked one. Parts that cannot be placed
    /// now do nothing, as a part with network ports on a locked Creation (<see cref="PartTray.CanPick"/>).
    /// </summary>
    public void PickPart(BuildPart part)
    {
        if (_activeTool != BuildTool.Parts || !PartTray.CanPick(part, _locked))
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

    private void SetPickedLink(BuildLink? link)
    {
        if (_pickedLink == link)
        {
            return;
        }

        _pickedLink = link;
        OnPropertyChanged(nameof(PickedLink));
    }

    /// <summary>Every part, each kind in its list order, as <see cref="CreatureBuilder.Parts"/>.</summary>
    public IReadOnlyList<IPartDef> Parts => _builder.Parts;

    public IReadOnlyList<NodeDef> Nodes => _builder.Nodes;

    public IReadOnlyList<BeamDef> Beams => _builder.Beams;

    public IReadOnlyList<SensorDef> Sensors => _builder.Sensors;

    public IReadOnlyList<ServoDef> Servos => _builder.Servos;

    public IReadOnlyList<VelocityMotorDef> VelocityMotors => _builder.VelocityMotors;

    /// <summary>Every joint motor: the Servos, then the Velocity motors, then the Steppers.</summary>
    public IReadOnlyList<IJointMotorDef> JointMotors => _builder.JointMotors;

    /// <summary>What fills each joint's hinge-part slot: the <see cref="JointMotors"/>, then the <see cref="Brakes"/>, then the <see cref="AngleSensors"/>.</summary>
    public IReadOnlyList<IHingePartDef> HingeParts => _builder.HingeParts;

    /// <summary>Every part that sits on a joint: the <see cref="HingeParts"/>, then the Wheels, Touch sensors and Rubbers.</summary>
    public IReadOnlyList<IJointPartDef> JointParts => _builder.JointParts;

    public IReadOnlyList<BrakeDef> Brakes => _builder.Brakes;

    public IReadOnlyList<AngleSensorDef> AngleSensors => _builder.AngleSensors;

    public IReadOnlyList<StepperDef> Steppers => _builder.Steppers;

    public IReadOnlyList<WheelDef> Wheels => _builder.Wheels;

    public IReadOnlyList<TouchSensorDef> TouchSensors => _builder.TouchSensors;

    public IReadOnlyList<RubberDef> Rubbers => _builder.Rubbers;

    public IReadOnlyList<PistonDef> Pistons => _builder.Pistons;

    public IReadOnlyList<SpringDef> Springs => _builder.Springs;

    /// <summary>Joined to nothing by a beam or a link, so the creature cannot train (<see cref="CreatureReadiness.IsAttached"/>).</summary>
    public bool IsLoose(int nodeId) =>
        !Beams.Any(beam => beam.NodeA == nodeId || beam.NodeB == nodeId)
        && !Pistons.Any(piston => piston.NodeA == nodeId || piston.NodeB == nodeId)
        && !Springs.Any(spring => spring.NodeA == nodeId || spring.NodeB == nodeId);

    /// <summary>
    /// Whether joint <paramref name="nodeId"/> draws the loose mark (ring and cross): a loose joint
    /// without a hinge part or Wheel. Those show the error in their own <c>danger</c> look; a Touch
    /// sensor's cushion and a Rubber's skin have none, so they do not count.
    /// </summary>
    public bool ShowsLooseMark(int nodeId) => IsLoose(nodeId)
        && !CreatureDef.HasJointPart(nodeId, _builder.JointParts.Where(part => PartKinds.SlotOf(part) is JointSlot.HingePart or JointSlot.Wheel));

    /// <summary>Why a Servo, Motor, Stepper, Brake or Angle sensor of <paramref name="kind"/> on a joint without two sides cannot work.</summary>
    public static UiText NeedsTwoSidesReason(CreatureElementKind kind) => kind switch
    {
        CreatureElementKind.Servo => CreatureBuilder.ServoNeedsTwoSidesReason,
        CreatureElementKind.VelocityMotor => CreatureBuilder.VelocityMotorNeedsTwoSidesReason,
        CreatureElementKind.Brake => CreatureBuilder.BrakeNeedsTwoSidesReason,
        CreatureElementKind.AngleSensor => CreatureBuilder.AngleSensorNeedsTwoSidesReason,
        CreatureElementKind.Stepper => CreatureBuilder.StepperNeedsTwoSidesReason,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only a Servo, Motor, Stepper, Brake or Angle sensor has sides."),
    };

    /// <summary>The pieces the links join the joints into (<see cref="Mechanics.Pieces.Of"/>); more than one cannot train.</summary>
    public IReadOnlyList<IReadOnlyList<int>> Pieces() => Mechanics.Pieces.Of(Nodes, LinkRef.All(Beams, Pistons, Springs));

    /// <summary>The part with <paramref name="partId"/>, whatever its kind, or null.</summary>
    public IPartDef? FindPart(int partId) => _builder.FindPart(partId);

    // The part element names, or null when the body has no part of that kind with that id.
    private IPartDef? FindPart(CreatureElementSelection element) =>
        _builder.FindPart(element.Id) is { } part && CreatureElementSelection.Of(part) == element ? part : null;

    private bool Exists(CreatureElementSelection element) => FindPart(element) is not null;

    /// <summary>
    /// Places a new node, moved inside <see cref="BuildArea"/> and onto the grid when
    /// <see cref="SnapToGrid"/>, and returns its id. A joint has no network ports, so a locked
    /// Creation places one too.
    /// </summary>
    public int PlaceNode(Vector2D position)
    {
        var id = _history.Change(() => _builder.AddNode(Placed(position, NodeDef.PlainJointRadius)));
        RaiseAnatomyChanged();
        return id;
    }

    /// <summary>Moves an already-placed node to a new position, as far as <see cref="BuildArea"/> reaches, and onto the grid when <see cref="SnapToGrid"/>.</summary>
    public void MoveNode(int nodeId, Vector2D position)
    {
        _history.Change(() => _builder.MoveNode(nodeId, Placed(position, NodeRadius(nodeId))));
        RaiseAnatomyChanged();
    }

    private Vector2D Placed(Vector2D position, double radius) =>
        SnapToGrid ? OnGrid(position, radius) : BuildArea.Clamp(position, radius);

    /// <summary>
    /// The settings the panel shows for the selection: one part's own, or those every
    /// selected part has and can share.
    /// </summary>
    public IReadOnlyList<PartParameterId> ShownParameters
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

    /// <summary>
    /// The <see cref="ShownParameters"/> the selection can change now. Like a Camera's aim,
    /// most tune the body without changing the network's ports, so a locked Creation can change them
    /// too; one that <see cref="PartParameter.ChangesPorts"/>, a Camera's Rays, it keeps.
    /// </summary>
    public IReadOnlyList<PartParameterId> EditableParameters =>
        [.. ShownParameters.Where(id => !_locked || !PartParameters.Of(id).ChangesPorts)];

    public bool CanEdit(PartParameterId parameter) => EditableParameters.Contains(parameter);

    /// <summary>
    /// Each selected Camera's rays while the panel shows their settings, in canvas
    /// units: where they leave its beam and where each ends. The canvas draws them and the view
    /// keeps them in sight.
    /// </summary>
    public IReadOnlyList<(Vector2D Origin, IReadOnlyList<Vector2D> Ends)> ShownCameraRays()
    {
        if (!ShownParameters.Contains(PartParameterId.Rays))
        {
            return [];
        }

        return [.. Sensors.Where(sensor => sensor.Kind == SensorKind.Camera && IsSelected(CreatureElementKind.Sensor, sensor.Id)).Select(camera =>
        {
            var beam = Beams[BeamIndexOf(camera.BeamId)];
            var nodeA = Nodes[NodeIndexOf(beam.NodeA)].Position;
            var nodeB = Nodes[NodeIndexOf(beam.NodeB)].Position;
            var origin = SensorCentre(camera.Id);
            var (sin, cos) = Math.SinCos(CameraRays.BeamAngle(nodeA, nodeB));
            IReadOnlyList<Vector2D> ends = [.. CameraRays.LocalRayTargets(camera, camera.Aim ?? SensorDef.DefaultAim(nodeA, nodeB))
                .Select(target => new Vector2D(origin.X + (target.X * cos) - (target.Y * sin), origin.Y + (target.X * sin) + (target.Y * cos)))];
            return (origin, ends);
        })];
    }

    /// <summary>Where the sensor <paramref name="sensorId"/> sits on its beam and senses from (<see cref="SensorPicture.Centre"/>).</summary>
    public Vector2D SensorCentre(int sensorId) => SensorCentreOn(Sensors.Single(sensor => sensor.Id == sensorId).BeamId);

    /// <summary>Where a sensor sits on the beam <paramref name="beamId"/> (<see cref="SensorPicture.Centre"/>).</summary>
    public Vector2D SensorCentreOn(int beamId)
    {
        var beam = Beams[BeamIndexOf(beamId)];
        return SensorPicture.Centre(Nodes[NodeIndexOf(beam.NodeA)].Position, Nodes[NodeIndexOf(beam.NodeB)].Position, CreatureDef.CoveredRadius(beam.NodeA, JointMotors), CreatureDef.CoveredRadius(beam.NodeB, JointMotors));
    }

    /// <summary>
    /// Whether <paramref name="parameter"/> does anything on any selected part as set now: a one-ray
    /// Camera's Spread does nothing, so its slider shows disabled.
    /// </summary>
    public bool HasEffect(PartParameterId parameter) =>
        SelectedPartIds().Any(part => _builder.ParameterHasEffect(part, parameter));

    /// <summary>Each selected part's <paramref name="parameter"/>, in world units, for a shown setting.</summary>
    public IReadOnlyList<double> SelectedValuesOf(PartParameterId parameter)
    {
        if (!ShownParameters.Contains(parameter))
        {
            throw new InvalidOperationException($"The selection does not show {parameter}.");
        }

        return [.. SelectedPartIds().Select(part => _builder.ParameterValue(part, parameter))];
    }

    /// <summary>Sets <paramref name="parameter"/> to <paramref name="value"/>, in world units, on every selected part.</summary>
    public void SetParameter(PartParameterId parameter, double value)
    {
        // A Brake's top step is Locked: an infinite Strength.
        if (!double.IsFinite(value) && !(parameter == PartParameterId.BrakeStrength && value == BrakeDef.LockedStrength))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "A setting must be finite, but a Brake's Strength may be Locked.");
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

    /// <summary>
    /// The part drawn on top at <paramref name="position"/>, so a touch hits what it sees
    /// (<see cref="DrawGroups.PartAt"/>), with the selection and the <paramref name="targets"/> on
    /// the selected surface as the canvas draws them (<see cref="RaisedParts"/>). A motor, Stepper, Brake, Angle sensor or Wheel
    /// is hit as its joint; a Touch sensor and a Rubber add no tap area. Null off every part.
    /// </summary>
    public CreatureElementSelection? DrawnPartAt(Vector2D position, PlacingTargets targets)
    {
        var groups = DrawGroups();
        var hit = groups.PartAt(position, nodeId => NodeById(nodeId).Position, _builder.HingeParts, _builder.Wheels, _builder.Sensors, RaisedIn(groups, Selection, targets));
        return hit?.Kind switch
        {
            CreatureElementKind.Servo => new CreatureElementSelection(CreatureElementKind.Node, _builder.Servos[_builder.ServoIndexOf(hit.Id)].NodeId),
            CreatureElementKind.VelocityMotor => new CreatureElementSelection(CreatureElementKind.Node, _builder.VelocityMotors[_builder.VelocityMotorIndexOf(hit.Id)].NodeId),
            CreatureElementKind.Brake => new CreatureElementSelection(CreatureElementKind.Node, _builder.Brakes[_builder.BrakeIndexOf(hit.Id)].NodeId),
            CreatureElementKind.Stepper => new CreatureElementSelection(CreatureElementKind.Node, _builder.Steppers[_builder.StepperIndexOf(hit.Id)].NodeId),
            CreatureElementKind.Wheel => new CreatureElementSelection(CreatureElementKind.Node, _builder.Wheels[_builder.WheelIndexOf(hit.Id)].NodeId),
            _ => hit,
        };
    }

    /// <summary>
    /// The Wheel whose disc holds <paramref name="position"/> (<see cref="Domain.DrawGroups.WheelDiscAt"/>):
    /// a tap's last fallback, after every part and reach, never a press's or a drop's. Null outside every disc.
    /// </summary>
    public CreatureElementSelection? WheelDiscAt(Vector2D position) =>
        Domain.DrawGroups.WheelDiscAt(position, nodeId => NodeById(nodeId).Position, _builder.Wheels) is { } wheel
            ? new CreatureElementSelection(CreatureElementKind.Wheel, wheel.Id)
            : null;

    /// <summary>
    /// What the canvas draws on the selected surface, which a touch hits there too
    /// (<see cref="DrawnPartAt"/>): what belongs with <paramref name="selected"/>, a joint part
    /// as its joint, and the <paramref name="targets"/> alone, with the moved sensor.
    /// </summary>
    public RaisedParts RaisedParts(PartSet selected, PlacingTargets targets) => RaisedIn(DrawGroups(), selected, targets);

    private RaisedParts RaisedIn(DrawGroups groups, PartSet selected, PlacingTargets targets)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(targets);
        var parts = selected.Parts;
        if (targets.MovingSensor is { } moving)
        {
            parts = parts.Append(new CreatureElementSelection(CreatureElementKind.Sensor, moving));
        }

        return groups.Raised(parts, HingeParts, _builder.Wheels, _builder.TouchSensors, _builder.Rubbers, targets.Beams, targets.Joints);
    }

    private DrawGroups DrawGroups() => Domain.DrawGroups.Of(_builder.Nodes, LinkRef.All(_builder.Beams, _builder.Pistons, _builder.Springs));

    /// <summary>The name a part shows: its own name if it has one, else <see cref="DefaultPartName"/>.</summary>
    public UiText PartDisplayName(int partId) => PartNames.Display(_builder.Parts, partId);

    /// <summary>The name a part shows until it is renamed, such as "Joint 2" (<see cref="PartNames.Default"/>).</summary>
    public UiText DefaultPartName(int partId) => PartNames.Default(_builder.Parts, partId);

    /// <summary>
    /// Renames a part by id, so an edit lands on the part it started on even if the selection
    /// moved meanwhile; a part deleted since is ignored. A blank name, or the
    /// <paramref name="shownDefault"/> left as it is, clears its own name so it shows the default
    /// again. Only the screen knows the default in the player's language, so it passes the one it
    /// showed. Names are labels only, so a locked Creation can be renamed too.
    /// </summary>
    public void RenamePart(int partId, string name, string? shownDefault)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_builder.FindPart(partId) is null)
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

    private string? PartName(int partId) => PartNames.Own(_builder.Parts, partId);

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
    /// for the caller to instantiate.
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
    /// readiness chip already shows why one cannot, so a refusal says nothing more.
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

    public int ServoIndexOf(int servoId) => _builder.ServoIndexOf(servoId);

    public int WheelIndexOf(int wheelId) => _builder.WheelIndexOf(wheelId);

    public int TouchSensorIndexOf(int touchSensorId) => _builder.TouchSensorIndexOf(touchSensorId);

    public int RubberIndexOf(int rubberId) => _builder.RubberIndexOf(rubberId);

    public int VelocityMotorIndexOf(int velocityMotorId) => _builder.VelocityMotorIndexOf(velocityMotorId);

    public int BrakeIndexOf(int brakeId) => _builder.BrakeIndexOf(brakeId);

    public int AngleSensorIndexOf(int angleSensorId) => _builder.AngleSensorIndexOf(angleSensorId);

    public int StepperIndexOf(int stepperId) => _builder.StepperIndexOf(stepperId);

    public double NodeRadius(int nodeId) => _builder.NodeRadius(nodeId);

    public bool ServoNeedsTwoSides(int nodeId) => _builder.ServoNeedsTwoSides(nodeId);

    public int? ServoAtNode(int nodeId) => _builder.Servos.FirstOrDefault(servo => servo.NodeId == nodeId)?.Id;

    public int? WheelAtNode(int nodeId) => _builder.Wheels.FirstOrDefault(wheel => wheel.NodeId == nodeId)?.Id;

    /// <summary>
    /// The joint parts on joint <paramref name="nodeId"/>, top → down as drawn
    /// (<see cref="CreatureDef.JointParts"/>). To the player they are that joint: a tap, drag
    /// or box selects them, never the joint on its own.
    /// </summary>
    public IReadOnlyList<CreatureElementSelection> JointPartsAt(int nodeId) => CreatureDef.JointParts(nodeId, JointParts);

    public IReadOnlyList<LinkRef> LinksAt(int nodeId) => _builder.LinksAt(nodeId);

    public LinkRef Link(int linkId) => _builder.Link(linkId);

    /// <summary>
    /// Gives the Servo, Velocity motor, Stepper, Brake or Angle sensor <paramref name="partId"/> a new Fixed or Target side,
    /// from its panel's pickers; it keeps its id. Its ports do not depend on its sides, so a locked
    /// Creation changes them too.
    /// </summary>
    public void SetHingePartLink(int partId, bool fixedRole, int linkId)
    {
        _history.Change(() => _builder.SetHingePartLink(partId, fixedRole, linkId));
        RaiseAnatomyChanged();
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
