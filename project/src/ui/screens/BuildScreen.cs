using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Build screen, unlocked state (<c>reference design/components/Build</c>). The layout is authored
/// in <c>scenes/screens/BuildScreen.tscn</c>; this script binds the construction presentation and
/// forwards intents to its host, which owns saves and navigation.
/// </summary>
public partial class BuildScreen : Control
{
    private ConstructionViewModel? _construction;
    private ConstructionPresentationViewModel? _presentation;
    private bool _subscribedToPresentation;

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void StartTrainingRequestedEventHandler();

    [Signal]
    public delegate void CreationNameChangedEventHandler(string name);

    [Signal]
    public delegate void ResetTrainingRequestedEventHandler();

    [Signal]
    public delegate void DeleteCreationRequestedEventHandler();

    [Signal]
    public delegate void ClearSelectionRequestedEventHandler();

    [Signal]
    public delegate void DeleteSelectionRequestedEventHandler();

    [Signal]
    public delegate void StatsRequestedEventHandler();

    [Signal]
    public delegate void BrainRequestedEventHandler();

    [Signal]
    public delegate void ToolRequestedEventHandler(ConstructionTool tool);

    [Signal]
    public delegate void BrainShapeChangedEventHandler(int hiddenLayers, int neuronsPerLayer);

    /// <summary>False while the overflow menu is open; it takes Android Back itself.</summary>
    public bool CanTakeBack => !Toolbar.Menu.Visible;

    private UiToolbar Toolbar => GetNode<UiToolbar>("%Toolbar");

    private BrainSetupSheet BrainSetup => GetNode<BrainSetupSheet>("%BrainSetupSheet");

    /// <summary>Binds the creation being built: the canvas edits it and the panels show it.</summary>
    public void Setup(ConstructionViewModel construction)
    {
        ArgumentNullException.ThrowIfNull(construction);
        UnsubscribeFromPresentation();
        _construction = construction;
        _presentation = new ConstructionPresentationViewModel(construction);
        if (IsInsideTree())
        {
            SubscribeToPresentation();
        }

        if (IsNodeReady())
        {
            BindViewModels();
            Apply();
        }
    }

    /// <summary>Closes an open sheet, as Android Back does first. False when none was open.</summary>
    public bool CloseOverlay()
    {
        if (!BrainSetup.IsOpen)
        {
            return false;
        }

        BrainSetup.Close();
        return true;
    }

    public override void _EnterTree()
    {
        SubscribeToPresentation();
    }

    public override void _Ready()
    {
        UiLayout.ApplyScreen(this);
        var toolbar = Toolbar;
        toolbar.BackPressed += () => EmitSignal(SignalName.BackRequested);
        var name = GetNode<UiTextField>("%CreationName");
        name.ValidateValue = static value => !string.IsNullOrWhiteSpace(value);
        name.EditingFinished += OnNameEdited;
        GetNode<UiButton>("%StartTraining").Activated += () => EmitSignal(SignalName.StartTrainingRequested);
        BindMenuItem(toolbar, GetNode<UiMenuActionItem>("%MenuBrainSetup"), BrainSetup.Open);
        BindMenuItem(toolbar, GetNode<UiMenuActionItem>("%MenuResetTraining"), () => EmitSignal(SignalName.ResetTrainingRequested));
        BindMenuItem(toolbar, GetNode<UiMenuActionItem>("%MenuDeleteCreation"), () => EmitSignal(SignalName.DeleteCreationRequested));
        BindTool(GetNode<UiButton>("%MoveTool"), ConstructionTool.Place);
        BindTool(GetNode<UiButton>("%BeamTool"), ConstructionTool.Beam);
        BindTool(GetNode<UiButton>("%SelectTool"), ConstructionTool.Select);
        GetNode<UiPartRow>("%CorePart").PartSelected += () => EmitSignal(SignalName.ToolRequested, (int)ConstructionTool.Core);
        GetNode<UiButton>("%Stats").Activated += () => EmitSignal(SignalName.StatsRequested);
        GetNode<UiButton>("%Brain").Activated += () => EmitSignal(SignalName.BrainRequested);
        GetNode<UiButton>("%PartDelete").Activated += () => EmitSignal(SignalName.DeleteSelectionRequested);
        GetNode<UiButton>("%SelectionDelete").Activated += () => EmitSignal(SignalName.DeleteSelectionRequested);
        GetNode<UiButton>("%PartClose").Activated += () => EmitSignal(SignalName.ClearSelectionRequested);
        GetNode<UiButton>("%SelectionClear").Activated += () => EmitSignal(SignalName.ClearSelectionRequested);
        BrainSetup.BrainShapeChanged += (layers, neurons) => EmitSignal(SignalName.BrainShapeChanged, layers, neurons);
        BindViewModels();
        Apply();
    }

