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
    private const int _motorRelationSensorValueCount = 2;

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
        _ => string.Empty,
    };

    public int NodeCount => _build.Nodes.Count;

    public int SensorCount => _build.Sensors.Count;

    public BrainShapeDef BrainShape => _build.BrainShape;

    public BrainSetupPresentation BrainSetup
    {
        get
        {
            var buildPanel = BuildPanel;
            return BrainSetupPresentation.For(BrainShape, buildPanel.InputCount, buildPanel.OutputCount);
        }
    }

    public bool IsBrainShapeLocked => _build.IsMoveOnly;

    public string MoveOnlyLockReason => "Move only · training kept";

    public string TrainingSummaryTitle => _build.TrainingGeneration is { } generation
        ? $"Trained {generation} generations"
        : "Not trained yet";

    public string TrainingSummaryBody => _build.TrainingGeneration is { } generation
        ? $"Generation {generation}. Best distance {BestDistanceText}. Anatomy is locked so this brain stays valid."
        : "Start training when you are ready. Parts are locked so the brain stays valid.";

    public string BestDistanceText => _build.BestFitness is { } bestFitness
        ? $"{bestFitness:0.0} m"
        : "—";

    public int SelectedNodeCount => _build.SelectedNodeCount;

    public int SelectedBeamCount => _build.SelectedBeamCount;

    public int SelectedPartCount => _build.SelectedPartCount;

    /// <summary>The Part settings for the one selected part, or null unless exactly one part is selected.</summary>
    public PartSettingsPresentation? SinglePart
    {
        get
        {
            var canDelete = !_build.IsMoveOnly;
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
                    canDelete);
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
                    canDelete);
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
                    canDelete);
            }

            return null;
        }
    }

    /// <summary>Added to an unlocked Camera's note: what its Aim handle does (#594).</summary>
    public const string AimNote = "Drag the round handle to aim it.";

    public static string SensorNote(SensorKind kind) => kind switch
    {
        SensorKind.Accelerometer => "Feels how its beam speeds up, slows down and tilts.",
        SensorKind.Camera => "Three rays see how near the ground is.",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The selection panel, or null unless several parts are selected.</summary>
    public SelectionPanelPresentation? Selection => _build.SelectedPartCount > 1
        ? new SelectionPanelPresentation(
            $"{_build.SelectedPartCount} selected",
            $"Delete {_build.SelectedPartCount}",
            "Beams on a deleted node go with it.",
            CanDelete: !_build.IsMoveOnly)
        : null;

    public bool LockTopologyTools => _build.IsMoveOnly;

    /// <summary>True for a locked Creation: its anatomy is fixed and only moving nodes is allowed.</summary>
    public bool IsLocked => _build.IsMoveOnly;

    public bool ShowRebuildAction => _build.IsMoveOnly;

    public string RebuildActionText => "Rebuild body";

    public string RebuildConfirmationTitle => "Rebuild body?";

    public string RebuildConfirmationBody => "Rebuild creates a new body and a new brain. The original Creation and its training stay unchanged.";

    public BuildPanelPresentation BuildPanel => CreateBuildPanel();

    public static string ToolHint(BuildTool tool)
    {
        return tool switch
        {
            BuildTool.Move => "Drag a joint to move it. Tap a part to select it.",
            BuildTool.Beam => "Drag from one joint to another to join them with a beam.",
            BuildTool.Joint => "Tap empty space to add a joint, or tap a beam to split it.",
            BuildTool.Select => "Tap parts to select them. Drag selected parts to move them together.",
            _ => string.Empty,
        };
    }

    private BuildPanelPresentation CreateBuildPanel()
    {
        if (!_build.TryLeave(out var creature, out var errors) || creature is null)
        {
            var inputSummary = errors.Count > 0
                ? BuildInvalidDraftInputSummary(_build.Sensors.Count)
                : BuildInputSummary(_build.Sensors, motorRelationCount: 0);
            var motorRelationSummary = errors.Count > 0
                ? "Fix anatomy to count motor relations."
                : "Two beams at one node create a motor relation; closed triangles do not twist.";
            return new BuildPanelPresentation(
                inputSummary,
                motorRelationSummary,
                CanStartTraining: false,
                ReadinessText: ShortReadiness(errors),
                InputCount: SensorInputCount(_build.Sensors),
                OutputCount: 0);
        }

        var motorRelationCount = MotorTopology.BuildNodeConnections(creature)
            .Count(connection => connection.IsMotorized);
        var inputCount = SensorInputCount(creature.Sensors) + (motorRelationCount * _motorRelationSensorValueCount);
        if (!CreatureReadiness.CanTrain(creature))
        {
            return new BuildPanelPresentation(
                BuildInputSummary(creature.Sensors, motorRelationCount),
                "0 motor relations can twist",
                CanStartTraining: false,
                ReadinessText: "Add a two-beam node",
                InputCount: inputCount,
                OutputCount: 0);
        }

        return new BuildPanelPresentation(
            BuildInputSummary(creature.Sensors, motorRelationCount),
            motorRelationCount == 1 ? "1 motor relation can twist" : $"{motorRelationCount} motor relations can twist",
            CanStartTraining: true,
            ReadinessText: "Ready to train",
            InputCount: inputCount,
            OutputCount: motorRelationCount);
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
                return !_build.Beams.Any(beam => beam.NodeA == nodeId || beam.NodeB == nodeId);
            });
        if (unconnected > 0)
        {
            return unconnected == 1 ? "1 node not connected" : $"{unconnected} nodes not connected";
        }

        // Build's canvas names each short beam with a callout (#593), so the line only counts them.
        var tooShort = _build.Beams.Count(beam =>
            NodeById(beam.NodeA).Position != NodeById(beam.NodeB).Position
            && CreatureReadiness.IsTooShort(NodeById(beam.NodeA), NodeById(beam.NodeB)));
        return tooShort switch
        {
            0 => errors[0],
            1 => "1 beam too short",
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

    private static string BuildInputSummary(IReadOnlyList<SensorDef> sensors, int motorRelationCount)
    {
        var sensorInputCount = SensorInputCount(sensors);
        var motorInputCount = motorRelationCount * _motorRelationSensorValueCount;
        var inputCount = sensorInputCount + motorInputCount;
        var sensorWord = sensors.Count == 1 ? "sensor" : "sensors";
        var relationWord = motorRelationCount == 1 ? "motor relation" : "motor relations";
        var sensorInputWord = sensorInputCount == 1 ? "input" : "inputs";
        var motorInputWord = motorInputCount == 1 ? "input" : "inputs";
        return $"{sensors.Count} {sensorWord}: {sensorInputCount} {sensorInputWord}; {motorRelationCount} {relationWord}: {motorInputCount} {motorInputWord}; {inputCount} inputs total";
    }

    private static int SensorInputCount(IReadOnlyList<SensorDef> sensors) =>
        sensors.Sum(sensor => sensor.Kind switch
        {
            SensorKind.Accelerometer => Accelerometer.ReadingNames.Count,
            SensorKind.Camera => CameraRays.RayCount,
            _ => throw new InvalidOperationException($"Unknown sensor kind {sensor.Kind}."),
        });

    private static string BuildInvalidDraftInputSummary(int sensorCount)
    {
        var sensorWord = sensorCount == 1 ? "sensor" : "sensors";
        return $"{sensorCount} {sensorWord} placed; fix anatomy to count inputs.";
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
