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

    public string BuildModeButtonText => _build.IsActive ? "Simulate" : "Build";

    public string InspectorTitle => "Building";

    public string CreationName => _build.CreationName;

    public string InspectorRole => _build.IsMoveOnly ? "Tool: Move" : $"Tool: {_build.ActiveTool}";

    public string InspectorValues => _build.StatusMessage ?? (_build.IsMoveOnly
        ? "Drag an existing node to reposition it. Training is kept."
        : ToolHint(_build.ActiveTool));

    public BuildTool ActiveTool => _build.ActiveTool;

    public IReadOnlyList<PartTrayGroup> PartGroups => PartTray.Groups();

    /// <summary>
    /// The side panel's one-line status for the rail tools that need one (Beam, Joint, Select);
    /// empty for Move and tools without a short side-panel hint.
    /// </summary>
    public string PanelToolHint => ActiveTool switch
    {
        BuildTool.Beam => "Drag joint to joint.",
        BuildTool.Joint => "Tap space or a beam.",
        BuildTool.Select => "Tap or box parts.",
        BuildTool.Piston => "Drag joint to joint.",
        _ => string.Empty,
    };

    public int NodeCount => _build.Nodes.Count;

    public int SensorCount => _build.Sensors.Count;

    public string MoveOnlyLockReason => "Move only · training kept";

    public string TrainingSummaryTitle => _build.TrainingGeneration is { } generation
        ? $"Trained {CreationCardPresentation.FormatCount(generation, "generation")}"
        : "Not trained yet";

    public string TrainingSummaryBody => _build.TrainingGeneration is not null
        ? "This can drop after a noisy generation; Training's Best never does. Tap the padlock to change the body. Training is kept."
        : "Start training when you are ready.";

    /// <summary>The Reset training dialog body (#687). Copy sits beside Reset in the overflow, so it is offered.</summary>
    public string ResetTrainingWarning =>
        $"{_build.CreationName} forgets its {CreationCardPresentation.FormatCount(_build.TrainingGeneration ?? 0, "generation")} of training and keeps its body. Copy it first to keep the trained one.";

    /// <summary>The latest generation's distance (#479); the best ever belongs to Stats.</summary>
    public string LatestDistanceText => $"Latest distance {(_build.LatestDistance is { } distance ? Metres.FormatWithUnit(distance) : "—")}";

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
                    string.Empty,
                    string.Empty,
                    PistonNote,
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
                    "On",
                    _build.PartDisplayName(sensor.BeamId),
                    _build.AimableCameraId == sensorId ? $"{SensorNote(sensor.Kind)} {AimNote}" : SensorNote(sensor.Kind),
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
                    "Between",
                    $"{_build.PartDisplayName(beam.NodeA)} ↔ {_build.PartDisplayName(beam.NodeB)}",
                    "Drag its ends to change the length.",
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
                    "Beams",
                    ConnectedBeamText(nodeId),
                    "Beams meet and turn here. Drag it to move them.",
                    canDelete,
                    PanelSliders());
            }

            return null;
        }
    }

    public const string PistonNote = "The brain pushes it out and pulls it in, within its stroke.";

    /// <summary>A slider for each setting the selection can change in the panel (#704).</summary>
    private List<ParameterSlider> PanelSliders() =>
        [.. _build.EditableParameters
            .Where(id => PartParameters.Of(id).InPanel)
            .Select(id => PartParameters.SliderOver(id, _build.SelectedValuesOf(id)))];

    /// <summary>Added to an unlocked Camera's note: what its Aim handle does (#594).</summary>
    public const string AimNote = "Drag the round handle to aim it.";

    public static string SensorNote(SensorKind kind) => kind switch
    {
        SensorKind.Accelerometer => "Feels how its beam speeds up, slows down and tilts.",
        SensorKind.Camera => "Three rays see how near the ground is.",
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
                ? "Beams on a deleted node go with it."
                : _build.Sensors.Any(sensor => selection.Beams.Contains(sensor.BeamId) && !selection.Sensors.Contains(sensor.Id))
                    ? "A sensor on a deleted beam goes with it."
                    : string.Empty;
            return new SelectionPanelPresentation(
                $"{count} selected",
                settings,
                settings.Count > 0 ? "A slider sets one value for all of them." : string.Empty,
                settings.Count == 0 && !showFrameRows ? "These parts share no settings." : string.Empty,
                showFrameRows,
                $"Delete {count}",
                deleteNote,
                CanDelete: !_build.IsMoveOnly);
        }
    }

    public bool LockTopologyTools => _build.IsMoveOnly;

    /// <summary>True for a locked Creation: its anatomy is fixed and only moving nodes is allowed.</summary>
    public bool IsLocked => _build.IsMoveOnly;

    /// <summary>True when the Creation has saved training, locked or unlocked for this visit.</summary>
    public bool IsTrained => _build.TrainingGeneration is not null;

    public BuildPanelPresentation BuildPanel => CreateBuildPanel();

    public static string ToolHint(BuildTool tool)
    {
        return tool switch
        {
            BuildTool.Move => "Drag a joint to move it. Tap a part to select it.",
            BuildTool.Beam => "Drag from one joint to another to join them with a beam.",
            BuildTool.Joint => "Tap empty space to add a joint, or tap a beam to split it.",
            BuildTool.Select => "Tap parts to select them. Drag selected parts to move them together.",
            BuildTool.Piston => "Drag from one joint to another to place a piston.",
            _ => string.Empty,
        };
    }

    private BuildPanelPresentation CreateBuildPanel()
    {
        if (!_build.TryLeave(out var creature, out var errors) || creature is null)
        {
            return new BuildPanelPresentation(CanStartTraining: false, ShortReadiness(errors));
        }

        return CreatureReadiness.CanTrain(creature)
            ? new BuildPanelPresentation(CanStartTraining: true, "Ready to train")
            : new BuildPanelPresentation(CanStartTraining: false, "Add a piston");
    }

    // A short form of the builder's errors for the readiness line; CreatureReadiness decides whether training may start.
    private string ShortReadiness(IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
        {
            return "Add nodes + beams";
        }

        var unconnected = Enumerable.Range(0, _build.Nodes.Count)
            .Count(index =>
            {
                var nodeId = _build.Nodes[index].Id;
                return !_build.Beams.Any(beam => beam.NodeA == nodeId || beam.NodeB == nodeId)
                    && !_build.Pistons.Any(piston => piston.NodeA == nodeId || piston.NodeB == nodeId);
            });
        if (unconnected > 0)
        {
            return unconnected == 1 ? "1 node not connected" : $"{unconnected} nodes not connected";
        }

        // Build's canvas names each short beam with a callout (#593), so the line only counts them.
        var tooShort = _build.Beams.Count(beam =>
            NodeById(beam.NodeA).Position != NodeById(beam.NodeB).Position
            && CreatureReadiness.IsTooShort(NodeById(beam.NodeA), NodeById(beam.NodeB)));
        var tooShortPistons = _build.Pistons.Count(piston =>
            NodeById(piston.NodeA).Position != NodeById(piston.NodeB).Position
            && CreatureReadiness.IsTooShort(NodeById(piston.NodeA), NodeById(piston.NodeB)));
        return (tooShort, tooShortPistons) switch
        {
            (0, 0) => errors[0],
            (0, 1) => "1 piston too short",
            (0, _) => $"{tooShortPistons} pistons too short",
            (1, _) => "1 beam too short",
            _ => $"{tooShort} beams too short",
        };
    }

    private string ConnectedBeamText(int nodeId)
    {
        var connected = _build.Beams
            .Where(beam => beam.NodeA == nodeId || beam.NodeB == nodeId)
            .Select(beam => _build.PartDisplayName(beam.Id))
            .ToArray();
        return connected.Length == 0 ? "None yet" : string.Join(" · ", connected);
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
