using System.ComponentModel;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The Training screen's network sheet: the live direct network, its summary and what the selected
/// neuron drives or is driven by.
/// The scene owns the layout; a tap outside the sheet closes it. The Network view (#393) replaces it.
/// </summary>
public partial class NetworkFocusSheet : Control
{
    private NetworkFocusPresentationViewModel? _presentation;

    private Control Sheet => GetNode<Control>("%Sheet");

    private UiLabel Summary => GetNode<UiLabel>("%Summary");

    private UiLabel Selected => GetNode<UiLabel>("%Selected");

    private NetworkFocusView Network => GetNode<NetworkFocusView>("%Network");

    public bool IsOpen => Visible;

    public NetworkFocusPresentationViewModel? Presentation
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

        Summary.ShowText(_presentation.Summary);
        Selected.ShowText(_presentation.SelectionText);
    }
}
