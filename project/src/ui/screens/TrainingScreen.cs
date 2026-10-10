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
    private (string Text, UiIconId Icon, IReadOnlyList<UiCalloutLine>? Lines)? _partCallout;

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
    /// What the selected part's callout shows (#388, #1064): its name and, for a part with brain
    /// ports, its glyph and live values; null shows none. Call when the selection or the sampled
    /// values change; <see cref="MovePartCallout"/> places it every frame.
    /// </summary>
    public void ShowPartCallout(PartCalloutPresentation? callout)
    {
        if (callout is null)
        {
            _partCallout = null;
            GetNode<UiCalloutLayer>("%PartCallouts").SetCallouts([]);
            return;
        }

        _partCallout = (
            UiTextTranslation.Now(callout.Name),
            callout.ShowsGlyph ? PartIcons.For(callout.Kind) : UiIconId.None,
            CalloutLines(callout, UiTextTranslation.Source(callout.Note), port => UiTextTranslation.Now(port.Label)));
    }

    /// <summary>
    /// Places the part callout above the whole creature, its leader down to the part. World
    /// coordinates; call every frame.
    /// </summary>
    public void MovePartCallout(Vector2 worldAnchor, Rect2 worldCreatureBounds)
    {
        if (_partCallout is not { } callout)
        {
            return;
        }

        var arena = GetNode<UiWorldView>("%ArenaView");
        var anchor = arena.FromWorld(worldAnchor);
        var creatureTop = arena.FromWorld(worldCreatureBounds.Position).Y;
        GetNode<UiCalloutLayer>("%PartCallouts").SetCallouts(
        [
            new UiCalloutLayout.Placement(
                anchor,
                Vector2.Up,
                Math.Max(0, anchor.Y - creatureTop) + UiSize.Space.S1,
                UiCallout.CalloutKind.Warning,
                callout.Icon,
                callout.Text,
                callout.Lines),
        ]);
    }

    /// <summary>
    /// The lines under a part callout's name (#1064): its senses in <c>accent</c>, then its
    /// outputs in <c>output</c>, each a centred bar for −1…1 or a filling bar for 0…1; or its
    /// <paramref name="note"/>; null for a part that shows only its name. <paramref name="note"/> is
    /// <c>callout.Note</c> and <paramref name="label"/> each port's label, both in the player's
    /// language; they are passed in because only <see cref="UiTextTranslation"/> reads a UiText.
    /// </summary>
    public static IReadOnlyList<UiCalloutLine>? CalloutLines(PartCalloutPresentation callout, Func<string>? note, Func<PartCalloutPort, string> label)
    {
        ArgumentNullException.ThrowIfNull(callout);
        ArgumentNullException.ThrowIfNull(label);

        if (note is not null)
        {
            return [UiCalloutLine.OfNote(note())];
        }

        var lines = new List<UiCalloutLine>();
        if (callout.Senses.Count > 0)
        {
            lines.Add(Line(UiTokens.Color.Accent, callout.Senses, label));
        }

        if (callout.Outputs.Count > 0)
        {
            lines.Add(Line(UiTokens.Color.Output, callout.Outputs, label));
        }

        return lines.Count == 0 ? null : lines;
    }

    private static UiCalloutLine Line(UiTokens.Color color, IReadOnlyList<PartCalloutPort> ports, Func<PartCalloutPort, string> label) =>
        UiCalloutLine.OfMeters(
            color,
            ports.Select(port => new UiCalloutMeter(label(port), port.Value ?? double.NaN, port.Range == PortRange.MinusOneToOne)).ToArray());

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

    // The run bar fills every frame, as the strip's bars do.
    public override void _Process(double delta)
    {
        if (_training is not { } training || !GetNode<Control>("%Caption").Visible)
        {
            return;
        }

        var runTime = GetNode<UiSlider>("%RunTime");
        // Empty between runs rather than hidden, so the tray keeps its height.
        var value = UiSliderValue.Progress(training.RunProgress ?? 0);
        if (runTime.Value != value)
        {
            runTime.Value = value;
        }
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
        ApplySignalFlow(counts: args.PropertyName != nameof(SignalFlowPresentationViewModel.DistanceNote));

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
        GetNode<Control>("%Caption").Visible = shows;
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
    }
}
