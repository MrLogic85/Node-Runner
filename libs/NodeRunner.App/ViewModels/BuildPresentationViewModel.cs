using NodeRunner.App.Lifecycle;
using NodeRunner.Domain;

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

    public IReadOnlyList<PartTrayGroup> PartGroups => PartTray.Groups();

    public LinkListPresentation? LinkList => ToolPanel.Mode == ToolPanelMode.LinkList
        ? BuildLinkList.Create(_build.PickedLink)
        : null;

    public ToolPanelPresentation ToolPanel => CreateToolPanel();

    public int NodeCount => _build.Nodes.Count;

    public int SensorCount => _build.Sensors.Count;

    public UiText TrainingSummaryTitle => _build.TrainingGeneration is { } generation
        ? UiText.Counted("Trained {0} generation", "Trained {0} generations", generation)
        : UiText.Plain("Not trained yet");

    public UiText TrainingSummaryBody => _build.TrainingGeneration is not null
        ? UiText.Plain("This can drop after a noisy generation; Training's Best never does. Tap the padlock to change the body. Training is kept.")
        : UiText.Plain("Start training when you are ready.");

    /// <summary>The Reset training dialog body (#687). Copy sits beside Reset in the overflow, so it is offered.</summary>
    public UiText ResetTrainingWarning =>
        UiText.Counted(
            "{1} forgets its {0} generation of training and keeps its body. Copy it first to keep the trained one.",
            "{1} forgets its {0} generations of training and keeps its body. Copy it first to keep the trained one.",
            _build.TrainingGeneration ?? 0,
            _build.CreationName);

    /// <summary>The latest generation's distance (#479); the best ever belongs to Stats.</summary>
    public UiText LatestDistanceText => _build.LatestDistance is { } distance
        ? UiText.Format("Latest distance {0}", Metres.WithUnit(distance))
        : UiText.Plain("Latest distance —");

    public int SelectedNodeCount => _build.SelectedNodeCount;

    public int SelectedBeamCount => _build.SelectedBeamCount;

    public int SelectedPartCount => _build.SelectedPartCount;

    /// <summary>The Part settings for the one selected part, or null unless exactly one part is selected.</summary>
    public PartSettingsPresentation? SinglePart
    {
        get
        {
            var canDelete = !_build.IsMoveOnly;
            if (_build.SingleSelectedPistonId is { } pistonId)
            {
                return new PartSettingsPresentation(
                    pistonId,
                    PartSettingsKind.Piston,
                    _build.PartDisplayName(pistonId),
                    _build.DefaultPartName(pistonId),
                    null,
                    null,
                    PistonNote,
                    canDelete,
                    PanelSliders());
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
                    SpringNote,
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
                    UiText.Plain("Drag its ends to change the length."),
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
                    UiText.Plain("Beams"),
                    ConnectedBeamText(nodeId),
                    UiText.Plain("Beams meet and turn here. Drag it to move them."),
                    canDelete,
                    PanelSliders());
            }

            return null;
        }
    }

    public static UiText PistonNote { get; } = UiText.Plain("The brain pushes it out and pulls it in, within its stroke.");

    public static UiText SpringNote { get; } = UiText.Plain("It pulls back toward its drawn length. Damping stops it bouncing.");

    /// <summary>A slider for each setting the selection can change in the panel (#704).</summary>
    private List<ParameterSlider> PanelSliders() =>
        [.. _build.EditableParameters
            .Where(id => PartParameters.Of(id).InPanel)
            .Select(id => PartParameters.SliderOver(id, _build.SelectedValuesOf(id)))];

    /// <summary>What a sensor does; an <paramref name="aimable"/> Camera's note also says what its Aim handle does (#594).</summary>
    public static UiText SensorNote(SensorKind kind, bool aimable) => kind switch
    {
        SensorKind.Accelerometer => UiText.Plain("Feels how its beam speeds up, slows down and tilts."),
        SensorKind.Camera => aimable
            ? UiText.Plain("Three rays see how near the ground is. Drag the round handle to aim it.")
            : UiText.Plain("Three rays see how near the ground is."),
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
                ? UiText.Plain("Beams on a deleted node go with it.")
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
                CanDelete: !_build.IsMoveOnly);
        }
    }

    public bool LockTopologyTools => _build.IsMoveOnly;

    /// <summary>True for a locked Creation: its anatomy is fixed and only moving nodes is allowed.</summary>
    public bool IsLocked => _build.IsMoveOnly;

    /// <summary>Whether the overflow's Undo can be tapped (#689).</summary>
    public bool CanUndo => _build.CanUndo;

    /// <summary>Whether the overflow's Redo can be tapped (#689).</summary>
    public bool CanRedo => _build.CanRedo;

    /// <summary>True when the Creation has saved training, locked or unlocked for this visit.</summary>
    public bool IsTrained => _build.TrainingGeneration is not null;

    public BuildPanelPresentation BuildPanel => CreateBuildPanel();

    private ToolPanelPresentation CreateToolPanel()
    {
        if (_build.SelectedPartCount > 0 || _build.IsMoveOnly)
        {
            return ToolPanelPresentation.None;
        }

        return ActiveTool switch
        {
            BuildTool.Parts => new ToolPanelPresentation(ToolPanelMode.PartsTray, UiText.Plain("Parts")),
            BuildTool.Beam => new ToolPanelPresentation(ToolPanelMode.LinkList, UiText.Plain("Beams")),
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
            : new BuildPanelPresentation(CanStartTraining: false, UiText.Plain("Add a piston"));
    }

    // A short form of the builder's errors for the readiness line; CreatureReadiness decides whether training may start.
    private UiText ShortReadiness(IReadOnlyList<UiText> errors)
    {
        if (errors.Count == 0)
        {
            return UiText.Plain("Add nodes + beams");
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
            return UiText.Counted("{0} node not connected", "{0} nodes not connected", unconnected);
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
            NodeById(nodeA).Position != NodeById(nodeB).Position
            && CreatureReadiness.IsTooShort(NodeById(nodeA), NodeById(nodeB));
    }

    private UiText ConnectedBeamText(int nodeId)
    {
        var connected = _build.Beams
            .Where(beam => beam.NodeA == nodeId || beam.NodeB == nodeId)
            .Select(beam => _build.PartDisplayName(beam.Id))
            .ToArray();
        return connected.Length == 0
            ? UiText.Plain("None yet")
            : connected.Skip(1).Aggregate(connected[0], (list, next) => UiText.Format("{0} · {1}", list, next));
    }

    private NodeDef NodeById(int nodeId) => _build.Nodes[_build.NodeIndexOf(nodeId)];

    private BeamDef BeamById(int beamId) => _build.Beams[_build.BeamIndexOf(beamId)];

    private SensorDef SensorById(int sensorId) => _build.Sensors.First(sensor => sensor.Id == sensorId);

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
