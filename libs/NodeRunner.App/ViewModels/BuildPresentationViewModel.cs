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
    private const int _coreSensorValueCount = 6;
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

    public int NodeCount => _build.Nodes.Count;

    public int CoreCount => _build.Cores.Count;

    public int MaxCores => _build.MaxCores;

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

    public int SelectedCoreCount => _build.SelectedCoreCount;

    public string SinglePartTitle => _build.SingleSelectedBeamIndex is { } beamIndex
        ? $"Beam {beamIndex + 1}"
        : _build.SingleSelectedNodeIndex is { } index
        ? _build.SingleSelectionHasCore ? $"Core · Node {index + 1}" : $"Node {index + 1}"
        : "Part";

    public string SinglePartBody => _build.SingleSelectedBeamIndex is not null
        ? _build.IsMoveOnly
            ? "Select and move an endpoint Node to reposition it. The Beam follows its Nodes."
            : "Move either endpoint Node to change the Beam length."
        : _build.SingleSelectionHasCore
            ? "The Core contributes six sensor inputs. Move its Node to reposition it."
            : "Move the Node to change its position and connected Beam lengths.";

    public string SinglePartPrimaryLabel => _build.SingleSelectedBeamIndex is not null
        ? "Length"
        : _build.SingleSelectionHasCore
            ? "Built-in senses"
            : "Position";

    public string SinglePartPrimaryValue => _build.SingleSelectedBeamIndex is { } beamIndex
        ? $"{BeamLength(beamIndex):0.0} units"
        : _build.SingleSelectedNodeIndex is { } nodeIndex
            ? _build.SingleSelectionHasCore
                ? "6 inputs"
                : $"{_build.Nodes[nodeIndex].Position.X:0}, {_build.Nodes[nodeIndex].Position.Y:0}"
            : "—";

    public string SinglePartConnectionsLabel => _build.SingleSelectedBeamIndex is not null
        ? "Between"
        : _build.SingleSelectionHasCore
            ? "Mounted on"
            : "Connections";

    public string SinglePartConnectionsValue => _build.SingleSelectedBeamIndex is { } beamIndex
        ? $"Node {_build.Beams[beamIndex].NodeA + 1} ↔ Node {_build.Beams[beamIndex].NodeB + 1}"
        : _build.SingleSelectedNodeIndex is { } nodeIndex
            ? _build.SingleSelectionHasCore
                ? $"Node {nodeIndex + 1}"
                : ConnectedBeamText(nodeIndex)
            : "—";

    public string SinglePartFacts => _build.SingleSelectedBeamIndex is not null
        ? "Rigid connection"
        : _build.SingleSelectionHasCore
            ? "Down ray · Forward ray · Forward-down ray · Pitch · Elevation · Speed"
            : _build.SingleSelectedNodeIndex is { } nodeIndex
                ? $"Radius {_build.Nodes[nodeIndex].Radius:0.0} · {_build.Beams.Count(beam => beam.NodeA == nodeIndex || beam.NodeB == nodeIndex)} attached Beam(s)"
                : string.Empty;

    public string MultiSelectionTitle => $"{_build.SelectedPartCount} selected";

    public string MultiSelectionCounts => _build.SelectedCoreCount > 0
        ? $"Nodes · {_build.SelectedNodeCount}    Core · {_build.SelectedCoreCount}"
        : _build.SelectedBeamCount > 0
        ? $"Beam · {_build.SelectedBeamCount}"
        : $"Nodes · {_build.SelectedNodeCount}";

    public string MultiSelectionBody => "Drag any selected part to move them together. Parts are locked, so this selection can only be moved.";

    public bool LockTopologyTools => _build.IsMoveOnly;

    public string CoreToolText => _build.IsMoveOnly ? "Core · locked" : BuildCoreToolText();

    /// <summary>True for a locked Creation: its anatomy is fixed and only moving nodes is allowed.</summary>
    public bool IsLocked => _build.IsMoveOnly;

    public bool ShowRebuildAction => _build.IsMoveOnly;

    public string RebuildActionText => "Rebuild body";

    public string RebuildConfirmationTitle => "Rebuild body?";

    public string RebuildConfirmationBody => "Rebuild creates a new body and a new brain. The original Creation and its training stay unchanged.";

    public string CoreToolTooltip => _build.IsMoveOnly
        ? MoveOnlyLockReason
        : _build.MaxCores > 1
        ? "Attach or remove a core. Extra core slot unlocked."
        : "Attach or remove a core. Train to unlock a second core slot.";

    public BuildPanelPresentation BuildPanel => CreateBuildPanel();

    public static string ToolHint(BuildTool tool)
    {
        return tool switch
        {
            BuildTool.Move => "Drag a joint to move it. Tap a part to select it.",
            BuildTool.Beam => "Drag from one joint to another to join them with a beam.",
            BuildTool.Joint => "Tap empty space to add a joint, or tap a beam to split it.",
            BuildTool.Select => "Tap parts to select them. Drag selected parts to move them together.",
            BuildTool.Core => "Tap a node to attach a core, tap again to remove it.",
            _ => string.Empty,
        };
    }

    private BuildPanelPresentation CreateBuildPanel()
    {
        if (!_build.TryLeave(out var creature, out var errors) || creature is null)
        {
            var inputSummary = errors.Count > 0
                ? BuildInvalidDraftInputSummary(_build.Cores.Count)
                : BuildInputSummary(_build.Cores.Count, motorRelationCount: 0);
            var motorRelationSummary = errors.Count > 0
                ? "Fix anatomy to count motor relations."
                : "Two beams at one node create a motor relation; closed triangles do not twist.";
            return new BuildPanelPresentation(
                inputSummary,
                motorRelationSummary,
                CanStartTraining: false,
                ReadinessText: ShortReadiness(errors),
                InputCount: _build.Cores.Count * _coreSensorValueCount,
                OutputCount: 0);
        }

        var motorRelationCount = MotorTopology.BuildNodeConnections(creature)
            .Count(connection => connection.IsMotorized);
        var inputCount = BuildInputCount(creature.Cores.Count, motorRelationCount);
        if (!CreatureReadiness.CanTrain(creature))
        {
            return new BuildPanelPresentation(
                BuildInputSummary(creature.Cores.Count, motorRelationCount),
                "0 motor relations can twist",
                CanStartTraining: false,
                ReadinessText: "Add a two-beam node",
                InputCount: inputCount,
                OutputCount: 0);
        }

        return new BuildPanelPresentation(
            BuildInputSummary(creature.Cores.Count, motorRelationCount),
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
            .Count(node => !_build.Beams.Any(beam => beam.NodeA == node || beam.NodeB == node));
        return unconnected switch
        {
            0 => errors[0],
            1 => "1 node not connected",
            _ => $"{unconnected} nodes not connected",
        };
    }

    private string BuildCoreToolText()
    {
        var unlockHint = _build.MaxCores > 1 ? "unlocked" : "50 fitness";
        return $"Core {_build.Cores.Count}/{_build.MaxCores} ({unlockHint})";
    }

    private double BeamLength(int beamIndex)
    {
        var beam = _build.Beams[beamIndex];
        var start = _build.Nodes[beam.NodeA].Position;
        var end = _build.Nodes[beam.NodeB].Position;
        var deltaX = end.X - start.X;
        var deltaY = end.Y - start.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private string ConnectedBeamText(int nodeIndex)
    {
        var connected = _build.Beams
            .Select((beam, index) => (beam, index))
            .Where(item => item.beam.NodeA == nodeIndex || item.beam.NodeB == nodeIndex)
            .Select(item => $"Beam {item.index + 1}")
            .ToArray();
        return connected.Length == 0 ? "No Beams" : string.Join(" · ", connected);
    }

    private static string BuildInputSummary(int coreCount, int motorRelationCount)
    {
        var inputCount = BuildInputCount(coreCount, motorRelationCount);
        var coreSensorCount = coreCount * _coreSensorValueCount;
        var motorSensorCount = motorRelationCount * _motorRelationSensorValueCount;
        var coreWord = coreCount == 1 ? "core" : "cores";
        var relationWord = motorRelationCount == 1 ? "motor relation" : "motor relations";
        var coreSensorWord = coreSensorCount == 1 ? "sensor" : "sensors";
        var motorSensorWord = motorSensorCount == 1 ? "sensor" : "sensors";
        return $"{coreCount} {coreWord}: {coreSensorCount} {coreSensorWord}; {motorRelationCount} {relationWord}: {motorSensorCount} {motorSensorWord}; {inputCount} inputs total";
    }

    private static int BuildInputCount(int coreCount, int motorRelationCount)
    {
        return (coreCount * _coreSensorValueCount) + (motorRelationCount * _motorRelationSensorValueCount);
    }

    private static string BuildInvalidDraftInputSummary(int coreCount)
    {
        var coreWord = coreCount == 1 ? "core" : "cores";
        return $"{coreCount} {coreWord} placed; fix anatomy to count inputs.";
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
