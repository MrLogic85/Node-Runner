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

    public string TrainingSummaryBody => _build.TrainingGeneration is not null
        ? "This can drop after a noisy generation; Training's Best never does. Tap the padlock to change the body. Training is kept."
        : "Start training when you are ready.";

    /// <summary>The Reset training dialog body (#687). Copy sits beside Reset in the overflow, so it is offered.</summary>
    public UiText ResetTrainingWarning =>
        UiText.Counted(
            "{1} forgets its {0} generation of training and keeps its body. Copy it first to keep the trained one.",
            "{1} forgets its {0} generations of training and keeps its body. Copy it first to keep the trained one.",
            _build.TrainingGeneration ?? 0,
            _build.CreationName);

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
                UiText.Counted("{0} selected", "{0} selected", count),
                settings,
                settings.Count > 0 ? "A slider sets one value for all of them." : string.Empty,
                settings.Count == 0 && !showFrameRows ? "These parts share no settings." : string.Empty,
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
            BuildTool.Parts => new ToolPanelPresentation(ToolPanelMode.PartsTray, "Parts"),
            BuildTool.Beam => new ToolPanelPresentation(ToolPanelMode.LinkList, "Beams"),
            BuildTool.Joint => new ToolPanelPresentation(ToolPanelMode.JointHelp, "Joint"),
            BuildTool.Select => new ToolPanelPresentation(ToolPanelMode.SelectHelp, "Select"),
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
    private UiText ShortReadiness(IReadOnlyList<string> errors)
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
                    && !_build.Pistons.Any(piston => piston.NodeA == nodeId || piston.NodeB == nodeId);
            });
        if (unconnected > 0)
        {
            return UiText.Counted("{0} node not connected", "{0} nodes not connected", unconnected);
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
            // The builder's reasons are still finished English until #752 makes them UiText.
            (0, 0) => UiText.Plain(errors[0]),
            (0, _) => UiText.Counted("{0} piston too short", "{0} pistons too short", tooShortPistons),
            _ => UiText.Counted("{0} beam too short", "{0} beams too short", tooShort),
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
