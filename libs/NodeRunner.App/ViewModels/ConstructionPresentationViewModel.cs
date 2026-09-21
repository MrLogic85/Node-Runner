using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// Pure presentation adapter for construction-mode shell state. Godot owns
/// nodes/input/rendering; this class owns stable labels, hints, and command
/// visibility so target screens can reuse the same contract without copying
/// `Main.cs` formatting rules.
/// </summary>
public sealed class ConstructionPresentationViewModel
{
    private const int _coreSensorValueCount = 6;
    private const int _motorRelationSensorValueCount = 2;

    private readonly ConstructionViewModel _construction;
    private EventHandler? _presentationChanged;
    private bool _isSubscribedToConstruction;

    public ConstructionPresentationViewModel(ConstructionViewModel construction)
    {
        _construction = construction ?? throw new ArgumentNullException(nameof(construction));
    }

    public event EventHandler? PresentationChanged
    {
        add
        {
            _presentationChanged += value;
            SubscribeToConstruction();
        }
        remove
        {
            _presentationChanged -= value;
            if (_presentationChanged is null)
            {
                UnsubscribeFromConstruction();
            }
        }
    }

    public string BuildModeButtonText => _construction.IsActive ? "Simulate" : "Build";

    public string InspectorTitle => "Building";

    public string CreationName => _construction.CreationName;

    public string CreationSubtitle => _construction.IsMoveOnly
        ? "Saved Creation · anatomy locked"
        : "Unsaved anatomy draft";

    public string InspectorRole => _construction.IsMoveOnly ? "Tool: Move" : $"Tool: {_construction.ActiveTool}";

    public string InspectorValues => _construction.StatusMessage ?? (_construction.IsMoveOnly
        ? "Drag an existing node to reposition it. Training is kept."
        : ToolHint(_construction.ActiveTool));

    public ConstructionTool ActiveTool => _construction.ActiveTool;

    public int NodeCount => _construction.Nodes.Count;

    public int CoreCount => _construction.Cores.Count;

    public int MaxCores => _construction.MaxCores;

    public BrainShapeDef BrainShape => _construction.HasCustomBrainShape
        ? _construction.BrainShape
        : new BrainShapeDef(BrainShapeDef.DefaultHiddenLayers, RecommendedNeurons(BuildPanel));

    public bool IsBrainShapeLocked => _construction.IsMoveOnly;

    public string MoveOnlyLockReason => "Move only · training kept";

    public string PartsLockedChipText => "Parts locked · drag to move";

    public string TrainingSummaryTitle => _construction.TrainingGeneration is { } generation
        ? $"Trained {generation} generations"
        : "Not trained yet";

    public string TrainingSummaryBody => _construction.TrainingGeneration is { } generation
        ? $"Generation {generation}. Best distance {BestDistanceText}. Anatomy is locked so this brain stays valid."
        : "Start training when you are ready. Anatomy is locked after Save.";

    public string BestDistanceText => _construction.BestFitness is { } bestFitness
        ? $"{bestFitness:0.0} m"
        : "—";

    public int SelectedNodeCount => _construction.SelectedNodeCount;

    public int SelectedCoreCount => _construction.SelectedCoreCount;

    public string SinglePartTitle => _construction.SingleSelectedNodeIndex is { } index
        ? _construction.SingleSelectionHasCore ? $"Core · Node {index + 1}" : $"Node {index + 1}"
        : "Part";

    public string SinglePartBody => "Position can be moved. Structural settings are locked after Save.";

    public string MultiSelectionTitle => $"{_construction.SelectedNodeCount} selected";

    public string MultiSelectionCounts => _construction.SelectedCoreCount > 0
        ? $"Nodes · {_construction.SelectedNodeCount}    Core · {_construction.SelectedCoreCount}"
        : $"Nodes · {_construction.SelectedNodeCount}";

    public string MultiSelectionBody => "Drag any selected part to move them together. Parts are locked, so this selection can only be moved.";

    public string LockedTopologyToolsText => $"Beam, Core, Delete locked: {MoveOnlyLockReason}";

    public string PlaceToolText => _construction.IsMoveOnly ? "Move" : "Place";

    public bool LockTopologyTools => _construction.IsMoveOnly;

    public string BeamToolText => _construction.IsMoveOnly ? "Beam · locked" : "Beam";

    public string CoreToolText => _construction.IsMoveOnly ? "Core · locked" : BuildCoreToolText();

    public string DeleteToolText => _construction.IsMoveOnly ? "Delete · locked" : "Delete";

    public string SelectToolText => "Select";

    public bool ShowCompleteAction => !_construction.IsMoveOnly;

    public bool ShowRebuildAction => _construction.IsMoveOnly;

    public string RebuildActionText => "Rebuild body";

    public string RebuildConfirmationTitle => "Rebuild body?";

