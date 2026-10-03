using Godot;
using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Build screen, unlocked state (<c>reference design/components/Build</c>). The layout is authored
/// in <c>scenes/screens/BuildScreen.tscn</c>; this script binds the Build presentation and
/// forwards intents to its host, which owns saves and navigation.
/// </summary>
public partial class BuildScreen : Control
{
    private BuildViewModel? _build;
    private BuildPresentationViewModel? _presentation;
    private int? _renamingPartId;
    private bool _subscribedToPresentation;
    private string? _shownPartGroup;

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void StartTrainingRequestedEventHandler();

    [Signal]
    public delegate void CreationNameChangedEventHandler(string name);

    [Signal]
    public delegate void PartNameChangedEventHandler(int partId, string name);

    [Signal]
    public delegate void UnlockRequestedEventHandler();

    [Signal]
    public delegate void ResetTrainingRequestedEventHandler();

    [Signal]
    public delegate void CopyCreationRequestedEventHandler();

    [Signal]
    public delegate void DeleteCreationRequestedEventHandler();

    [Signal]
    public delegate void DeleteSelectionRequestedEventHandler();

    [Signal]
    public delegate void ToolRequestedEventHandler(BuildTool tool);

    /// <summary>A link row in the Beams list was tapped (#705): the rail tool stays Beams.</summary>
    [Signal]
    public delegate void LinkPickedEventHandler(int link);

    /// <summary>A setting's slider moved (#704): every selected part takes <paramref name="value"/> for <paramref name="parameter"/> (a <see cref="PartParameterId"/>).</summary>
    [Signal]
    public delegate void ParameterChangedEventHandler(int parameter, double value);

    /// <summary>False while the overflow menu is open; it takes Android Back itself.</summary>
    public bool CanTakeBack => !Toolbar.Menu.Visible;

    private UiToolbar Toolbar => GetNode<UiToolbar>("%Toolbar");

