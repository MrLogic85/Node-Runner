using System.ComponentModel;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The Training screen's brain sheet: the live network, its summary and the selected neuron.
/// The scene owns the layout; a tap outside the sheet closes it. The Brain view (#393) replaces it.
/// </summary>
public partial class BrainFocusSheet : Control
{
    private BrainFocusPresentationViewModel? _presentation;

    private Control Sheet => GetNode<Control>("%Sheet");

    private Label Summary => GetNode<Label>("%Summary");

    private Label Selected => GetNode<Label>("%Selected");

    private BrainFocusNetworkView Network => GetNode<BrainFocusNetworkView>("%Network");

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
            if (IsNodeReady())
            {
                Network.ViewModel = _presentation;
            }

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
        Network.ViewModel = _presentation;
        GetNode<UiButton>("%Close").Activated += Close;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } press &&
            !Sheet.GetGlobalRect().HasPoint(GetGlobalTransform() * press.Position))
        {
            Close();
            AcceptEvent();
        }
    }

    public void Open()
    {
        UpdateLabels();
        Show();
    }

    public void Close() => Hide();

    private void OnPresentationChanged(object? sender, PropertyChangedEventArgs args) => UpdateLabels();

    private void UpdateLabels()
    {
        if (_presentation is null || !IsNodeReady())
        {
            return;
        }

        Summary.Text = _presentation.HasNetwork
            ? $"{_presentation.Summary}. Circles/solid cyan are positive; diamonds/dashed red are negative; stronger signals draw brighter/thicker."
            : _presentation.Summary;
        Selected.Text = $"{_presentation.SelectedNeuronLabel}: {_presentation.SelectedNeuronSummary}";
    }
}
