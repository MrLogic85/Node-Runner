using Godot;
using NodeRunner.App.Navigation;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Train setup (#194): Train or Simulate, Shadows, Run length and the map, with one Start. The
/// layout is authored in <c>scenes/screens/TrainSetupScreen.tscn</c>; this script binds the
/// presentation, forwards the mode, the sliders and Start, and handles Back.
/// </summary>
public partial class TrainSetupScreen : Control
{
    // The mode switch's segments, in the scene's order.
    private const int _trainSegment = 0;
    private const int _simulateSegment = 1;

    private TrainSetupPresentationViewModel? _presentation;

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void StartRequestedEventHandler();

    public void Setup(TrainSetupPresentationViewModel presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (_presentation is not null)
        {
            _presentation.Changed -= OnPresentationChanged;
        }

        _presentation = presentation;
        _presentation.Changed += OnPresentationChanged;
        if (IsNodeReady())
        {
            Apply();
        }
    }

    public override void _Ready()
    {
        UiLayout.ApplyScreen(this);
        var toolbar = GetNode<UiToolbar>("%Toolbar");
        toolbar.BackPressed += RequestBack;
        var back = new UiBackHandler { CanTakeBack = () => !toolbar.Menu.Visible };
        back.BackRequested += RequestBack;
        AddChild(back, @internal: InternalMode.Front);
        GetNode<UiButton>("%Start").Activated += () => EmitSignal(SignalName.StartRequested);
        GetNode<UiSegmentedSwitch>("%ModeSwitch").SelectionChanged += index => _presentation?.SetMode(ModeAt(index));
        GetNode<UiSlider>("%Shadows").ThumbChanged += (_, position) => _presentation?.SetShadows(position);
        GetNode<UiSlider>("%RunLength").ThumbChanged += (_, position) => _presentation?.SetRunLength(position);
        Apply();
    }

    public override void _ExitTree()
    {
        if (_presentation is not null)
        {
            _presentation.Changed -= OnPresentationChanged;
        }
    }

    private void RequestBack() => EmitSignal(SignalName.BackRequested);

    private static TrainingRunMode ModeAt(int segment) =>
        segment == _simulateSegment ? TrainingRunMode.Simulate : TrainingRunMode.Train;

    private void OnPresentationChanged(object? sender, EventArgs eventArgs)
    {
        if (IsNodeReady())
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (_presentation is null)
        {
            return;
        }

        GetNode<UiLabel>("%Title").ShowText(_presentation.Title);
        GetNode<UiLabel>("%Subtitle").ShowText(_presentation.Subtitle);
        GetNode<UiLabel>("%MapName").ShowText(_presentation.MapName);
        GetNode<Control>("%NoPoweredParts").Visible = !_presentation.HasPoweredParts;
        var mode = GetNode<UiSegmentedSwitch>("%ModeSwitch");
        mode.Visible = _presentation.CanSimulate;
        mode.SelectedIndex = _presentation.Mode == TrainingRunMode.Simulate ? _simulateSegment : _trainSegment;
        GetNode<UiLabel>("%ModeNote").ShowText(_presentation.ModeNote);
        Bind(GetNode<UiSlider>("%Shadows"), _presentation.Shadows, _presentation.ShadowsEnds);
        BindNote(_presentation.ShadowsNote);
        Bind(GetNode<UiSlider>("%RunLength"), _presentation.RunLength, _presentation.RunLengthEnds);
    }

    // The row stays in every state, empty in Simulate, so Run length below it does not move (#318).
    private void BindNote(SettingNote? note)
    {
        var text = GetNode<UiLabel>("%ShadowsNoteText");
        GetNode<Control>("%ShadowsNoteIcon").Visible = note?.IsWarning == true;
        text.TextColor = note?.IsWarning == true ? UiTokens.Color.Halo : UiTokens.Color.Muted;
        text.TextSource = note is null ? () => string.Empty : UiTextTranslation.Source(note.Text);
    }

    private static void Bind(UiSlider slider, SettingSlider value, IReadOnlyList<UiText> ends)
    {
        slider.LabelSource = UiTextTranslation.Source(value.Label);
        slider.ReadoutSource = UiTextTranslation.Source(value.Readout);
        slider.Step = value.Step;
        // No position: an empty track with no thumb or fill.
        slider.Value = value.Position is { } position ? UiSliderValue.Thumb(position) : UiSliderValue.Progress(0);
        slider.Disabled = value.Disabled;
        // The ends never change, so they are set once rather than rebuilt on every drag.
        slider.StepLabelSources ??= [.. ends.Select(end => UiTextTranslation.Source(end))];
    }
}
