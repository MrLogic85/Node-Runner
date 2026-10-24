using System.ComponentModel;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// The Training screen (<c>reference design/components/Training</c>), which also shows a saved
/// network's Simulate run. The layout is authored in <c>scenes/screens/TrainingScreen.tscn</c>; its host authors the
/// world inside <c>%ArenaView</c>, feeds the view-models and owns the run.
/// </summary>
public partial class TrainingScreen : Control
{
    private TrainingHeaderPresentation? _header;
    private TrainingPresentationViewModel? _training;
    private SignalFlowPresentationViewModel? _signalFlow;
    private IReadOnlyList<UiCalloutSection> _partCallout = [];

    // The part callout's sections after its first, kept so every frame passes the same list and
    // the callout refreshes only when a new sample arrives.
    private UiCalloutSection[]? _partCalloutSections;

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void PauseRequestedEventHandler();

    [Signal]
    public delegate void StatsRequestedEventHandler();

    [Signal]
    public delegate void ArenaPressedEventHandler(Vector2 worldPosition);

    private NetworkFocusSheet NetworkFocus => GetNode<NetworkFocusSheet>("%NetworkFocusSheet");

    /// <summary>
    /// The share of the arena's height the part callout takes from the top, with its leader and
    /// margins: the shelf the camera keeps clear for it. 0 while no callout shows.
    /// </summary>
    public double PartCalloutShelf { get; private set; }

    /// <summary>Binds the run: its header, its progress, the signal flow and the network it shows.</summary>
    public void Setup(
        TrainingHeaderPresentation header,
        TrainingPresentationViewModel training,
        SignalFlowPresentationViewModel signalFlow,
        NetworkFocusPresentationViewModel networkFocus)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(signalFlow);
        ArgumentNullException.ThrowIfNull(networkFocus);
        Unsubscribe();
        _header = header;
        _training = training;
        _signalFlow = signalFlow;
        NetworkFocus.Presentation = networkFocus;
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

    /// <summary>Shows or hides the "Too many shadows!" chip in the arena's top-left corner.</summary>
    public void ShowSlowMotion(bool shown) => GetNode<Control>("%SlowMotionChip").Visible = shown;

    /// <summary>
    /// What the selected parts' callout shows: for each part, top → down, its name
    /// and, for a part with network ports, its glyph and live values; a stacked joint's parts each
    /// get a section of one callout. An empty list shows none. Call when the selection or
    /// the sampled values change; <see cref="MovePartCallout"/> places it every frame.
    /// </summary>
    public void ShowPartCallout(IReadOnlyList<PartCalloutPresentation> callouts)
    {
        ArgumentNullException.ThrowIfNull(callouts);
        _partCallout =
        [
            .. callouts.Select(callout => CalloutSection(
                callout,
                UiTextTranslation.Now(callout.Name),
                UiTextTranslation.Source(callout.Note),
                port => UiTextTranslation.Now(port.Label),
                (UiTextTranslation.Now(PartCalloutPresentation.InputsHeading), UiTextTranslation.Now(PartCalloutPresentation.OutputsHeading)))),
        ];
        _partCalloutSections = _partCallout.Count > 1 ? [.. _partCallout.Skip(1)] : null;
        if (_partCallout.Count == 0)
        {
            GetNode<UiCalloutLayer>("%PartCallouts").SetCallouts([]);
            PartCalloutShelf = 0;
        }
    }