    /// <summary>Binds the creation being built: the canvas edits it and the panels show it.</summary>
    public void Setup(BuildViewModel build)
    {
        ArgumentNullException.ThrowIfNull(build);
        UnsubscribeFromPresentation();
        _build = build;
        _presentation = new BuildPresentationViewModel(build);
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
        GetNode<UiButton>("%Unlock").Activated += () => EmitSignal(SignalName.UnlockRequested);
        BindMenuItem(toolbar, GetNode<UiMenuActionItem>("%MenuResetTraining"), () => EmitSignal(SignalName.ResetTrainingRequested));
        BindMenuItem(toolbar, GetNode<UiMenuActionItem>("%MenuCopyCreation"), () => EmitSignal(SignalName.CopyCreationRequested));
        BindMenuItem(toolbar, GetNode<UiMenuActionItem>("%MenuDeleteCreation"), () => EmitSignal(SignalName.DeleteCreationRequested));
        BindTool(GetNode<UiButton>("%MoveTool"), BuildTool.Move);
        BindTool(GetNode<UiButton>("%BeamTool"), BuildTool.Beam);
        BindTool(GetNode<UiButton>("%JointTool"), BuildTool.Joint);
        BindTool(GetNode<UiButton>("%SelectTool"), BuildTool.Select);
        GetNode<UiIconTabs>("%PartTabs").TabSelected += OnPartTabSelected;
        GetNode<UiButton>("%PartDelete").Activated += () => EmitSignal(SignalName.DeleteSelectionRequested);
        GetNode<UiButton>("%SelectionDelete").Activated += () => EmitSignal(SignalName.DeleteSelectionRequested);
        var partName = GetNode<UiTextField>("%PartName");
        partName.EditingStarted += () => _renamingPartId = _presentation?.SinglePart?.Id;
        partName.EditingFinished += OnPartNameEdited;
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

    private void BindTool(UiButton button, BuildTool tool) =>
        button.Activated += () => EmitSignal(SignalName.ToolRequested, (int)tool);

    private void BindViewModels()
    {
        GetNode<BuildCanvas>("%BuildCanvas").ViewModel = _build;
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

    private void OnPartNameEdited(string value)
    {
        if (_renamingPartId is { } partId)
        {
            _renamingPartId = null;
            EmitSignal(SignalName.PartNameChanged, partId, value);
        }
    }

    /// <summary>
    /// One slider per setting in <paramref name="settings"/>, in order (#704). A setting's slider is
    /// made the first time it shows; differing values have Marker ends and no thumb.
    /// </summary>
    private void ApplyParameterSliders(Container container, IReadOnlyList<ParameterSlider> settings)
    {
        container.Visible = settings.Count > 0;
        foreach (var slider in container.GetChildren().OfType<UiSlider>())
        {
            slider.Visible = settings.Any(setting => setting.Id.ToString() == slider.Name);
        }

        for (var index = 0; index < settings.Count; index++)
        {
            var setting = settings[index];
            var slider = container.GetNodeOrNull<UiSlider>(setting.Id.ToString()) ?? AddParameterSlider(container, setting.Id);
            container.MoveChild(slider, index);
            slider.LabelText = setting.Label;
            slider.ReadoutText = setting.Readout;
            slider.Value = setting.ValuesDiffer
                ? new UiSliderValue(UiSliderEnd.Marker(setting.Low), UiSliderEnd.Marker(setting.High))
                : UiSliderValue.Thumb(setting.High);
        }
    }

    private UiSlider AddParameterSlider(Container container, PartParameterId id)
    {
        var slider = new UiSlider { Name = id.ToString(), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        slider.ThumbChanged += (_, position) => EmitSignal(SignalName.ParameterChanged, (int)id, PartParameters.ValueAt(id, position));

        // Differing values have no thumb: a touch sets one value for all of them.
        slider.TrackPressed += position =>
        {
            slider.Value = UiSliderValue.Thumb(position);
            EmitSignal(SignalName.ParameterChanged, (int)id, PartParameters.ValueAt(id, position));
        };
        container.AddChild(slider);
        return slider;
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

    private void ApplyToolbar(BuildPresentationViewModel presentation, BuildPanelPresentation buildPanel, bool locked)
    {
        var name = GetNode<UiTextField>("%CreationName");
        if (name.State != UiTextField.TextInputState.Editing)
        {
            name.TextValue = presentation.CreationName;
        }

        GetNode<UiButton>("%StartTraining").Disabled = !buildPanel.CanStartTraining;
        GetNode<UiButton>("%Unlock").Visible = locked;
        GetNode<UiMenuActionItem>("%MenuStats").Visible = presentation.IsTrained;
        GetNode<UiMenuActionItem>("%MenuResetTraining").Visible = presentation.IsTrained;
        GetNode<UiMenuActionItem>("%MenuCopyCreation").Visible = presentation.IsTrained;
    }

    private void ApplyTools(BuildPresentationViewModel presentation)
    {
        GetNode<UiButton>("%MoveTool").Selected = presentation.ActiveTool == BuildTool.Move;
        var beam = GetNode<UiButton>("%BeamTool");
        beam.Selected = presentation.ActiveTool == BuildTool.Beam;
        beam.Disabled = presentation.LockTopologyTools;
        var joint = GetNode<UiButton>("%JointTool");
        joint.Selected = presentation.ActiveTool == BuildTool.Joint;
        joint.Disabled = presentation.LockTopologyTools;
        GetNode<UiButton>("%SelectTool").Selected = presentation.ActiveTool == BuildTool.Select;
    }

    private void ApplySidePanel(BuildPresentationViewModel presentation, BuildPanelPresentation buildPanel, bool locked)
    {
        // A name being typed belongs to the part it was started on: finish it before another part shows.
        var partName = GetNode<UiTextField>("%PartName");
        if (partName.State == UiTextField.TextInputState.Editing && presentation.SinglePart?.Id != _renamingPartId)
        {
            partName.FinishEditing();
        }

        var selected = presentation.SelectedPartCount;
        var tray = GetNode<Control>("%PartsTray");
        var savedPanel = GetNode<Control>("%SavedCreation");
        var partSettings = GetNode<Control>("%PartSettings");
        var selection = GetNode<Control>("%Selection");
        var linkList = presentation.LinkList;
        tray.Visible = selected == 0 && !locked;
        GetNode<Control>("%PanelSpacer").Visible = !tray.Visible;
        GetNode<UiLabel>("%ToolHint").Text = presentation.PanelToolHint;
        GetNode<UiIcon>("%ToolLineIcon").IconId = RailIcon(presentation.ActiveTool);
        GetNode<Control>("%ToolLine").Visible = tray.Visible && presentation.PanelToolHint.Length > 0;
        savedPanel.Visible = selected == 0 && locked;
        partSettings.Visible = selected == 1;
        selection.Visible = selected > 1;
        GetNode<Control>("%Readiness").Visible = selected == 0;
        var part = presentation.SinglePart;
        var sidePanel = GetNode<UiSidePanel>("%SidePanel");
        sidePanel.Title = selected switch
        {
            0 when locked => "Training",
            0 when linkList is not null => linkList.Title,
            0 => "Parts",
            1 => part?.Name ?? string.Empty,
            _ => presentation.Selection?.Title ?? string.Empty,
        };
        sidePanel.IconId = selected > 1 ? UiIconId.Select : part is null ? UiIconId.None : PartSettingsIcon(part.Kind);

        if (tray.Visible)
        {
            ApplyPickList(presentation, linkList);
        }

        if (savedPanel.Visible)
        {
            GetNode<UiLabel>("%SavedTitle").Text = presentation.TrainingSummaryTitle;
            GetNode<UiLabel>("%SavedLatest").Text = presentation.LatestDistanceText;
            GetNode<UiLabel>("%SavedBody").Text = presentation.TrainingSummaryBody;
        }

        if (partSettings.Visible && part is not null)
        {
            ApplyPartSettings(part);
        }

        if (selection.Visible && presentation.Selection is { } group)
        {
            ApplySelection(group);
        }

        ApplyReadiness(buildPanel);
    }

    private void ApplySelection(SelectionPanelPresentation group)
    {
        ApplyParameterSliders(GetNode<Container>("%SelectionParameters"), group.Settings);
        GetNode<Control>("%SelectionSettings").Visible = group.Settings.Count > 0;
        GetNode<UiLabel>("%SelectionSettingsNote").Text = group.SettingsNote;
        var emptyNote = GetNode<UiLabel>("%SelectionEmptyNote");
        emptyNote.Text = group.EmptyNote;
        emptyNote.Visible = group.EmptyNote.Length > 0;
        GetNode<Control>("%SelectionRows").Visible = group.ShowFrameRows;
        GetNode<UiButton>("%SelectionDelete").Text = group.DeleteText;
        var deleteNote = GetNode<UiLabel>("%SelectionDeleteNote");
        deleteNote.Text = group.DeleteNote;
        deleteNote.Visible = group.DeleteNote.Length > 0;
        GetNode<Control>("%SelectionActions").Visible = group.CanDelete;
    }

    private void ApplyPickList(BuildPresentationViewModel presentation, LinkListPresentation? linkList)
    {
        GetNode<UiIconTabs>("%PartTabs").Visible = linkList is null;
        if (linkList is not null)
        {
            ApplyLinkList(linkList);
        }
        else
        {
            ApplyTray(presentation);
        }
    }

    private void ApplyLinkList(LinkListPresentation list)
    {
        GetNode<UiLabel>("%PartGroupName").Text = list.Name;
        GetNode<UiLabel>("%PartHelp").Text = list.HelpText;
        var lockedNote = GetNode<UiLabel>("%PartLockedNote");
        lockedNote.Text = list.LockedNote;
        lockedNote.Visible = list.LockedNote.Length > 0;
        GetNode<Control>("%PartLockedIcon").Visible = lockedNote.Visible;
        var rows = GetNode<Container>("%PartRows");
        if (_shownPartGroup != "links")
        {
            _shownPartGroup = "links";
            ClearRows(rows);
            foreach (var link in list.Rows)
            {
                var row = new UiPartRow { IconId = LinkIcon(link.Link), Label = link.Name, Compact = true };
                if (link.IsPickable)
                {
                    row.PartSelected += () => EmitSignal(SignalName.LinkPicked, (int)link.Link);
                }

                rows.AddChild(row);
            }
        }

        for (var index = 0; index < list.Rows.Count; index++)
        {
            rows.GetChild<UiPartRow>(index).State = list.Rows[index].State switch
            {
                LinkListRowState.Selected => UiPartRow.PartRowState.Selected,
                LinkListRowState.Locked => UiPartRow.PartRowState.Locked,
                _ => UiPartRow.PartRowState.Rest,
            };
        }
    }

    private void ApplyTray(BuildPresentationViewModel presentation)
    {
        var group = presentation.PartGroups[GetNode<UiIconTabs>("%PartTabs").SelectedIndex];
        GetNode<UiLabel>("%PartGroupName").Text = group.Name;
        GetNode<UiLabel>("%PartHelp").Text = group.HelpText;
        var lockedNote = GetNode<UiLabel>("%PartLockedNote");
        lockedNote.Text = group.LockedNote;
        lockedNote.Visible = group.LockedNote.Length > 0;
        GetNode<Control>("%PartLockedIcon").Visible = lockedNote.Visible;
        var rows = GetNode<Container>("%PartRows");
        if (_shownPartGroup != $"tray:{group.Name}")
        {
            _shownPartGroup = $"tray:{group.Name}";
            ClearRows(rows);

            foreach (var part in group.Rows)
            {
                var row = new UiPartRow { IconId = PartIcon(part.Part), Label = part.Name, Compact = true };
                if (DraggablePart(part) is { } draggable)
                {
                    row.SetDragForwarding(
                        Callable.From<Vector2, Variant>(_ => StartPartDrag(row, draggable)),
                        new Callable(),
                        new Callable());
                }

                rows.AddChild(row);
            }
        }

        for (var index = 0; index < group.Rows.Count; index++)
        {
            var row = group.Rows[index];
            rows.GetChild<UiPartRow>(index).State = row.State switch
            {
                PartTrayRowState.ComingLater => UiPartRow.PartRowState.Locked,
                _ => UiPartRow.PartRowState.Rest,
            };
        }
    }

    private static void ClearRows(Container rows)
    {
        foreach (var child in rows.GetChildren())
        {
            rows.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void OnPartTabSelected(int index)
    {
        GetNode<ScrollContainer>("%PartScroll").ScrollVertical = 0;
        Apply();
    }

    /// <summary>The part a tray row can be dragged out as (#376): an available sensor, or null.</summary>
    public static BuildPart? DraggablePart(PartTrayRow row) =>
        row.IsAvailable && PartTray.SensorKindOf(row.Part) is not null ? row.Part : null;

    /// <summary>Lifts the part out of its row: the canvas takes the drop, and the row's glyph floats above the finger.</summary>
    private Variant StartPartDrag(UiPartRow row, BuildPart part)
    {
        if (_presentation?.IsLocked != false)
        {
            return default;
        }

        row.SetDragPreview(row.CreateDragPreview());
        return BuildCanvas.PartDragData(part);
    }

    private static UiIconId RailIcon(BuildTool tool) => tool switch
    {
        BuildTool.Beam => UiIconId.Beam,
        BuildTool.Joint => UiIconId.Joint,
        BuildTool.Select => UiIconId.Select,
        _ => UiIconId.Move,
    };

    /// <summary>The Beams list glyph for a link.</summary>
    public static UiIconId LinkIcon(BuildLink link) => link switch
    {
        BuildLink.Beam => UiIconId.PartBeam,
        BuildLink.Piston => UiIconId.PartPiston,
        BuildLink.Spring => UiIconId.PartSpring,
        BuildLink.Wing => UiIconId.PartWing,
        _ => UiIconId.None,
    };

    /// <summary>The tray glyph for a part.</summary>
    public static UiIconId PartIcon(BuildPart part) => part switch
    {
        BuildPart.Brake => UiIconId.PartBrake,
        BuildPart.Servo => UiIconId.PartServo,
        BuildPart.Stepper => UiIconId.PartStepper,
        BuildPart.VelocityMotor => UiIconId.PartVelocity,
        BuildPart.Wheel => UiIconId.PartWheel,
        BuildPart.Accelerometer => UiIconId.PartAccelerometer,
        BuildPart.Camera => UiIconId.PartCamera,
        BuildPart.Battery => UiIconId.PartBattery,
        BuildPart.Generator => UiIconId.PartGenerator,
        BuildPart.FuelTank => UiIconId.PartFuel,
        _ => UiIconId.None,
    };

    private void ApplyPartSettings(PartSettingsPresentation part)
    {
        var name = GetNode<UiTextField>("%PartName");
        name.PlaceholderText = part.DefaultName;
        if (name.State != UiTextField.TextInputState.Editing)
        {
            name.TextValue = part.Name;
        }

        GetNode<UiLabel>("%PartConnectionsLabel").Text = part.ConnectionsLabel;
        GetNode<UiLabel>("%PartConnectionsValue").Text = part.ConnectionsValue;
        GetNode<Control>("%PartConnectionsLabel").GetParent<Control>().Visible = part.ConnectionsLabel.Length > 0;
        ApplyParameterSliders(GetNode<Container>("%PartParameters"), part.Settings);

        GetNode<UiLabel>("%PartNote").Text = part.Note;
        GetNode<UiButton>("%PartDelete").Visible = part.CanDelete;
    }

    /// <summary>The side panel glyph for the part whose settings are open.</summary>
    public static UiIconId PartSettingsIcon(PartSettingsKind kind) => kind switch
    {
        PartSettingsKind.Node => UiIconId.Joint,
        PartSettingsKind.Beam => UiIconId.Beam,
        PartSettingsKind.Accelerometer => UiIconId.PartAccelerometer,
        PartSettingsKind.Camera => UiIconId.PartCamera,
        PartSettingsKind.Piston => UiIconId.PartPiston,
        _ => UiIconId.None,
    };

    private void ApplyReadiness(BuildPanelPresentation buildPanel)
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
