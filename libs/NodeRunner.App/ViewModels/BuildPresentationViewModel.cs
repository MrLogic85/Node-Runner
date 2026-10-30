using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// Pure presentation adapter for Build-mode shell state. Godot owns
/// nodes/input/rendering; this class owns stable labels, hints, and command
/// visibility so target screens can reuse the same contract without copying
/// the Build host's formatting rules.
/// </summary>
public sealed class BuildPresentationViewModel
{
    private readonly BuildViewModel _build;
    private EventHandler? _presentationChanged;
    private bool _isSubscribedToBuild;

    public BuildPresentationViewModel(BuildViewModel build)
    {
        _build = build ?? throw new ArgumentNullException(nameof(build));
    }

    public event EventHandler? PresentationChanged
    {
        add
        {
            _presentationChanged += value;
            SubscribeToBuild();
        }
        remove
        {
            _presentationChanged -= value;
            if (_presentationChanged is null)
            {
                UnsubscribeFromBuild();
            }
        }
    }

    public string CreationName => _build.CreationName;

    public BuildTool ActiveTool => _build.ActiveTool;

    public PartTrayPresentation Tray => PartTray.Create(_build.PickedPart, _build.IsLocked);

    public LinkListPresentation? LinkList => ToolPanel.Mode == ToolPanelMode.LinkList
        ? BuildLinkList.Create(_build.PickedLink, _build.IsLocked)
        : null;

    public ToolPanelPresentation ToolPanel => CreateToolPanel();

    /// <summary>The Reset training dialog body. Copy sits beside Reset in the overflow, so it is offered.</summary>
    public UiText ResetTrainingWarning =>
        UiText.Counted(
            "{1} forgets its {0} generation of training and keeps its body. Copy it first to keep the trained one.",
            "{1} forgets its {0} generations of training and keeps its body. Copy it first to keep the trained one.",
            _build.TrainingGeneration ?? 0,
            _build.CreationName);

    public int SelectedNodeCount => _build.SelectedNodeCount;

    public int SelectedPartCount => _build.SelectedPartCount;