    public override void _ExitTree()
    {
        UnsubscribeFromPresentation();
    }

    private static void BindMenuItem(UiToolbar toolbar, UiMenuActionItem item, Action action) =>
        item.Activated += () =>
        {
            toolbar.CloseMenu();
            action();
        };

    private void BindTool(UiButton button, ConstructionTool tool) =>
        button.Activated += () => EmitSignal(SignalName.ToolRequested, (int)tool);

    private void BindViewModels()
    {
        GetNode<ConstructionCanvas>("%ConstructionCanvas").ViewModel = _construction;
        BrainSetup.Presentation = _presentation;
    }

    private void OnNameEdited(string value)
    {
        var name = value.Trim();
        if (name.Length > 0 && name != _presentation?.CreationName)
        {
            EmitSignal(SignalName.CreationNameChanged, name);
        }

        // Show the name the creation really has: a rename the host could not save leaves it unchanged.
        GetNode<UiTextField>("%CreationName").TextValue = _presentation?.CreationName ?? string.Empty;
    }

    private void OnPresentationChanged(object? sender, EventArgs eventArgs)
    {
        if (IsNodeReady())
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (_presentation is not { } presentation)
        {
            return;
        }

        var locked = presentation.IsLocked;
        var buildPanel = presentation.BuildPanel;
        ApplyToolbar(presentation, buildPanel, locked);
        ApplyTools(presentation);
        GetNode<UiChip>("%PartsLockedChip").Visible = locked;
        ApplySidePanel(presentation, buildPanel, locked);
    }

    private void ApplyToolbar(ConstructionPresentationViewModel presentation, ConstructionBuildPanelPresentation buildPanel, bool locked)
    {
        var name = GetNode<UiTextField>("%CreationName");
        if (name.State != UiTextField.TextInputState.Editing)
        {
            name.TextValue = presentation.CreationName;
        }

        GetNode<UiButton>("%StartTraining").Disabled = !buildPanel.CanStartTraining;
        var brainSetup = GetNode<UiMenuActionItem>("%MenuBrainSetup");
        brainSetup.Disabled = presentation.IsBrainShapeLocked;
        brainSetup.NoteText = presentation.IsBrainShapeLocked ? "Locked once trained" : string.Empty;
        GetNode<UiMenuActionItem>("%MenuResetTraining").Visible = locked;
        GetNode<UiMenuActionItem>("%MenuDeleteCreation").Disabled = !presentation.IsSaved;
        if (presentation.IsBrainShapeLocked && BrainSetup.IsOpen)
        {
            BrainSetup.Close();
        }
    }

    private void ApplyTools(ConstructionPresentationViewModel presentation)
    {
        GetNode<UiButton>("%MoveTool").Selected = presentation.ActiveTool == ConstructionTool.Place;
        var beam = GetNode<UiButton>("%BeamTool");
        beam.Selected = presentation.ActiveTool == ConstructionTool.Beam;
        beam.Disabled = presentation.LockTopologyTools;
        GetNode<UiButton>("%SelectTool").Selected = presentation.ActiveTool == ConstructionTool.Select;
    }

