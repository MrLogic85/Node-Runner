using System.ComponentModel;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// The Training screen (<c>reference design/components/Training</c>), also used to simulate a saved
/// brain. The layout is authored in <c>scenes/screens/TrainingScreen.tscn</c>; its host authors the
/// world inside <c>%ArenaView</c>, feeds the view-models and owns the run.
/// </summary>
public partial class TrainingScreen : Control
{
    private TrainingHeaderPresentation? _header;
    private TrainingPresentationViewModel? _training;
    private SignalFlowPresentationViewModel? _signalFlow;

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void PauseRequestedEventHandler();

    [Signal]
    public delegate void StatsRequestedEventHandler();

    [Signal]
    public delegate void ArenaPressedEventHandler(Vector2 worldPosition);

    private BrainFocusSheet BrainFocus => GetNode<BrainFocusSheet>("%BrainFocusSheet");

    /// <summary>Binds the run: its header, its progress, the signal flow and the brain it shows.</summary>
    public void Setup(
        TrainingHeaderPresentation header,
        TrainingPresentationViewModel training,
        SignalFlowPresentationViewModel signalFlow,
        BrainFocusPresentationViewModel brainFocus)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(signalFlow);
        ArgumentNullException.ThrowIfNull(brainFocus);
        Unsubscribe();
        _header = header;
        _training = training;
        _signalFlow = signalFlow;
        BrainFocus.Presentation = brainFocus;
        if (IsInsideTree())
        {
            Subscribe();
        }

        if (IsNodeReady())
        {
            Apply();
        }
    }

    /// <summary>Shows whether the run is paused: Pause becomes Run.</summary>
    public void ShowPaused(bool paused)
    {
        var pause = GetNode<UiButton>("%Pause");
        pause.IconId = paused ? UiIconId.Play : UiIconId.Pause;
        pause.TooltipText = paused ? "Run" : "Pause";
    }

    /// <summary>Shows or hides the "Too many shadows!" chip in the arena's top-left corner (#318).</summary>
    public void ShowSlowMotion(bool shown) => GetNode<Control>("%SlowMotionChip").Visible = shown;

    /// <summary>
    /// Names the selected part (#388) in a callout above the whole creature, its leader down to the
    /// part, or with a null <paramref name="name"/> shows none. World coordinates; call every frame.
    /// </summary>
    public void ShowPartName(string? name, Vector2 worldAnchor, Rect2 worldCreatureBounds)
    {
        var layer = GetNode<UiCalloutLayer>("%PartCallouts");
        if (name is null)
        {
            layer.SetCallouts([]);
            return;
        }

        var arena = GetNode<UiWorldView>("%ArenaView");
        var anchor = arena.FromWorld(worldAnchor);
        var creatureTop = arena.FromWorld(worldCreatureBounds.Position).Y;
        layer.SetCallouts(
        [
            new UiCalloutLayout.Placement(
                anchor,
                Vector2.Up,
                Math.Max(0, anchor.Y - creatureTop) + UiSize.Space.S1,
                UiCallout.CalloutKind.Warning,
                UiIconId.None,
                name),
        ]);
    }

    /// <summary>Closes the brain sheet, as Android Back does first. False when it was closed.</summary>
    public bool CloseOverlay()
    {
        if (!BrainFocus.IsOpen)
        {
            return false;
        }

        BrainFocus.Close();
        return true;
    }

    public override void _EnterTree()
    {
        Subscribe();
    }

    public override void _Ready()
    {
        UiLayout.ApplyScreen(this);
        GetNode<UiToolbar>("%Toolbar").BackPressed += () => EmitSignal(SignalName.BackRequested);
        GetNode<UiButton>("%Brain").Activated += BrainFocus.Open;
        GetNode<UiStageCard>("%BrainStage").StageSelected += BrainFocus.Open;
        GetNode<UiButton>("%Stats").Activated += () => EmitSignal(SignalName.StatsRequested);
        GetNode<UiButton>("%Pause").Activated += () => EmitSignal(SignalName.PauseRequested);
        GetNode<UiWorldView>("%ArenaView").WorldPressed += position => EmitSignal(SignalName.ArenaPressed, position);
        Apply();
    }

    public override void _ExitTree()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_training is not null)
        {
            _training.PropertyChanged += OnTrainingChanged;
        }

        if (_signalFlow is not null)
        {
            _signalFlow.PropertyChanged += OnSignalFlowChanged;
        }
    }

    private void Unsubscribe()
    {
        if (_training is not null)
        {
            _training.PropertyChanged -= OnTrainingChanged;
        }

        if (_signalFlow is not null)
        {
            _signalFlow.PropertyChanged -= OnSignalFlowChanged;
        }
    }

    private void OnTrainingChanged(object? sender, PropertyChangedEventArgs args) => ApplyGeneration();

    private void OnSignalFlowChanged(object? sender, PropertyChangedEventArgs args) =>
        ApplySignalFlow(counts: args.PropertyName is null);

    private void Apply()
    {
        if (_header is { } header)
        {
            GetNode<UiLabel>("%CreationName").ShowText(header.CreationName);
            GetNode<UiLabel>("%StatusText").ShowText(header.StatusText);
        }

        ApplyGeneration();
        ApplySignalFlow();
    }

    // The shadow strip refreshes itself every frame; the caption follows progress changes.
    private void ApplyGeneration()
    {
        var shows = _header?.ShowsGeneration == true && _training is not null;
        var generation = GetNode<UiLabel>("%Generation");
        var strip = GetNode<ShadowStrip>("%ShadowStrip");
        generation.Visible = shows;
        strip.Visible = shows;
        strip.Presentation = _training;
        if (_training is not null)
        {
            generation.ShowText(_training.GenerationText);
        }
    }

    private void ApplySignalFlow(bool counts = true)
    {
        if (_signalFlow is not { } signalFlow)
        {
            return;
        }

        if (counts)
        {
            GetNode<UiStageCard>("%SensesStage").NoteSource = UiTextTranslation.Source(signalFlow.SensesNote);
            GetNode<UiStageCard>("%OutputsStage").NoteSource = UiTextTranslation.Source(signalFlow.OutputsNote);
        }

        GetNode<UiStageCard>("%DistanceStage").NoteSource = UiTextTranslation.Source(signalFlow.DistanceNote);
        var timeLeft = GetNode<UiValueRow>("%TimeLeftRow");
        timeLeft.ValueSource = UiTextTranslation.Source(signalFlow.TimeLeft);
        GetNode<Control>("%TimeLeft").Visible = timeLeft.ValueSource is not null;
    }
}