    /// <summary>
    /// Places the part callout: docked at the arena's top over its centre, where the camera
    /// keeps a creature at rest, clear of the slow-motion
    /// chip, so the box holds still and only its leader follows the part at
    /// <paramref name="worldAnchor"/>, in world coordinates. Call every frame; it updates
    /// <see cref="PartCalloutShelf"/>.
    /// </summary>
    public void MovePartCallout(Vector2 worldAnchor)
    {
        if (_partCallout.Count == 0)
        {
            return;
        }

        var layer = GetNode<UiCalloutLayer>("%PartCallouts");
        var bounds = new Rect2(Vector2.Zero, layer.Size);
        var chip = GetNode<Control>("%SlowMotionChip");
        if (chip.Visible)
        {
            bounds = UiCalloutLayout.RightOf(bounds, chip.Position.X + chip.Size.X);
        }

        var first = _partCallout[0];
        var placement = new UiCalloutLayout.Placement(
            GetNode<UiWorldView>("%ArenaView").FromWorld(worldAnchor),
            Vector2.Zero,
            0,
            UiCallout.CalloutKind.Warning,
            first.IconId,
            first.Text,
            first.Lines,
            _partCalloutSections);
        var docked = layer.SetDockedCallout(placement, layer.Size.X / 2, bounds);
        PartCalloutShelf = UiCalloutLayout.DockedShare(docked, layer.Size.Y);
    }

    /// <summary>
    /// One part's section of the part callout: its glyph if it shows one, its name,
    /// then its inputs in <c>accent</c> and its outputs in <c>output</c>, each line headed by its
    /// group's word and each port a centred bar for −1…1 or a filling bar for 0…1; or its
    /// <paramref name="note"/>; or no lines for a part that shows only its name.
    /// <paramref name="name"/> is <c>callout.Name</c>, <paramref name="note"/> <c>callout.Note</c>,
    /// <paramref name="label"/> each port's label and <paramref name="headings"/>
    /// <see cref="PartCalloutPresentation.InputsHeading"/> and
    /// <see cref="PartCalloutPresentation.OutputsHeading"/>, all in the player's language; they
    /// are passed in because only <see cref="UiTextTranslation"/> reads a UiText.
    /// </summary>
    public static UiCalloutSection CalloutSection(
        PartCalloutPresentation callout,
        string name,
        Func<string>? note,
        Func<PartCalloutPort, string> label,
        (string Inputs, string Outputs) headings)
    {
        ArgumentNullException.ThrowIfNull(callout);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(label);
        return new UiCalloutSection(
            callout.ShowsGlyph ? PartIcons.For(callout.Kind) : UiIconId.None,
            name,
            CalloutLines(callout, note, label, headings));
    }

    private static IReadOnlyList<UiCalloutLine>? CalloutLines(
        PartCalloutPresentation callout,
        Func<string>? note,
        Func<PartCalloutPort, string> label,
        (string Inputs, string Outputs) headings)
    {
        if (note is not null)
        {
            return [UiCalloutLine.OfNote(note())];
        }

        var lines = new List<UiCalloutLine>();
        if (callout.Inputs.Count > 0)
        {
            lines.Add(Line(UiTokens.Color.Accent, callout.Inputs, label, headings.Inputs));
        }

        if (callout.Outputs.Count > 0)
        {
            lines.Add(Line(UiTokens.Color.Output, callout.Outputs, label, headings.Outputs));
        }

        return lines.Count == 0 ? null : lines;
    }

    private static UiCalloutLine Line(UiTokens.Color color, IReadOnlyList<PartCalloutPort> ports, Func<PartCalloutPort, string> label, string heading) =>
        UiCalloutLine.OfMeters(
            color,
            ports.Select(port => new UiCalloutMeter(label(port), port.Value ?? double.NaN, port.Range == PortRange.MinusOneToOne)).ToArray(),
            heading);

