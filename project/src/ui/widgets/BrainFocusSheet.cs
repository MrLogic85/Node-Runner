using System.ComponentModel;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The Training screen's brain sheet: the live network, its summary and the selected neuron.
/// Still built in code with its own sizes, outside the rewritten-scene checks; the Brain view
/// (#393) replaces it.
/// </summary>
public partial class BrainFocusSheet : Control
{
    private BrainFocusPresentationViewModel? _presentation;
    private UiSheet? _sheet;
    private Label? _summaryLabel;
    private Label? _selectedLabel;

    public bool IsOpen => Visible;

    public BrainFocusPresentationViewModel? Presentation
    {
        get => _presentation;
        set
        {
            if (_presentation is not null)
            {
                _presentation.PropertyChanged -= OnPresentationChanged;
            }

            _presentation = value;
            if (_presentation is not null && IsInsideTree())
            {
                _presentation.PropertyChanged += OnPresentationChanged;
            }
        }
    }

    public override void _EnterTree()
    {
        if (_presentation is not null)
        {
            _presentation.PropertyChanged += OnPresentationChanged;
        }
    }

    public override void _ExitTree()
    {
        if (_presentation is not null)
        {
            _presentation.PropertyChanged -= OnPresentationChanged;
        }
    }

    public override void _Ready()
    {
        var dismiss = new Button
        {
            Name = "BrainFocusDismiss",
            Flat = true,
            Text = string.Empty,
            MouseFilter = MouseFilterEnum.Stop,
        };
        dismiss.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dismiss.Pressed += Close;
        AddChild(dismiss);

        _sheet = new UiSheet
        {
            Name = "BrainFocusSheet",
            Title = "BrainFocus · Decides",
            CustomMinimumSize = new Vector2(540, 0),
        };
        AddChild(_sheet);
    }

    public void Open()
    {
        if (_sheet is null)
        {
            return;
        }

        _sheet.Position = new Vector2(Mathf.Max(24, (Size.X - _sheet.CustomMinimumSize.X) / 2), 72);
        _sheet.SetBody(CreateBody());
        UpdateLabels();
        Show();
    }

    public void Close() => Hide();

    private Control CreateBody()
    {
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);

        _summaryLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        stack.AddChild(_summaryLabel);

        stack.AddChild(new BrainFocusNetworkView
        {
            ViewModel = _presentation,
            CustomMinimumSize = new Vector2(500, 220),
            MouseFilter = MouseFilterEnum.Stop,
        });

        _selectedLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        stack.AddChild(_selectedLabel);

        var close = new UiButton
        {
            Kind = UiButtonKind.Primary,
            Text = "Back to SignalFlow",
        };
        close.Pressed += Close;
        stack.AddChild(close);

        return stack;
    }

    private void OnPresentationChanged(object? sender, PropertyChangedEventArgs args) => UpdateLabels();

    private void UpdateLabels()
    {
        if (_presentation is null)
        {
            return;
        }

        if (_summaryLabel is not null)
        {
            _summaryLabel.Text = _presentation.HasNetwork
                ? $"{_presentation.Summary}. Circles/solid cyan are positive; diamonds/dashed red are negative; stronger signals draw brighter/thicker."
                : _presentation.Summary;
        }

        if (_selectedLabel is not null)
        {
            _selectedLabel.Text = $"{_presentation.SelectedNeuronLabel}: {_presentation.SelectedNeuronSummary}";
        }
    }
}