    public string RebuildConfirmationBody => "Rebuild creates a new body and a new brain. The original Creation and its training stay unchanged.";

    public string CoreToolTooltip => _construction.IsMoveOnly
        ? MoveOnlyLockReason
        : _construction.MaxCores > 1
        ? "Attach or remove a core. Extra core slot unlocked."
        : "Attach or remove a core. Train to unlock a second core slot.";

    public ConstructionBuildPanelPresentation BuildPanel => CreateBuildPanel();

    public static string ToolHint(ConstructionTool tool)
    {
        return tool switch
        {
            ConstructionTool.Place => "Tap empty space to place a node. Drag a node to move it.",
            ConstructionTool.Beam => "Tap a node, then another node, to connect them with a beam.",
            ConstructionTool.Select => "Tap parts to select them. Drag selected parts to move them together.",
            ConstructionTool.Core => "Tap a node to attach a core, tap again to remove it.",
            ConstructionTool.Delete => "Tap a node or beam to delete it.",
            _ => string.Empty,
        };
    }

    private ConstructionBuildPanelPresentation CreateBuildPanel()
    {
        if (!_construction.TryLeave(out var creature, out var errors) || creature is null)
        {
            var disabledReason = errors.Count > 0
                ? errors[0]
                : "Add nodes and beams before training a new creature.";
            var inputSummary = errors.Count > 0
                ? BuildInvalidDraftInputSummary(_construction.Cores.Count)
                : BuildInputSummary(_construction.Cores.Count, motorRelationCount: 0);
            var motorRelationSummary = errors.Count > 0
                ? "Fix anatomy to count motor relations."
                : "Two beams at one node create a motor relation; closed triangles do not twist.";
            return new ConstructionBuildPanelPresentation(
                inputSummary,
                motorRelationSummary,
                $"Not ready: {disabledReason}",
                CanStartTraining: false,
                CanCompleteCreation: false,
                DisabledReason: disabledReason,
                InputCount: _construction.Cores.Count * _coreSensorValueCount,
                OutputCount: 0);
        }

        var motorRelationCount = MotorTopology.BuildNodeConnections(creature)
            .Count(connection => connection.IsMotorized);
        var inputCount = BuildInputCount(creature.Cores.Count, motorRelationCount);
        if (motorRelationCount == 0)
        {
            const string disabledReason = "Add a two-beam node. Closed triangles cannot twist.";
            return new ConstructionBuildPanelPresentation(
                BuildInputSummary(creature.Cores.Count, motorRelationCount),
                "0 motor relations can twist",
                $"Not ready: {disabledReason}",
                CanStartTraining: false,
                CanCompleteCreation: true,
                DisabledReason: disabledReason,
                InputCount: inputCount,
                OutputCount: 0);
        }

        return new ConstructionBuildPanelPresentation(
            BuildInputSummary(creature.Cores.Count, motorRelationCount),
            motorRelationCount == 1 ? "1 motor relation can twist" : $"{motorRelationCount} motor relations can twist",
            $"Ready: {inputCount} inputs -> {motorRelationCount} outputs",
            CanStartTraining: true,
            CanCompleteCreation: true,
            DisabledReason: null,
            InputCount: inputCount,
            OutputCount: motorRelationCount);
    }

    private string BuildCoreToolText()
    {
        var unlockHint = _construction.MaxCores > 1 ? "unlocked" : "50 fitness";
        return $"Core {_construction.Cores.Count}/{_construction.MaxCores} ({unlockHint})";
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

    private static int RecommendedNeurons(ConstructionBuildPanelPresentation buildPanel) =>
        Math.Clamp(
            (int)Math.Ceiling((buildPanel.InputCount + buildPanel.OutputCount) / 2.0),
            BrainShapeDef.MinimumNeuronsPerLayer,
            BrainShapeDef.MaximumNeuronsPerLayer);

    private static string BuildInvalidDraftInputSummary(int coreCount)
    {
        var coreWord = coreCount == 1 ? "core" : "cores";
        return $"{coreCount} {coreWord} placed; fix anatomy to count inputs.";
    }

    private void SubscribeToConstruction()
    {
        if (_isSubscribedToConstruction)
        {
            return;
        }

        _construction.AnatomyChanged += OnConstructionChanged;
        _construction.PropertyChanged += OnConstructionChanged;
        _isSubscribedToConstruction = true;
    }

    private void UnsubscribeFromConstruction()
    {
        if (!_isSubscribedToConstruction)
        {
            return;
        }

        _construction.AnatomyChanged -= OnConstructionChanged;
        _construction.PropertyChanged -= OnConstructionChanged;
        _isSubscribedToConstruction = false;
    }

    private void OnConstructionChanged(object? sender, EventArgs eventArgs)
    {
        _presentationChanged?.Invoke(this, EventArgs.Empty);
    }
}