    /// <summary>The Part settings for the one selected part, or null unless exactly one part is selected.</summary>
    public PartSettingsPresentation? SinglePart
    {
        get
        {
            var canDelete = _build.DeleteLockedReason is null;
            if (_build.SingleSelectedId(CreatureElementKind.Servo) is { } servoId)
            {
                var servo = ServoById(servoId);
                return new PartSettingsPresentation(
                    servoId,
                    PartKind.Servo,
                    _build.PartDisplayName(servoId),
                    _build.DefaultPartName(servoId),
                    null,
                    null,
                    PartInfo.Servo,
                    canDelete,
                    PanelSliders(),
                    SidePickers(servo, CreatureBuilder.ServoNeedsTwoSidesReason),
                    OnThisJoint: OnThisJoint(servo.NodeId));
            }

            if (_build.SingleSelectedId(CreatureElementKind.VelocityMotor) is { } velocityMotorId)
            {
                var motor = _build.VelocityMotors[_build.VelocityMotorIndexOf(velocityMotorId)];
                return new PartSettingsPresentation(
                    velocityMotorId,
                    PartKind.VelocityMotor,
                    _build.PartDisplayName(velocityMotorId),
                    _build.DefaultPartName(velocityMotorId),
                    null,
                    null,
                    PartInfo.VelocityMotor,
                    canDelete,
                    PanelSliders(),
                    SidePickers(motor, CreatureBuilder.VelocityMotorNeedsTwoSidesReason),
                    OnThisJoint: OnThisJoint(motor.NodeId));
            }

            if (_build.SingleSelectedId(CreatureElementKind.Stepper) is { } stepperId)
            {
                var stepper = _build.Steppers[_build.StepperIndexOf(stepperId)];
                return new PartSettingsPresentation(
                    stepperId,
                    PartKind.Stepper,
                    _build.PartDisplayName(stepperId),
                    _build.DefaultPartName(stepperId),
                    null,
                    null,
                    PartInfo.Stepper,
                    canDelete,
                    PanelSliders(),
                    SidePickers(stepper, CreatureBuilder.StepperNeedsTwoSidesReason),
                    OnThisJoint: OnThisJoint(stepper.NodeId));
            }

            if (_build.SingleSelectedId(CreatureElementKind.Brake) is { } brakeId)
            {
                var brake = _build.Brakes[_build.BrakeIndexOf(brakeId)];
                return new PartSettingsPresentation(
                    brakeId,
                    PartKind.Brake,
                    _build.PartDisplayName(brakeId),
                    _build.DefaultPartName(brakeId),
                    null,
                    null,
                    PartInfo.Brake,
                    canDelete,
                    PanelSliders(),
                    SidePickers(brake, CreatureBuilder.BrakeNeedsTwoSidesReason),
                    OnThisJoint: OnThisJoint(brake.NodeId));
            }

            if (_build.SingleSelectedId(CreatureElementKind.AngleSensor) is { } angleSensorId)
            {
                var angleSensor = _build.AngleSensors[_build.AngleSensorIndexOf(angleSensorId)];
                return new PartSettingsPresentation(
                    angleSensorId,
                    PartKind.AngleSensor,
                    _build.PartDisplayName(angleSensorId),
                    _build.DefaultPartName(angleSensorId),
                    null,
                    null,
                    PartInfo.AngleSensor,
                    canDelete,
                    PanelSliders(),
                    SidePickers(angleSensor, CreatureBuilder.AngleSensorNeedsTwoSidesReason),
                    OnThisJoint: OnThisJoint(angleSensor.NodeId));
            }

            if (_build.SingleSelectedId(CreatureElementKind.Piston) is { } pistonId)
            {
                return new PartSettingsPresentation(
                    pistonId,
                    PartKind.Piston,
                    _build.PartDisplayName(pistonId),
                    _build.DefaultPartName(pistonId),
                    null,
                    null,
                    PartInfo.Piston,
                    canDelete,
                    PanelSliders());
            }

            if (_build.SingleSelectedId(CreatureElementKind.Wheel) is { } wheelId)
            {
                var wheel = _build.Wheels[_build.WheelIndexOf(wheelId)];
                return new PartSettingsPresentation(
                    wheelId,
                    PartKind.Wheel,
                    _build.PartDisplayName(wheelId),
                    _build.DefaultPartName(wheelId),
                    null,
                    null,
                    PartInfo.Wheel,
                    canDelete,
                    PanelSliders(),
                    Readouts: [new PartReadout(UiText.Plain("Weight"), UiText.Format("{0} kg", new FixedNumber(Wheel.Mass(wheel), 1)))],
                    OnThisJoint: OnThisJoint(wheel.NodeId));
            }

            if (_build.SingleSelectedId(CreatureElementKind.TouchSensor) is { } touchId)
            {
                var touch = _build.TouchSensors[_build.TouchSensorIndexOf(touchId)];
                return new PartSettingsPresentation(
                    touchId,
                    PartKind.TouchSensor,
                    _build.PartDisplayName(touchId),
                    _build.DefaultPartName(touchId),
                    null,
                    null,
                    PartInfo.Touch,
                    canDelete,
                    PanelSliders(),
                    OnThisJoint: OnThisJoint(touch.NodeId));
            }

            if (_build.SingleSelectedId(CreatureElementKind.Rubber) is { } rubberId)
            {
                var rubber = _build.Rubbers[_build.RubberIndexOf(rubberId)];
                return new PartSettingsPresentation(
                    rubberId,
                    PartKind.Rubber,
                    _build.PartDisplayName(rubberId),
                    _build.DefaultPartName(rubberId),
                    null,
                    null,
                    PartInfo.Rubber,
                    canDelete,
                    PanelSliders(),
                    OnThisJoint: OnThisJoint(rubber.NodeId));
            }

            if (_build.SingleSelectedId(CreatureElementKind.Spring) is { } springId)
            {
                return new PartSettingsPresentation(
                    springId,
                    PartKind.Spring,
                    _build.PartDisplayName(springId),
                    _build.DefaultPartName(springId),
                    null,
                    null,
                    PartInfo.Spring,
                    canDelete,
                    PanelSliders());
            }

            if (_build.SingleSelectedId(CreatureElementKind.Sensor) is { } sensorId)
            {
                var sensor = SensorById(sensorId);
                return new PartSettingsPresentation(
                    sensorId,
                    ((IPartDef)sensor).PartKind,
                    _build.PartDisplayName(sensorId),
                    _build.DefaultPartName(sensorId),
                    UiText.Plain("On"),
                    _build.PartDisplayName(sensor.BeamId),
                    SensorNote(sensor.Kind, aimable: _build.AimableCameraId == sensorId),
                    canDelete,
                    PanelSliders());
            }

            if (_build.SingleSelectedId(CreatureElementKind.MemoryCell) is { } memoryCellId)
            {
                var memoryCell = _build.MemoryCells[_build.MemoryCellIndexOf(memoryCellId)];
                return new PartSettingsPresentation(
                    memoryCellId,
                    PartKind.MemoryCell,
                    _build.PartDisplayName(memoryCellId),
                    _build.DefaultPartName(memoryCellId),
                    UiText.Plain("On"),
                    _build.PartDisplayName(memoryCell.BeamId),
                    PartInfo.MemoryCell,
                    canDelete,
                    PanelSliders());
            }

            if (_build.SingleSelectedId(CreatureElementKind.Beam) is { } beamId)
            {
                var beam = BeamById(beamId);
                return new PartSettingsPresentation(
                    beamId,
                    PartKind.Beam,
                    _build.PartDisplayName(beamId),
                    _build.DefaultPartName(beamId),
                    UiText.Plain("Between"),
                    UiText.Format("{0} ↔ {1}", _build.PartDisplayName(beam.NodeA), _build.PartDisplayName(beam.NodeB)),
                    PartInfo.Beam,
                    canDelete,
                    PanelSliders());
            }

            if (_build.SingleSelectedId(CreatureElementKind.Node) is { } nodeId)
            {
                return new PartSettingsPresentation(
                    nodeId,
                    PartKind.Node,
                    _build.PartDisplayName(nodeId),
                    _build.DefaultPartName(nodeId),
                    null,
                    null,
                    UiText.Plain("Links meet and turn here. Drag it to move them."),
                    canDelete,
                    PanelSliders());
            }

            return null;
        }
    }

