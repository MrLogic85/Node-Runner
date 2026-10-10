using Godot;
using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
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
    private (int Selected, PartSettingsKind? Kind, int? PanelId, ToolPanelMode Mode, bool Locked)? _sidePanelShows;
    private string? _renamingDefaultName;
    private bool _subscribedToPresentation;
    private UiSlider? _hintSlider;
    private string? _shownPartGroup;
    private int _shownServoPickerId;
    private PartPickerPresentation? _fixedPicker;
    private PartPickerPresentation? _targetPicker;

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void StartTrainingRequestedEventHandler();

    [Signal]
    public delegate void CreationNameChangedEventHandler(string name);

    [Signal]
    public delegate void PartNameChangedEventHandler(int partId, string name, string shownDefault);

    [Signal]
    public delegate void UnlockRequestedEventHandler();

    [Signal]
    public delegate void UndoRequestedEventHandler();

    [Signal]
    public delegate void RedoRequestedEventHandler();

    [Signal]
    public delegate void ResetTrainingRequestedEventHandler();

    [Signal]
    public delegate void CopyCreationRequestedEventHandler();

    [Signal]
    public delegate void ShareBuildRequestedEventHandler();

    [Signal]
    public delegate void DeleteCreationRequestedEventHandler();

    [Signal]
    public delegate void DeleteSelectionRequestedEventHandler();

    [Signal]
    public delegate void CopySelectionRequestedEventHandler();

    /// <summary>A part or link row the locked Creation refuses was tapped (#896).</summary>
    [Signal]
    public delegate void CreationLockedPressedEventHandler();

    /// <summary>A setting's disabled slider was tapped where the setting does nothing as set now (#578).</summary>
    [Signal]
    public delegate void NoEffectSettingPressedEventHandler(int setting);

    /// <summary>A Coming later tray row was tapped (#992).</summary>
    [Signal]
    public delegate void ComingLaterPartPressedEventHandler(int part);

    /// <summary>A Links row not yet implemented was tapped (#992).</summary>
    [Signal]
    public delegate void ComingLaterLinkPressedEventHandler(int link);

    [Signal]
    public delegate void ToolRequestedEventHandler(BuildTool tool);

    /// <summary>A row in the Links tool's list was tapped (#705): the rail tool stays Links.</summary>
    [Signal]
    public delegate void LinkPickedEventHandler(int link);

    /// <summary>A tray part was tapped (#805); the view-model picks it, or clears it if it was picked.</summary>
    [Signal]
    public delegate void PartPickedEventHandler(int part);

    /// <summary>
    /// The picked part went out of sight (#805): another tray tab was opened or the side panel
    /// collapsed. The view-model clears the pick, so a tap never places a part the player cannot see.
    /// </summary>
    [Signal]
    public delegate void PartPickHiddenEventHandler();

    /// <summary>A setting's slider moved (#704): every selected part takes <paramref name="value"/> for <paramref name="parameter"/> (a <see cref="PartParameterId"/>).</summary>
    [Signal]
    public delegate void ParameterChangedEventHandler(int parameter, double value);

    /// <summary>A setting's slider was let go: its changes since the touch are one undo step (#689).</summary>
    [Signal]
    public delegate void ParameterChangeFinishedEventHandler();

    [Signal]
    public delegate void ServoLinkChangedEventHandler(int servoId, bool fixedRole, int linkId);

    /// <summary>An "On this joint" tab was tapped (#1044): select that part instead.</summary>
    [Signal]
    public delegate void JointPartChosenEventHandler(int kind, int partId);

    /// <summary>The Advanced settings section was opened or closed (#903).</summary>
    [Signal]
    public delegate void AdvancedSettingsToggledEventHandler(bool open);

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
        name.MaxLength = NameLimits.Creation;
        name.EditingFinished += OnNameEdited;
        GetNode<UiButton>("%StartTraining").Activated += () => EmitSignal(SignalName.StartTrainingRequested);
        GetNode<UiButton>("%Unlock").Activated += () => EmitSignal(SignalName.UnlockRequested);
        GetNode<UiButton>("%Undo").Activated += () => EmitSignal(SignalName.UndoRequested);
        GetNode<UiButton>("%Redo").Activated += () => EmitSignal(SignalName.RedoRequested);
        BindMenuItem(toolbar, GetNode<UiMenuActionItem>("%MenuResetTraining"), () => EmitSignal(SignalName.ResetTrainingRequested));
        BindMenuItem(toolbar, GetNode<UiMenuActionItem>("%MenuCopyCreation"), () => EmitSignal(SignalName.CopyCreationRequested));
        BindMenuItem(toolbar, GetNode<UiMenuActionItem>("%MenuShareBuild"), () => EmitSignal(SignalName.ShareBuildRequested));
        BindMenuItem(toolbar, GetNode<UiMenuActionItem>("%MenuDeleteCreation"), () => EmitSignal(SignalName.DeleteCreationRequested));
        BindTool(GetNode<UiButton>("%PartsTool"), BuildTool.Parts);
        BindTool(GetNode<UiButton>("%BeamTool"), BuildTool.Beam);
        BindTool(GetNode<UiButton>("%JointTool"), BuildTool.Joint);
        BindTool(GetNode<UiButton>("%SelectTool"), BuildTool.Select);
        var partTabs = GetNode<UiIconTabs>("%PartTabs");
        partTabs.SelectedIndex = PartTray.OpeningGroup();
        partTabs.TabSelected += OnPartTabSelected;
        GetNode<UiIconTabs>("%PartStackTabs").TabSelected += OnPartStackTabSelected;
        GetNode<UiButton>("%PartDelete").Activated += () => EmitSignal(SignalName.DeleteSelectionRequested);
        GetNode<UiButton>("%SelectionDelete").Activated += () => EmitSignal(SignalName.DeleteSelectionRequested);
        GetNode<UiButton>("%SelectionCopy").Activated += () => EmitSignal(SignalName.CopySelectionRequested);
        var partName = GetNode<UiTextField>("%PartName");
        partName.MaxLength = NameLimits.Part;
        partName.EditingStarted += OnPartNameEditingStarted;
        partName.EditingFinished += OnPartNameEdited;
        GetNode<UiSidePanel>("%SidePanel").CollapsedChanged += collapsed =>
        {
            HideSettingHint();
            if (collapsed)
            {
                EmitSignal(SignalName.PartPickHidden);
            }
        };
        GetNode<UiPicker>("%FixedPicker").SelectionChanged += selected => OnServoPickerChanged(selected, fixedRole: true);
        GetNode<UiPicker>("%TargetPicker").SelectionChanged += selected => OnServoPickerChanged(selected, fixedRole: false);
        GetNode<UiExpandSection>("%PartAdvanced").Toggled += open => EmitSignal(SignalName.AdvancedSettingsToggled, open);
        GetNode<UiExpandSection>("%SelectionAdvanced").Toggled += open => EmitSignal(SignalName.AdvancedSettingsToggled, open);
        BindViewModels();
        Apply();
    }

    public override void _ExitTree()
    {
        UnsubscribeFromPresentation();
    }

    // The part name field holds a translated default as editable text, which LineEdit never
    // translates itself; refresh it in place, as a language change may not add children.
    public override void _Notification(int what)
    {
        if (what == NotificationTranslationChanged && IsNodeReady() && _presentation?.SinglePart is { } part)
        {
            var name = GetNode<UiTextField>("%PartName");
            if (name.State != UiTextField.TextInputState.Editing)
            {
                name.TextValue = UiTextTranslation.Source(part.Name)();
            }
        }
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

    private void OnPartNameEditingStarted()
    {
        var part = _presentation?.SinglePart;
        _renamingPartId = part?.Id;
        _renamingDefaultName = part is null ? null : UiTextTranslation.Source(part.DefaultName)();
    }

    // The field shows a part without its own name by its default in the player's language; the
    // view-model keeps the default when that is left unchanged.
    private void OnPartNameEdited(string value)
    {
        if (_renamingPartId is { } partId)
        {
            _renamingPartId = null;
            EmitSignal(SignalName.PartNameChanged, partId, value, _renamingDefaultName ?? string.Empty);
        }
    }

    /// <summary>
    /// The basic sliders in <paramref name="basic"/>, then the advanced ones in
    /// <paramref name="advancedBody"/> under <paramref name="advanced"/> (#903), which shows only
    /// when there are any. <paramref name="gap"/> widens the space above the section to space-2
    /// when basic sliders come before it.
    /// </summary>
    private void ApplyParameterSliders(
        Container basic,
        Control gap,
        UiExpandSection advanced,
        Container advancedBody,
        IReadOnlyList<ParameterSlider> basicSettings,
        IReadOnlyList<ParameterSlider> advancedSettings,
        bool advancedOpen)
    {
        basic.Visible = basicSettings.Count > 0;
        ApplySliders(basic, basicSettings);
        advanced.Visible = advancedSettings.Count > 0;
        gap.Visible = basic.Visible && advanced.Visible;
        if (advanced.Open != advancedOpen)
        {
            advanced.Open = advancedOpen;
        }

        ApplySliders(advancedBody, advancedSettings);
    }

    /// <summary>
    /// One slider per setting in <paramref name="settings"/>, in order (#704). A setting's slider is
    /// made the first time it shows; differing values have Marker ends and no thumb, and a disabled
    /// setting's slider is dashed and ignores touches (#578); a tap on it says why: the creation
    /// is locked, like a locked tray row (#896), or the setting does nothing as set now (#578).
    /// </summary>
    private void ApplySliders(Container container, IReadOnlyList<ParameterSlider> settings)
    {
        foreach (var slider in container.GetChildren().OfType<UiSlider>())
        {
            slider.Visible = settings.Any(setting => setting.Id.ToString() == slider.Name);
        }

        for (var index = 0; index < settings.Count; index++)
        {
            var setting = settings[index];
            var slider = container.GetNodeOrNull<UiSlider>(setting.Id.ToString()) ?? AddParameterSlider(container, setting.Id);
            container.MoveChild(slider, index);
            slider.LabelSource = UiTextTranslation.Source(setting.Label);
            slider.ReadoutSource = UiTextTranslation.Source(setting.Readout);
            slider.Step = setting.Step;
            slider.Value = setting.ValuesDiffer
                ? new UiSliderValue(UiSliderEnd.Marker(setting.Low), UiSliderEnd.Marker(setting.High))
                : UiSliderValue.Thumb(setting.High);
            if (slider.Disabled != setting.Disabled)
            {
                slider.Disabled = setting.Disabled;
            }

            _shownSettings[setting.Id] = setting;
        }
    }

    // Each panel slider's setting as last shown, so a tap on a disabled one says why.
    private readonly Dictionary<PartParameterId, ParameterSlider> _shownSettings = [];

    private UiSlider AddParameterSlider(Container container, PartParameterId id)
    {
        var slider = new UiSlider { Name = id.ToString(), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        slider.ThumbChanged += (_, position) => ChangeParameter(id, position);
        slider.ThumbChangeCommitted += (_, _) => EmitSignal(SignalName.ParameterChangeFinished);
        slider.TouchStarted += () => ShowSettingHint(slider, id);
        slider.TouchEnded += () => LetGoOfSettingHint(slider);
        slider.DisabledPressed += () =>
        {
            if (_shownSettings[id].Locked)
            {
                EmitSignal(SignalName.CreationLockedPressed);
            }
            else if (_shownSettings[id].NoEffect)
            {
                EmitSignal(SignalName.NoEffectSettingPressed, (int)id);
            }
        };

        // Differing values have no thumb: a touch sets one value for all of them.
        slider.TrackPressed += position =>
        {
            slider.Value = UiSliderValue.Thumb(position);
            ChangeParameter(id, position);
        };
        container.AddChild(slider);
        return slider;
    }

    private void ChangeParameter(PartParameterId id, double position)
    {
        EmitSignal(SignalName.ParameterChanged, (int)id, PartParameters.ValueAt(id, position));
        GetNode<BuildCanvas>("%BuildCanvas").ShowCameraRays();
    }

    /// <summary>
    /// What the touched setting does, in a fixed spot over the canvas (#867). It shows at once and
    /// stays a moment after the finger lifts; a touch on another slider swaps the text.
    /// </summary>
    private void ShowSettingHint(UiSlider slider, PartParameterId id)
    {
        if (PartParameters.Of(id).Slider is not { } scale)
        {
            return;
        }

        _hintSlider = slider;
        GetNode<UiLabel>("%SettingHintTitle").TextSource = UiTextTranslation.Source(scale.Label);
        GetNode<UiLabel>("%SettingHintBody").TextSource = UiTextTranslation.Source(scale.Help);
        GetNode<UiHintCard>("%SettingHint").ShowNow();
    }

    // Only the finger that showed the hint lifting lets it go; a newer touch owns it.
    private void LetGoOfSettingHint(UiSlider slider)
    {
        if (slider == _hintSlider)
        {
            _hintSlider = null;
            GetNode<UiHintCard>("%SettingHint").HideAfterLinger();
        }
    }

    private void HideSettingHint()
    {
        _hintSlider = null;
        GetNode<UiHintCard>("%SettingHint").HideNow();
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
        ApplySidePanel(presentation, buildPanel, locked);
    }

    private void ApplyToolbar(BuildPresentationViewModel presentation, BuildPanelPresentation buildPanel, bool locked)
    {
        var name = GetNode<UiTextField>("%CreationName");
        if (name.State != UiTextField.TextInputState.Editing)
        {
            name.TextValue = presentation.CreationName;
        }

        GetNode<UiButton>("%StartTraining").Unavailable = !buildPanel.CanStartTraining;
        GetNode<UiButton>("%Unlock").Visible = locked;
        GetNode<UiButton>("%Undo").Disabled = !presentation.CanUndo;
        GetNode<UiButton>("%Redo").Disabled = !presentation.CanRedo;
        GetNode<UiMenuActionItem>("%MenuStats").Visible = presentation.IsTrained;
        GetNode<UiMenuActionItem>("%MenuResetTraining").Visible = presentation.IsTrained;
        GetNode<UiMenuActionItem>("%MenuCopyCreation").Visible = presentation.CanCopy;
        GetNode<UiMenuActionItem>("%MenuShareBuild").Visible = presentation.CanCopy;
    }

    private void ApplyTools(BuildPresentationViewModel presentation)
    {
        GetNode<UiButton>("%PartsTool").Selected = presentation.ActiveTool == BuildTool.Parts;
        var beam = GetNode<UiButton>("%BeamTool");
        beam.Selected = presentation.ActiveTool == BuildTool.Beam;
        var joint = GetNode<UiButton>("%JointTool");
        joint.Selected = presentation.ActiveTool == BuildTool.Joint;
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
        var jointHelp = GetNode<Control>("%JointHelp");
        var selectHelp = GetNode<Control>("%SelectHelp");
        var partSettings = GetNode<Control>("%PartSettings");
        var selection = GetNode<Control>("%Selection");
        var toolPanel = presentation.ToolPanel;
        var linkList = presentation.LinkList;
        tray.Visible = toolPanel.Mode is ToolPanelMode.PartsTray or ToolPanelMode.LinkList;
        jointHelp.Visible = toolPanel.Mode == ToolPanelMode.JointHelp;
        selectHelp.Visible = toolPanel.Mode == ToolPanelMode.SelectHelp;
        GetNode<Control>("%PanelSpacer").Visible = !tray.Visible;
        partSettings.Visible = selected == 1;
        selection.Visible = selected > 1;
        GetNode<Control>("%Readiness").Visible = selected == 0;
        var part = presentation.SinglePart;
        var sidePanel = GetNode<UiSidePanel>("%SidePanel");
        var shows = (selected, part?.Kind, part?.PanelId, toolPanel.Mode, locked);
        if (_sidePanelShows is { } showed && showed != shows)
        {
            sidePanel.ScrollContentToTop();
            HideSettingHint();
        }

        _sidePanelShows = shows;
        sidePanel.TitleSource = UiTextTranslation.Source(
            selected == 1 ? part?.Title
            : selected > 1 ? presentation.Selection?.Title
            : toolPanel.Title);
        sidePanel.IconId = selected > 1 ? UiIconId.Select : part is null ? UiIconId.None : PartSettingsIcon(part.Kind);

        if (tray.Visible)
        {
            ApplyPickList(presentation, linkList);
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
        ApplyParameterSliders(
            GetNode<Container>("%SelectionParameters"),
            GetNode<Control>("%SelectionAdvancedGap"),
            GetNode<UiExpandSection>("%SelectionAdvanced"),
            GetNode<Container>("%SelectionAdvancedParameters"),
            group.BasicSettings,
            group.AdvancedSettings,
            _presentation?.AdvancedSettingsOpen == true);
        GetNode<Control>("%SelectionSettings").Visible = group.Settings.Count > 0;
        GetNode<UiLabel>("%SelectionSettingsNote").TextSource = UiTextTranslation.Source(group.SettingsNote);
        var emptyNote = GetNode<UiLabel>("%SelectionEmptyNote");
        emptyNote.TextSource = UiTextTranslation.Source(group.EmptyNote);
        emptyNote.Visible = emptyNote.TextSource is not null;
        GetNode<Control>("%SelectionRows").Visible = group.ShowFrameRows;
        var copy = GetNode<UiButton>("%SelectionCopy");
        copy.ShowText(group.CopyText);
        copy.Unavailable = !group.CanCopy;
        var delete = GetNode<UiButton>("%SelectionDelete");
        delete.ShowText(group.DeleteText);
        delete.Unavailable = !group.CanDelete;
        var deleteNote = GetNode<UiLabel>("%SelectionDeleteNote");
        deleteNote.TextSource = UiTextTranslation.Source(group.DeleteNote);
        deleteNote.Visible = deleteNote.TextSource is not null;
    }

    private void ApplyPickList(BuildPresentationViewModel presentation, LinkListPresentation? linkList)
    {
        // The Links panel's title already says Links, so its list has no tabs or group header (#913).
        GetNode<UiIconTabs>("%PartTabs").Visible = linkList is null;
        GetNode<Control>("%PartGroupHeader").Visible = linkList is null;
        if (linkList is not null)
        {
            ApplyLinkList(linkList, presentation.IsLocked);
        }
        else
        {
            ApplyTray(presentation);
        }
    }

    private void ApplyLinkList(LinkListPresentation list, bool locked)
    {
        var help = GetNode<UiLabel>("%PartHelp");
        help.ShowText(list.HelpText);
        help.Visible = true;
        var rows = GetNode<UiPickList>("%PartRows");
        if (_shownPartGroup != $"links:{locked}")
        {
            _shownPartGroup = $"links:{locked}";
            ClearRows(rows);
            foreach (var link in list.Rows)
            {
                var row = new UiPartRow { IconId = LinkIcon(link.Link), LabelSource = UiTextTranslation.Source(link.Name), Compact = true };
                if (link.IsPickable)
                {
                    row.PartSelected += () => EmitSignal(SignalName.LinkPicked, (int)link.Link);
                }
                else if (link.State == LinkListRowState.CreationLocked)
                {
                    row.LockedPressed += () => EmitSignal(SignalName.CreationLockedPressed);
                }
                else if (link.State == LinkListRowState.Locked)
                {
                    row.LockedPressed += () => EmitSignal(SignalName.ComingLaterLinkPressed, (int)link.Link);
                }

                rows.AddChild(row);
            }
        }

        var linkRows = rows.GetChildren().OfType<UiPartRow>().ToList();
        UiPartRow? picked = null;
        for (var index = 0; index < list.Rows.Count; index++)
        {
            linkRows[index].State = list.Rows[index].State switch
            {
                LinkListRowState.Selected => UiPartRow.PartRowState.Selected,
                LinkListRowState.Locked or LinkListRowState.CreationLocked => UiPartRow.PartRowState.Locked,
                _ => UiPartRow.PartRowState.Rest,
            };
            if (list.Rows[index].State == LinkListRowState.Selected)
            {
                picked = linkRows[index];
            }
        }

        ShowPickedInfo(rows, picked, UiTextTranslation.Source(list.PickedInfo));
    }

    // The picked row's info sits right under it, in the Links list and the Parts tray alike.
    private void ShowPickedInfo(UiPickList rows, UiPartRow? picked, Func<string>? info)
    {
        GetNode<UiLabel>("%PickedInfo").TextSource = info;
        rows.ShowInfoUnder(info is null ? null : picked);
    }

    private void ApplyTray(BuildPresentationViewModel presentation)
    {
        var tab = GetNode<UiIconTabs>("%PartTabs").SelectedIndex;
        var tray = presentation.Tray;
        var group = tray.Groups[tab];
        GetNode<UiLabel>("%PartGroupName").ShowText(group.Name);
        var help = GetNode<UiLabel>("%PartHelp");
        help.ShowText(tray.HelpText);
        help.Visible = true;
        var rows = GetNode<UiPickList>("%PartRows");
        // Unlocking rebuilds the rows, so they can be dragged.
        if (_shownPartGroup != $"tray:{tab}:{presentation.IsLocked}")
        {
            _shownPartGroup = $"tray:{tab}:{presentation.IsLocked}";
            ClearRows(rows);

            foreach (var part in group.Rows)
            {
                var row = new UiPartRow { IconId = PartIcon(part.Part), LabelSource = UiTextTranslation.Source(part.Name), Compact = true };
                if (DraggablePart(part) is { } draggable)
                {
                    row.SetDragForwarding(
                        Callable.From<Vector2, Variant>(_ => StartPartDrag(row, draggable)),
                        new Callable(),
                        new Callable());
                    row.PartSelected += () => EmitSignal(SignalName.PartPicked, (int)draggable);
                }
                else if (part.State == PartTrayRowState.CreationLocked)
                {
                    row.LockedPressed += () => EmitSignal(SignalName.CreationLockedPressed);
                }
                else if (part.State == PartTrayRowState.ComingLater)
                {
                    row.LockedPressed += () => EmitSignal(SignalName.ComingLaterPartPressed, (int)part.Part);
                }

                rows.AddChild(row);
            }
        }

        var partRows = rows.GetChildren().OfType<UiPartRow>().ToList();
        UiPartRow? picked = null;
        for (var index = 0; index < group.Rows.Count; index++)
        {
            var row = group.Rows[index];
            partRows[index].State = row.State switch
            {
                PartTrayRowState.Available => UiPartRow.PartRowState.Rest,
                PartTrayRowState.Selected => UiPartRow.PartRowState.Selected,
                _ => UiPartRow.PartRowState.Locked,
            };
            if (row.State == PartTrayRowState.Selected)
            {
                picked = partRows[index];
            }
        }

        ShowPickedInfo(rows, picked, UiTextTranslation.Source(tray.PickedInfo));
    }

    // Frees the rows built in code; the scene's %PickedInfo label stays.
    private static void ClearRows(Container rows)
    {
        foreach (var child in rows.GetChildren().OfType<UiPartRow>())
        {
            rows.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void OnPartTabSelected(int index)
    {
        GetNode<ScrollContainer>("%PartScroll").ScrollVertical = 0;
        EmitSignal(SignalName.PartPickHidden);
        Apply();
    }

    /// <summary>The part a tray row can be dragged out as (#376, #577): an available sensor or joint part (Servo or Wheel, #129), or null.</summary>
    public static BuildPart? DraggablePart(PartTrayRow row) =>
        row.IsAvailable && (PartTray.SensorKindOf(row.Part) is not null || PartTray.IsJointPart(row.Part)) ? row.Part : null;

    /// <summary>Lifts the part out of its row: the canvas takes the drop, and the row's glyph floats above the finger.</summary>
    private Variant StartPartDrag(UiPartRow row, BuildPart part)
    {
        if (_presentation is null || !PartTray.CanPick(part, _presentation.IsLocked))
        {
            return default;
        }

        row.SetDragPreview(row.CreateDragPreview());
        return BuildCanvas.PartDragData(part);
    }

    /// <summary>The Links tool's list glyph for a link.</summary>
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
        BuildPart.TouchSensor => UiIconId.PartTouch,
        BuildPart.Accelerometer => UiIconId.PartAccelerometer,
        BuildPart.Camera => UiIconId.PartCamera,
        BuildPart.Pulse => UiIconId.PartPulse,
        BuildPart.Battery => UiIconId.PartBattery,
        BuildPart.Generator => UiIconId.PartGenerator,
        BuildPart.FuelTank => UiIconId.PartFuel,
        _ => UiIconId.None,
    };

    private void ApplyPartSettings(PartSettingsPresentation part)
    {
        var name = GetNode<UiTextField>("%PartName");
        name.PlaceholderSource = UiTextTranslation.Source(part.DefaultName);
        if (name.State != UiTextField.TextInputState.Editing)
        {
            name.TextValue = UiTextTranslation.Source(part.Name)();
        }

        var connectionsLabel = GetNode<UiLabel>("%PartConnectionsLabel");
        connectionsLabel.TextSource = UiTextTranslation.Source(part.ConnectionsLabel);
        GetNode<UiLabel>("%PartConnectionsValue").TextSource = UiTextTranslation.Source(part.ConnectionsValue);
        connectionsLabel.GetParent<Control>().Visible = connectionsLabel.TextSource is not null;
        ApplyParameterSliders(
            GetNode<Container>("%PartParameters"),
            GetNode<Control>("%PartAdvancedGap"),
            GetNode<UiExpandSection>("%PartAdvanced"),
            GetNode<Container>("%PartAdvancedParameters"),
            part.BasicSettings,
            part.AdvancedSettings,
            _presentation?.AdvancedSettingsOpen == true);
        ApplyPartReadouts(GetNode<Container>("%PartReadouts"), part.Readouts ?? []);
        ApplyPartPickers(part);
        ApplyPartStack(part);

        GetNode<UiLabel>("%PartNote").ShowText(part.Note);
        GetNode<UiButton>("%PartDelete").Unavailable = !part.CanDelete;
    }

    /// <summary>The "On this joint" tabs (#1044), top → down, shown only when the part's joint holds two or more parts.</summary>
    private void ApplyPartStack(PartSettingsPresentation part)
    {
        GetNode<Control>("%PartStack").Visible = part.OnThisJoint is not null;
        if (part.OnThisJoint is not { } tabs)
        {
            return;
        }

        var strip = GetNode<UiIconTabs>("%PartStackTabs");
        var icons = new Godot.Collections.Array<UiIconId>(tabs.Select(tab => PartSettingsIcon(tab.Kind)));
        if (!strip.Icons.SequenceEqual(icons))
        {
            strip.Icons = icons;
        }

        strip.SelectedIndex = part.OnThisJointIndex ?? 0;
    }

    private void OnPartStackTabSelected(int index)
    {
        if (_presentation?.SinglePart?.OnThisJoint is { } tabs && index < tabs.Count)
        {
            EmitSignal(SignalName.JointPartChosen, (int)tabs[index].Part.Kind, tabs[index].Part.Id);
        }
    }

    /// <summary>One read-only value row per readout, such as a Wheel's Weight (#129), after the basic settings.</summary>
    private static void ApplyPartReadouts(Container container, IReadOnlyList<PartReadout> readouts)
    {
        container.Visible = readouts.Count > 0;
        var rows = container.GetChildren().OfType<UiValueRow>().ToList();
        foreach (var extra in rows.Skip(readouts.Count))
        {
            container.RemoveChild(extra);
            extra.QueueFree();
        }

        for (var index = 0; index < readouts.Count; index++)
        {
            var row = index < rows.Count ? rows[index] : AddChildTo(container, new UiValueRow());
            row.LabelSource = UiTextTranslation.Source(readouts[index].Label);
            row.ValueSource = UiTextTranslation.Source(readouts[index].Value);
        }

        static UiValueRow AddChildTo(Container container, UiValueRow row)
        {
            container.AddChild(row);
            return row;
        }
    }

    private void ApplyPartPickers(PartSettingsPresentation part)
    {
        var container = GetNode<VBoxContainer>("%PartPickers");
        var fixedPicker = GetNode<UiPicker>("%FixedPicker");
        var targetPicker = GetNode<UiPicker>("%TargetPicker");
        _shownServoPickerId = part.Id;
        var pickers = part.Pickers ?? [];
        container.Visible = pickers.Count > 0;
        fixedPicker.Visible = pickers.Count > 0;
        targetPicker.Visible = pickers.Count > 1;
        _fixedPicker = null;
        _targetPicker = null;

        for (var index = 0; index < pickers.Count; index++)
        {
            var presentation = pickers[index];
            var fixedRole = index == 0;
            var picker = fixedRole ? fixedPicker : targetPicker;
            var iconTint = fixedRole ? UiTokens.Color.Detail : UiTokens.Color.Accent;
            picker.SetPresentation(
                UiTextTranslation.Source(presentation.Label)(),
                presentation.Options.Select((option, optionIndex) => new UiPickerOption(
                    UiTextTranslation.Source(option)(),
                    IconForLinkKind(presentation.LinkKindAt(optionIndex)),
                    IconTint: iconTint)).ToArray(),
                presentation.SelectedIndex ?? -1,
                disabled: false,
                UiTextTranslation.Source(presentation.Note)?.Invoke() ?? string.Empty,
                UiTextTranslation.Source(presentation.Placeholder)?.Invoke() ?? string.Empty);
            picker.State = presentation.IsLocked ? UiPicker.PickerState.Locked : UiPicker.PickerState.Collapsed;
            if (fixedRole)
            {
                _fixedPicker = presentation;
            }
            else
            {
                _targetPicker = presentation;
            }
        }

        static UiIconId? IconForLinkKind(CreatureElementKind? kind) => kind switch
        {
            CreatureElementKind.Beam => UiIconId.PartBeam,
            CreatureElementKind.Piston => UiIconId.PartPiston,
            CreatureElementKind.Spring => UiIconId.PartSpring,
            _ => null,
        };
    }

    private void OnServoPickerChanged(int selected, bool fixedRole)
    {
        var picker = fixedRole ? _fixedPicker : _targetPicker;
        if (picker?.LinkIdAt(selected) is { } linkId)
        {
            EmitSignal(SignalName.ServoLinkChanged, _shownServoPickerId, fixedRole, linkId);
        }
    }

    /// <summary>The side panel glyph for the part whose settings are open.</summary>
    public static UiIconId PartSettingsIcon(PartSettingsKind kind) => kind switch
    {
        PartSettingsKind.Node => UiIconId.Joint,
        PartSettingsKind.Beam => UiIconId.Beam,
        PartSettingsKind.Accelerometer => UiIconId.PartAccelerometer,
        PartSettingsKind.Camera => UiIconId.PartCamera,
        PartSettingsKind.Servo => UiIconId.PartServo,
        PartSettingsKind.Piston => UiIconId.PartPiston,
        PartSettingsKind.Spring => UiIconId.PartSpring,
        PartSettingsKind.Wheel => UiIconId.PartWheel,
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
        text.ShowText(buildPanel.ReadinessText);
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
