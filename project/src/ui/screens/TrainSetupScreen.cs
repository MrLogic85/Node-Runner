using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Train setup (#194): Train or Simulate, Shadows, Run length and the map, with one Start. The
/// layout is authored in <c>scenes/screens/TrainSetupScreen.tscn</c>; this script binds the
/// presentation, forwards the sliders and Start, and handles Back.
/// </summary>
public partial class TrainSetupScreen : Control
{
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

        GetNode<UiLabel>("%Title").Text = _presentation.Title;
        GetNode<UiLabel>("%Subtitle").Text = _presentation.Subtitle;
        Bind(GetNode<UiSlider>("%Shadows"), _presentation.Shadows, _presentation.ShadowsEnds);
        Bind(GetNode<UiSlider>("%RunLength"), _presentation.RunLength, _presentation.RunLengthEnds);
    }

    private static void Bind(UiSlider slider, PartSlider value, string[] ends)
    {
        slider.LabelText = value.Label;
        slider.ReadoutText = value.Readout;
        slider.HighPosition = value.Position;
        if (!slider.StepLabels.SequenceEqual(ends))
        {
            slider.StepLabels = ends;
        }
    }
}