    /// <summary>
    /// A slider for each setting the panel shows for the selection, disabled where it can't
    /// change now: kept by a locked Creation, or doing nothing on these parts.
    /// </summary>
    private List<ParameterSlider> PanelSliders() =>
        [.. _build.ShownParameters
            .Where(id => PartParameters.Of(id).InPanel)
            .Select(id => PartParameters.SliderOver(id, _build.SelectedValuesOf(id)) with
            {
                Locked = !_build.CanEdit(id),
                NoEffect = !_build.HasEffect(id),
            })];

    /// <summary>What a sensor does; an <paramref name="aimable"/> Camera's note also says what its Aim handle does.</summary>
    public static UiText SensorNote(SensorKind kind, bool aimable) => kind switch
    {
        SensorKind.Accelerometer => PartInfo.Accelerometer,
        SensorKind.Camera => aimable
            ? UiText.Plain("Measures distance to the ground. Drag the round handle to aim it.")
            : PartInfo.Camera,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The selection panel, or null unless several parts are selected.</summary>
    public SelectionPanelPresentation? Selection
    {
        get
        {
            var count = _build.SelectedPartCount;
            if (count < 2)
            {
                return null;
            }

            var selection = _build.Selection;
            var settings = PanelSliders();
            var showFrameRows = _build.SelectedNodeCount >= 2;
            var deleteNote = selection.IdsOf(CreatureElementKind.Node).Count > 0
                ? UiText.Plain("Links on a deleted joint go with it.")
                : _build.BeamParts.FirstOrDefault(part => selection.Contains(CreatureElementKind.Beam, part.BeamId) && !selection.Contains(CreatureElementSelection.Of(part))) switch
                {
                    SensorDef => UiText.Plain("A sensor on a deleted beam goes with it."),
                    MemoryCellDef => UiText.Plain("A Memory cell on a deleted beam goes with it."),
                    _ => null,
                };
            return new SelectionPanelPresentation(
                UiText.Counted("{0} selected", "{0} selected", count),
                settings,
                settings.Count > 0 ? UiText.Plain("A slider sets one value for all of them.") : null,
                settings.Count == 0 && !showFrameRows ? UiText.Plain("These parts share no settings.") : null,
                showFrameRows,
                UiText.Counted("Delete {0}", "Delete {0}", count),
                deleteNote,
                CanDelete: _build.DeleteLockedReason is null,
                UiText.Counted("Copy {0}", "Copy {0}", count),
                _build.CanCopySelection);
        }
    }

    /// <summary>Whether the part and selection panels show their Advanced settings.</summary>
    public bool AdvancedSettingsOpen => _build.AdvancedSettingsOpen;

    /// <summary>True for a locked Creation: it refuses every edit that would change its network.</summary>
    public bool IsLocked => _build.IsLocked;

    /// <summary>Whether the overflow's Undo can be tapped.</summary>
    public bool CanUndo => _build.CanUndo;

    /// <summary>Whether the overflow's Redo can be tapped.</summary>
    public bool CanRedo => _build.CanRedo;

    /// <summary>The grid size's place in <see cref="BuildGridDef.SizesCm"/>.</summary>
    public int GridSizeIndex => BuildGridSizes.IndexOf(_build.Grid.SizeCm);

    /// <summary>Whether placed and moved joints land on the grid.</summary>
    public bool SnapToGrid => _build.SnapToGrid;

    /// <summary>True when the Creation has saved training, locked or unlocked for this visit.</summary>
    public bool IsTrained => _build.TrainingGeneration is not null;

    /// <summary>
    /// Whether the overflow offers Copy creation and Share build: anything drawn,
    /// trained or not. An empty creation has nothing to copy or share.
    /// </summary>
    public bool CanCopy => _build.Nodes.Count > 0;

    public BuildReadinessPresentation Readiness => CreateReadiness();

    private ToolPanelPresentation CreateToolPanel()
    {
        if (_build.SelectedPartCount > 0)
        {
            return ToolPanelPresentation.None;
        }

        return ActiveTool switch
        {
            BuildTool.Parts => new ToolPanelPresentation(ToolPanelMode.PartsTray, UiText.Plain("Parts")),
            BuildTool.Beam => new ToolPanelPresentation(ToolPanelMode.LinkList, UiText.Plain("Links")),
            BuildTool.Joint => new ToolPanelPresentation(ToolPanelMode.JointHelp, UiText.Plain("Joint")),
            BuildTool.Select => new ToolPanelPresentation(ToolPanelMode.SelectHelp, UiText.Plain("Select")),
            _ => ToolPanelPresentation.None,
        };
    }

    private BuildReadinessPresentation CreateReadiness()
    {
        // Nothing drawn yet is a start, not a fault: the chip invites rather than warns.
        if (_build.Nodes.Count == 0)
        {
            return new BuildReadinessPresentation(BuildReadiness.Empty, UiText.Plain("Add joints and links"));
        }

        // With joints drawn, TryBuild fails exactly when CreatureReadiness finds a problem.
        return _build.TryLeave(out _, out var errors)
            ? new BuildReadinessPresentation(BuildReadiness.Ready, UiText.Plain("Ready to train"))
            : new BuildReadinessPresentation(BuildReadiness.Blocked, ShortReadiness(errors));
    }

    // A short form of the builder's errors for the readiness chip; CreatureReadiness decides whether training may start.
    private UiText ShortReadiness(IReadOnlyList<UiText> errors)
    {
        var unconnected = Enumerable.Range(0, _build.Nodes.Count)
            .Count(index =>
            {
                var nodeId = _build.Nodes[index].Id;
                return !_build.Beams.Any(beam => beam.NodeA == nodeId || beam.NodeB == nodeId)
                    && !_build.Pistons.Any(piston => piston.NodeA == nodeId || piston.NodeB == nodeId)
                    && !_build.Springs.Any(spring => spring.NodeA == nodeId || spring.NodeB == nodeId);
            });
        if (unconnected > 0)
        {
            return UiText.Counted("{0} joint not connected", "{0} joints not connected", unconnected);
        }

        if (_build.Pieces() is { Count: > 1 } pieces)
        {
            return UiText.Counted("{0} piece not connected", "{0} pieces not connected", pieces.Count);
        }

        // Build's canvas names each short beam with a callout, so the chip only counts them.
        var tooShort = _build.Beams.Count(beam => IsTooShort(CreatureElementKind.Beam, beam.NodeA, beam.NodeB));
        var tooShortPistons = _build.Pistons.Count(piston => IsTooShort(CreatureElementKind.Piston, piston.NodeA, piston.NodeB));
        var tooShortSprings = _build.Springs.Count(spring => IsTooShort(CreatureElementKind.Spring, spring.NodeA, spring.NodeB));
        if ((tooShort, tooShortPistons, tooShortSprings) != (0, 0, 0))
        {
            return (tooShort, tooShortPistons, tooShortSprings) switch
            {
                (0, 0, _) => UiText.Counted("{0} spring too short", "{0} springs too short", tooShortSprings),
                (0, _, _) => UiText.Counted("{0} piston too short", "{0} pistons too short", tooShortPistons),
                _ => UiText.Counted("{0} beam too short", "{0} beams too short", tooShort),
            };
        }

        var zeroLength = _build.Beams.Count(beam => IsZeroLength(beam.NodeA, beam.NodeB))
            + _build.Pistons.Count(piston => IsZeroLength(piston.NodeA, piston.NodeB))
            + _build.Springs.Count(spring => IsZeroLength(spring.NodeA, spring.NodeB));
        if (zeroLength > 0)
        {
            return UiText.Counted("{0} link has zero length", "{0} links have zero length", zeroLength);
        }

        var missingALink = _build.HingeParts.Count(CreatureReadiness.IsMissingALink);
        return missingALink > 0
            ? UiText.Counted("{0} part missing a side", "{0} parts missing a side", missingALink)
            : errors[0];

        bool IsTooShort(CreatureElementKind kind, int nodeA, int nodeB) =>
            CreatureReadiness.IsTooShort(kind, NodeById(nodeA), NodeById(nodeB), _build.JointMotors);

        bool IsZeroLength(int nodeA, int nodeB) => NodeById(nodeA).Position == NodeById(nodeB).Position;
    }

    private NodeDef NodeById(int nodeId) => _build.Nodes[_build.NodeIndexOf(nodeId)];

    private BeamDef BeamById(int beamId) => _build.Beams[_build.BeamIndexOf(beamId)];

    private SensorDef SensorById(int sensorId) => _build.Sensors.First(sensor => sensor.Id == sensorId);

    // The "On this joint" tabs, top → down, for a joint with two or more parts.
    private IReadOnlyList<JointPartTab>? OnThisJoint(int nodeId)
    {
        var parts = CreatureDef.JointPartDefs(nodeId, _build.JointParts);
        return parts.Count < 2 ? null : [.. parts.Select(part => new JointPartTab(part.PartKind, CreatureElementSelection.Of(part)))];
    }

    private ServoDef ServoById(int servoId) => _build.Servos[_build.ServoIndexOf(servoId)];

    // The Fixed and Target pickers of a joint's hinge-part slot, the Servo's, Velocity motor's,
    // Brake's and Angle sensor's alike, also on a locked Creation.
    private IReadOnlyList<PartPickerPresentation> SidePickers(IHingePartDef part, UiText needsTwoSidesReason)
    {
        // A Wheel on the joint is a one-sided link, listed after the links.
        (int Id, CreatureElementKind Kind)[] links =
        [
            .. _build.LinksAt(part.NodeId).OrderBy(link => link.Id).Select(link => (link.Id, link.Kind)),
            .. _build.Wheels.Where(wheel => wheel.NodeId == part.NodeId).Select(wheel => (wheel.Id, CreatureElementKind.Wheel)),
        ];
        var twoLinksNote = _build.ServoNeedsTwoSides(part.NodeId) ? needsTwoSidesReason : null;

        // Picking the other role's link swaps the two only when both are set; with one empty, it just moves.
        var bothSet = part.FixedLinkId is not null && part.TargetLinkId is not null;
        return
        [
            Picker(UiText.Plain("Fixed part"), part.FixedLinkId, UiText.Plain("Pick a Fixed part"), ServoLinkRole.Target),
            Picker(UiText.Plain("Target part"), part.TargetLinkId, UiText.Plain("Pick a Target part"), ServoLinkRole.Fixed),
        ];

        PartPickerPresentation Picker(UiText label, int? selectedLinkId, UiText placeholder, ServoLinkRole otherRole)
        {
            var selected = selectedLinkId is { } id ? Array.FindIndex(links, link => link.Id == id) : -1;
            var chosen = selected >= 0;
            return new PartPickerPresentation(
                label,
                [.. links.Select(Option)],
                chosen ? selected : null,
                chosen ? null : twoLinksNote,
                chosen ? null : placeholder);

            PartPickerOption Option((int Id, CreatureElementKind Kind) link)
            {
                var role = link.Id == part.FixedLinkId ? ServoLinkRole.Fixed
                    : link.Id == part.TargetLinkId ? ServoLinkRole.Target
                    : ServoLinkRole.None;
                return new PartPickerOption(
                    link.Id,
                    _build.PartDisplayName(link.Id),
                    link.Kind,
                    role,
                    bothSet && role == otherRole ? UiText.Plain("swaps") : null);
            }
        }
    }

    private void SubscribeToBuild()
    {
        if (_isSubscribedToBuild)
        {
            return;
        }

        _build.AnatomyChanged += OnBuildChanged;
        _build.PropertyChanged += OnBuildChanged;
        _isSubscribedToBuild = true;
    }

    private void UnsubscribeFromBuild()
    {
        if (!_isSubscribedToBuild)
        {
            return;
        }

        _build.AnatomyChanged -= OnBuildChanged;
        _build.PropertyChanged -= OnBuildChanged;
        _isSubscribedToBuild = false;
    }

    private void OnBuildChanged(object? sender, EventArgs eventArgs)
    {
        _presentationChanged?.Invoke(this, EventArgs.Empty);
    }
}
