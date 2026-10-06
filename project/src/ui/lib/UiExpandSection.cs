using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A header row that opens and closes the rows below it (#928). The header is a full-width
/// <c>control-sm</c> tap target with an Overline label and a chevron. The section's own children are
/// the body, stacked with the panel's <c>space-1</c> gap. Rows that are shown or hidden by
/// themselves must sit inside a body container, because the section sets each child's visibility.
/// The owner decides the open state: a tap flips <see cref="Open"/> and raises
/// <see cref="ToggledEventHandler"/> so the owner can keep the state.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiExpandSection : VBoxContainer, ISerializationListener
{
    [Signal]
    public delegate void ToggledEventHandler(bool open);

    private sealed record Header(Button Button, Label Label, TextureRect Chevron);

    private string _labelText = "Advanced";
    private bool _open;
    private Header? _header;
    private Callable PressedCallback => new(this, MethodName.OnHeaderPressed);
    private Callable DrawPressCallback => new(this, MethodName.DrawPress);

    /// <summary>The header row's height: <c>control-sm</c>, the whole row being the tap target.</summary>
    public static int HeaderHeight => UiSize.Control.Small;

    public static UiIconId ChevronFor(bool open) => open ? UiIconId.ChevronDown : UiIconId.ChevronRight;

    [Export]
    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value;
            ApplyContent();
        }
    }

    [Export]
    public bool Open
    {
        get => _open;
        set
        {
            _open = value;
            ApplyContent();
            ApplyOpen();
        }
    }

    public override void _EnterTree() => RequestReady();

    public override void _ExitTree() => DisconnectHeader();

    // An assembly reload in the editor drops _header; the header node survives and is found again.
    public void OnBeforeSerialize() => DisconnectHeader();

    public void OnAfterDeserialize() => CallDeferred(MethodName.RestoreHeader);

    private void RestoreHeader()
    {
        if (IsInsideTree())
        {
            InitializeHeader();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        AddThemeConstantOverride("separation", UiSize.Space.S1);
        InitializeHeader();
    }

    private void InitializeHeader()
    {
        DisconnectHeader();
        _header = RecoverHeader() ?? CreateHeader();
        ConnectHeader(_header);
        ApplyContent();
        ApplyOpen();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, ApplyContent);
        }
        else if (what == NotificationChildOrderChanged && IsNodeReady())
        {
            ApplyOpen();
        }
    }

    private Header? RecoverHeader()
    {
        foreach (Node child in GetChildren(includeInternal: true))
        {
            if (child is Button button
                && button.GetChildren(includeInternal: true).OfType<HBoxContainer>().FirstOrDefault() is { } row
                && row.GetChildren(includeInternal: true).OfType<Label>().FirstOrDefault() is { } label
                && row.GetChildren(includeInternal: true).OfType<TextureRect>().FirstOrDefault() is { } chevron)
            {
                return new Header(button, label, chevron);
            }
        }

        return null;
    }

    private Header CreateHeader()
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(0, HeaderHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass,
        };
        var empty = new StyleBoxEmpty();
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
        {
            button.AddThemeStyleboxOverride(state, empty);
        }
        AddChild(button, false, InternalMode.Front);

        var row = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        row.AddThemeConstantOverride("separation", UiSize.Space.S2);
        button.AddChild(row, false, InternalMode.Front);

        var label = UiFieldAndRows.Label(LabelText, UiTokens.Typography.Overline, UiTokens.Color.Muted);
        UiTranslation.ShareContext(this, label);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label, false, InternalMode.Front);

        var chevron = UiFieldAndRows.Icon(ChevronFor(Open), UiIconSize.Standard, UiThemeLookup.Color(this, UiTokens.Color.Muted));
        chevron.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(chevron, false, InternalMode.Front);
        return new Header(button, label, chevron);
    }

    private void ConnectHeader(Header header)
    {
        if (!header.Button.IsConnected(BaseButton.SignalName.Pressed, PressedCallback))
        {
            header.Button.Connect(BaseButton.SignalName.Pressed, PressedCallback);
        }
        if (!header.Button.IsConnected(CanvasItem.SignalName.Draw, DrawPressCallback))
        {
            header.Button.Connect(CanvasItem.SignalName.Draw, DrawPressCallback);
        }
    }

    private void DisconnectHeader()
    {
        if (_header is not { } header)
        {
            return;
        }

        if (header.Button.IsConnected(BaseButton.SignalName.Pressed, PressedCallback))
        {
            header.Button.Disconnect(BaseButton.SignalName.Pressed, PressedCallback);
        }
        if (header.Button.IsConnected(CanvasItem.SignalName.Draw, DrawPressCallback))
        {
            header.Button.Disconnect(CanvasItem.SignalName.Draw, DrawPressCallback);
        }
    }

    private void OnHeaderPressed()
    {
        Open = !Open;
        EmitSignal(SignalName.Toggled, Open);
    }

    // The shared press tint (#325) reaches space-2 past the row's sides, as on UiChoiceRow, so its
    // corners clear the label and the chevron.
    private void DrawPress()
    {
        if (_header is { } header && UiPressFeedback.Shows(header.Button, selected: false))
        {
            UiCorners.Uniform(UiSize.Radius.Small).Fill(
                header.Button,
                new Rect2(Vector2.Zero, header.Button.Size).GrowIndividual(UiSize.Space.S2, 0, UiSize.Space.S2, 0),
                UiPressFeedback.Tint(this, UiTokens.Color.Panel, danger: false));
        }
    }

    private void ApplyContent()
    {
        if (_header is not { } header)
        {
            return;
        }

        UiTranslation.ShareContext(this, header.Label);
        UiTranslation.ShareContext(this, header.Button);
        header.Label.Text = LabelText;
        header.Button.TooltipText = LabelText;
        UiThemeLookup.ApplyTextStyle(header.Label, UiTokens.Typography.Overline, UiTokens.Color.Muted);
        header.Chevron.Texture = UiIcons.Load(ChevronFor(Open), UiIconSize.Standard);
        header.Chevron.SelfModulate = UiThemeLookup.Color(this, UiTokens.Color.Muted);
        UiIcons.UseIconFilter(header.Chevron);
    }

    // In the editor the body stays shown, so hiding it never saves `visible = false` into a scene.
    private void ApplyOpen()
    {
        var shown = Open || Engine.IsEditorHint();
        foreach (var child in GetChildren().OfType<CanvasItem>())
        {
            child.Visible = shown;
        }
    }
}