    /// <summary>Closes the network sheet, as Android Back does first. False when it was closed.</summary>
    public bool CloseOverlay()
    {
        if (!NetworkFocus.IsOpen)
        {
            return false;
        }

        NetworkFocus.Close();
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
        GetNode<UiButton>("%Network").Activated += NetworkFocus.Open;
        GetNode<UiStageCard>("%NetworkStage").StageSelected += NetworkFocus.Open;
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

    private void OnSignalFlowChanged(object? sender, PropertyChangedEventArgs args) => ApplySignalFlow(args.PropertyName);

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

    // Applies only the property that changed; null, for a new view-model, applies all.
    private void ApplySignalFlow(string? changed = null)
    {
        if (_signalFlow is not { } signalFlow)
        {
            return;
        }

        if (changed is null or nameof(SignalFlowPresentationViewModel.InputBars))
        {
            ApplyMeters(
                GetNode<UiStageCard>("%InputsStage"),
                GetNode<HBoxContainer>("%InputBars"),
                GetNode<UiChip>("%MoreInputs"),
                GetNode<Control>("%NoInputs"),
                signalFlow.HasNoInputs,
                signalFlow.InputBars,
                UiTextTranslation.Source(signalFlow.InputsNote),
                UiTextTranslation.Source(signalFlow.MoreInputs),
                static centred => new UiMeterBar { Vertical = true, Centred = centred });
        }

        if (changed is null or nameof(SignalFlowPresentationViewModel.InputBars) or nameof(SignalFlowPresentationViewModel.InputValues))
        {
            ApplyValues(GetNode<HBoxContainer>("%InputBars"), signalFlow.InputValues, static (UiMeterBar bar, double value) => bar.Value = value);
        }

        if (changed is null or nameof(SignalFlowPresentationViewModel.OutputDials))
        {
            ApplyMeters(
                GetNode<UiStageCard>("%OutputsStage"),
                GetNode<HBoxContainer>("%OutputDials"),
                GetNode<UiChip>("%MoreOutputs"),
                GetNode<Control>("%NoOutputs"),
                signalFlow.HasNoOutputs,
                signalFlow.OutputDials,
                UiTextTranslation.Source(signalFlow.OutputsNote),
                UiTextTranslation.Source(signalFlow.MoreOutputs),
                static centred => new UiDial { Centred = centred });
        }

        if (changed is null or nameof(SignalFlowPresentationViewModel.OutputDials) or nameof(SignalFlowPresentationViewModel.OutputValues))
        {
            ApplyValues(GetNode<HBoxContainer>("%OutputDials"), signalFlow.OutputValues, static (UiDial dial, double value) => dial.Value = value);
        }

        if (changed is null or nameof(SignalFlowPresentationViewModel.DistanceReadout))
        {
            ApplyDistanceReadout(signalFlow);
        }
    }

    // Rebuilds a stage's bars or dials, which happens only when the followed creature's ports
    // change; the chip stays last. A creature with none says so, and with no creature the stage collapses.
    private static void ApplyMeters<T>(UiStageCard stage, HBoxContainer row, UiChip more, Control none, bool hasNone, IReadOnlyList<PortRange> ranges, Func<string>? note, Func<string>? moreText, Func<bool, T> create)
        where T : Control
    {
        foreach (var meter in row.GetChildren().OfType<T>())
        {
            row.RemoveChild(meter);
            meter.QueueFree();
        }

        foreach (var range in ranges)
        {
            row.AddChild(create(range == PortRange.MinusOneToOne));
        }

        row.MoveChild(more, -1);
        more.TextSource = moreText;
        more.Visible = moreText is not null;
        row.Visible = ranges.Count > 0;
        none.Visible = hasNone;
        stage.NoteSource = note;
        stage.Collapsed = ranges.Count == 0 && !hasNone;
    }

    // A bar or dial redraws only when its value changed (UiMeterBar.Value, UiDial.Value).
    private static void ApplyValues<T>(HBoxContainer row, IReadOnlyList<double> values, Action<T, double> show)
        where T : Control
    {
        for (var i = 0; i < values.Count; i++)
        {
            show(row.GetChild<T>(i), values[i]);
        }
    }

    // A tear shows smaller and in danger: "Crash 3.4 m" does not fit the card at the live size.
    private void ApplyDistanceReadout(SignalFlowPresentationViewModel signalFlow)
    {
        var readout = GetNode<UiLabel>("%DistanceReadout");
        var source = UiTextTranslation.Source(signalFlow.DistanceReadout);
        GetNode<UiStageCard>("%DistanceStage").Collapsed = source is null;
        readout.TextStyle = signalFlow.DistanceTorn ? UiTokens.Typography.ReadoutMedium : UiTokens.Typography.ReadoutLarge;
        readout.TextColor = signalFlow.DistanceTorn ? UiTokens.Color.Danger : UiTokens.Color.Ink;
        readout.TextSource = source;
    }
}
