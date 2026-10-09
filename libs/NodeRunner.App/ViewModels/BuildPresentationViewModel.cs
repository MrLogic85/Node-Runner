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

    /// <summary>The Reset training dialog body (#687). Copy sits beside Reset in the overflow, so it is offered.</summary>
    public UiText ResetTrainingWarning =>
        UiText.Counted(
            "{1} forgets its {0} generation of training and keeps its body. Copy it first to keep the trained one.",
            "{1} forgets its {0} generations of training and keeps its body. Copy it first to keep the trained one.",
            _build.TrainingGeneration ?? 0,
            _build.CreationName);

    public int SelectedNodeCount => _build.SelectedNodeCount;

    public int SelectedBeamCount => _build.SelectedBeamCount;

    public int SelectedPartCount => _build.SelectedPartCount;

    /// <summary>The Part settings for the one selected part, or null unless exactly one part is selected.</summary>
    public PartSettingsPresentation? SinglePart
    {
        get
        {
            var canDelete = _build.DeleteLockedReason is null;
            if (_build.SingleSelectedServoId is { } servoId)
            {
                var servo = ServoById(servoId);
                return new PartSettingsPresentation(
                    servoId,
                    PartSettingsKind.Servo,
                    _build.PartDisplayName(servoId),
                    _build.DefaultPartName(servoId),
                    null,
                    null,
                    PartInfo.Servo,
                    canDelete,
                    PanelSliders(),
                    ServoPickers(servo),
                    servo.NodeId);
            }

            if (_build.SingleSelectedPistonId is { } pistonId)
            {
                return new PartSettingsPresentation(
                    pistonId,
                    PartSettingsKind.Piston,
                    _build.PartDisplayName(pistonId),
                    _build.DefaultPartName(pistonId),
                    null,
                    null,
                    PartInfo.Piston,
                    canDelete,
                    PanelSliders());
            }

            if (_build.SingleSelectedWheelId is { } wheelId)
            {
                var wheel = _build.Wheels[_build.WheelIndexOf(wheelId)];
                return new PartSettingsPresentation(
                    wheelId,
                    PartSettingsKind.Wheel,
                    _build.PartDisplayName(wheelId),
                    _build.DefaultPartName(wheelId),
                    null,
                    null,
                    PartInfo.Wheel,
                    canDelete,
                    PanelSliders(),
                    Readouts: [new PartReadout(UiText.Plain("Weight"), UiText.Format("{0} kg", new FixedNumber(Wheel.Mass(wheel), 1)))]);
            }

            if (_build.SingleSelectedSpringId is { } springId)
            {
                return new PartSettingsPresentation(
                    springId,
                    PartSettingsKind.Spring,
                    _build.PartDisplayName(springId),
                    _build.DefaultPartName(springId),
                    null,
                    null,
                    PartInfo.Spring,
                    canDelete,
                    PanelSliders());
            }

            if (_build.SingleSelectedSensorId is { } sensorId)
            {
                var sensor = SensorById(sensorId);
                return new PartSettingsPresentation(
                    sensorId,
                    sensor.Kind == SensorKind.Accelerometer ? PartSettingsKind.Accelerometer : PartSettingsKind.Camera,
                    _build.PartDisplayName(sensorId),
                    _build.DefaultPartName(sensorId),
                    UiText.Plain("On"),
                    _build.PartDisplayName(sensor.BeamId),
                    SensorNote(sensor.Kind, aimable: _build.AimableCameraId == sensorId),
                    canDelete,
                    PanelSliders());
            }

            if (_build.SingleSelectedBeamId is { } beamId)
            {
                var beam = BeamById(beamId);
                return new PartSettingsPresentation(
                    beamId,
                    PartSettingsKind.Beam,
                    _build.PartDisplayName(beamId),
                    _build.DefaultPartName(beamId),
                    UiText.Plain("Between"),
                    UiText.Format("{0} ↔ {1}", _build.PartDisplayName(beam.NodeA), _build.PartDisplayName(beam.NodeB)),
                    PartInfo.Beam,
                    canDelete,
                    PanelSliders());
            }

            if (_build.SingleSelectedNodeId is { } nodeId)
            {
                return new PartSettingsPresentation(
                    nodeId,
                    PartSettingsKind.Node,
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
    /// A slider for each setting the panel shows for the selection (#704), disabled where it can't
    /// change now: kept by a locked Creation, or doing nothing on these parts (#578).
    /// </summary>
    private List<ParameterSlider> PanelSliders() =>
        [.. _build.ShownParameters
            .Where(id => PartParameters.Of(id).InPanel)
            .Select(id => PartParameters.SliderOver(id, _build.SelectedValuesOf(id)) with
            {
                Locked = !_build.CanEdit(id),
                NoEffect = !_build.HasEffect(id),
            })];

    /// <summary>What a sensor does; an <paramref name="aimable"/> Camera's note also says what its Aim handle does (#594).</summary>
    public static UiText SensorNote(SensorKind kind, bool aimable) => kind switch
    {
        SensorKind.Accelerometer => PartInfo.Accelerometer,
        SensorKind.Camera => aimable
            ? UiText.Plain("Its rays see how near the ground is. Drag the round handle to aim it.")
            : PartInfo.Camera,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The selection panel, or null unless several parts are selected (#704).</summary>
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
            var deleteNote = selection.Nodes.Count > 0
                ? UiText.Plain("Links on a deleted joint go with it.")
                : _build.Sensors.Any(sensor => selection.Beams.Contains(sensor.BeamId) && !selection.Sensors.Contains(sensor.Id))
                    ? UiText.Plain("A sensor on a deleted beam goes with it.")
                    : null;
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

    /// <summary>Whether the part and selection panels show their Advanced settings (#903).</summary>
    public bool AdvancedSettingsOpen => _build.AdvancedSettingsOpen;

    /// <summary>True for a locked Creation: it refuses every edit that would change its model (#896).</summary>
    public bool IsLocked => _build.IsLocked;

    /// <summary>Whether the overflow's Undo can be tapped (#689).</summary>
    public bool CanUndo => _build.CanUndo;

    /// <summary>Whether the overflow's Redo can be tapped (#689).</summary>
    public bool CanRedo => _build.CanRedo;

    /// <summary>True when the Creation has saved training, locked or unlocked for this visit.</summary>
    public bool IsTrained => _build.TrainingGeneration is not null;

    /// <summary>
    /// Whether the overflow offers Copy creation (#840) and Share build (#899): anything drawn,
    /// trained or not. An empty creation has nothing to copy or share.
    /// </summary>
    public bool CanCopy => _build.Nodes.Count > 0;

    public BuildPanelPresentation BuildPanel => CreateBuildPanel();

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

    private BuildPanelPresentation CreateBuildPanel()
    {
        if (!_build.TryLeave(out var creature, out var errors) || creature is null)
        {
            return new BuildPanelPresentation(CanStartTraining: false, ShortReadiness(errors));
        }

        return CreatureReadiness.CanTrain(creature)
            ? new BuildPanelPresentation(CanStartTraining: true, UiText.Plain("Ready to train"))
            : new BuildPanelPresentation(CanStartTraining: false, ShortReadiness(CreatureReadiness.Problems(creature)));
    }

    // A short form of the builder's errors for the readiness line; CreatureReadiness decides whether training may start.
    private UiText ShortReadiness(IReadOnlyList<UiText> errors)
    {
        if (errors.Count == 0)
        {
            return UiText.Plain("Add joints and links");
        }

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

        // Build's canvas names each short beam with a callout (#593), so the line only counts them.
        var tooShort = _build.Beams.Count(beam => IsTooShort(beam.NodeA, beam.NodeB));
        var tooShortPistons = _build.Pistons.Count(piston => IsTooShort(piston.NodeA, piston.NodeB));
        var tooShortSprings = _build.Springs.Count(spring => IsTooShort(spring.NodeA, spring.NodeB));
        return (tooShort, tooShortPistons, tooShortSprings) switch
        {
            (0, 0, 0) => errors[0],
            (0, 0, _) => UiText.Counted("{0} spring too short", "{0} springs too short", tooShortSprings),
            (0, _, _) => UiText.Counted("{0} piston too short", "{0} pistons too short", tooShortPistons),
            _ => UiText.Counted("{0} beam too short", "{0} beams too short", tooShort),
        };

        bool IsTooShort(int nodeA, int nodeB) =>
            CreatureReadiness.IsTooShort(NodeById(nodeA), NodeById(nodeB), _build.NodeRadius(nodeA), _build.NodeRadius(nodeB));
    }

    private NodeDef NodeById(int nodeId) => _build.Nodes[_build.NodeIndexOf(nodeId)];

    private BeamDef BeamById(int beamId) => _build.Beams[_build.BeamIndexOf(beamId)];

    private SensorDef SensorById(int sensorId) => _build.Sensors.First(sensor => sensor.Id == sensorId);

    private ServoDef ServoById(int servoId) => _build.Servos[_build.ServoIndexOf(servoId)];

    private IReadOnlyList<PartPickerPresentation> ServoPickers(ServoDef servo)
    {
        var links = _build.LinksAt(servo.NodeId)
            .OrderBy(link => link.Id)
            .ToArray();
        var options = links.Select(link => _build.PartDisplayName(link.Id)).ToArray();
        var trainedNote = !_build.IsLocked && _build.TrainingGeneration is not null ? UiText.Plain("Changing these makes it learn again.") : null;
        var twoLinksNote = _build.ServoNeedsTwoLinks(servo.NodeId) ? CreatureBuilder.ServoNeedsTwoLinksReason : null;
        return
        [
            Picker(UiText.Plain("Fixed link"), servo.FixedLinkId, UiText.Plain("Pick a Fixed link")),
            Picker(UiText.Plain("Target link"), servo.TargetLinkId, UiText.Plain("Pick a Target link")),
        ];

        PartPickerPresentation Picker(UiText label, int? selectedLinkId, UiText placeholder)
        {
            var selected = selectedLinkId is { } id ? Array.FindIndex(links, link => link.Id == id) : -1;
            var chosen = selected >= 0;
            return new PartPickerPresentation(
                label,
                links.Select(link => link.Id).ToArray(),
                options,
                chosen ? selected : null,
                _build.IsLocked,
                _build.IsLocked ? null : chosen ? trainedNote : twoLinksNote,
                links.Select(link => link.Kind).ToArray(),
                chosen ? null : placeholder);
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