    private void ApplySidePanel(ConstructionPresentationViewModel presentation, ConstructionBuildPanelPresentation buildPanel, bool locked)
    {
        var selected = presentation.SelectedPartCount;
        var tray = GetNode<Control>("%PartsTray");
        var savedPanel = GetNode<Control>("%SavedCreation");
        var partSettings = GetNode<Control>("%PartSettings");
        var selection = GetNode<Control>("%Selection");
        tray.Visible = selected == 0 && !locked;
        savedPanel.Visible = selected == 0 && locked;
        partSettings.Visible = selected == 1;
        selection.Visible = selected > 1;
        GetNode<Control>("%Readiness").Visible = selected == 0;
        GetNode<UiSidePanel>("%SidePanel").Title = selected switch
        {
            0 when locked => "Training",
            0 => "Parts",
            1 => presentation.SinglePartTitle,
            _ => presentation.MultiSelectionTitle,
        };

        if (tray.Visible)
        {
            ApplyTray(presentation);
        }

        if (savedPanel.Visible)
        {
            GetNode<UiLabel>("%SavedTitle").Text = presentation.TrainingSummaryTitle;
            GetNode<UiLabel>("%SavedBest").Text = $"Best distance {presentation.BestDistanceText}";
            GetNode<UiLabel>("%SavedBody").Text = presentation.TrainingSummaryBody;
        }

        if (partSettings.Visible)
        {
            ApplyPartSettings(presentation, locked);
        }

        if (selection.Visible)
        {
            GetNode<UiLabel>("%SelectionCounts").Text = presentation.MultiSelectionCounts;
            GetNode<UiLabel>("%SelectionBody").Text = locked
                ? presentation.MultiSelectionBody
                : "Drag any selected part to move them together, or delete the selection.";
            GetNode<UiButton>("%SelectionDelete").Visible = !locked;
        }

        ApplyReadiness(buildPanel);
    }

    private void ApplyTray(ConstructionPresentationViewModel presentation)
    {
        var coresLeft = Math.Max(0, presentation.MaxCores - presentation.CoreCount);
        var core = GetNode<UiPartRow>("%CorePart");
        core.ValueText = $"{coresLeft} left";
        core.State = presentation.ActiveTool == ConstructionTool.Core
            ? UiPartRow.PartRowState.Selected
            : coresLeft == 0
                ? UiPartRow.PartRowState.NoneLeft
                : UiPartRow.PartRowState.Rest;
        GetNode<UiLabel>("%ToolHint").Text = ConstructionPresentationViewModel.ToolHint(presentation.ActiveTool);
    }

    private void ApplyPartSettings(ConstructionPresentationViewModel presentation, bool locked)
    {
        GetNode<UiLabel>("%PartPrimaryLabel").Text = presentation.SinglePartPrimaryLabel;
        GetNode<UiLabel>("%PartPrimaryValue").Text = presentation.SinglePartPrimaryValue;
        GetNode<UiLabel>("%PartConnectionsLabel").Text = presentation.SinglePartConnectionsLabel;
        GetNode<UiLabel>("%PartConnectionsValue").Text = presentation.SinglePartConnectionsValue;
        GetNode<UiLabel>("%PartFacts").Text = presentation.SinglePartFacts;
        GetNode<UiLabel>("%PartBody").Text = presentation.SinglePartBody;
        GetNode<UiButton>("%PartDelete").Visible = !locked;
    }

    private void ApplyReadiness(ConstructionBuildPanelPresentation buildPanel)
    {
        var ready = buildPanel.CanStartTraining;
        var color = ready ? UiTokens.Color.Accent : UiTokens.Color.Danger;
        var icon = GetNode<UiIcon>("%ReadinessIcon");
        icon.IconId = ready ? UiIconId.Check : UiIconId.Warn;
        icon.Color = color;
        var text = GetNode<UiLabel>("%ReadinessText");
        text.Text = buildPanel.ReadinessText;
        text.TextColor = color;
    }

    private void SubscribeToPresentation()
    {
        if (_presentation is null || _subscribedToPresentation)
        {
            return;
        }

        _presentation.PresentationChanged += OnPresentationChanged;
        _subscribedToPresentation = true;
    }

    private void UnsubscribeFromPresentation()
    {
        if (_presentation is null || !_subscribedToPresentation)
        {
            return;
        }

        _presentation.PresentationChanged -= OnPresentationChanged;
        _subscribedToPresentation = false;
    }
}
