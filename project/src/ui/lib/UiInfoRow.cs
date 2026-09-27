using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Info row (<c>c_info_row</c>): a ringed halo icon with a title and one line of help, used to
/// explain a handle or a mode.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiInfoRow : HBoxContainer
{
    private const string _ringName = "_UiRing";
    private const string _textName = "_UiText";

    private UiIconId _iconId = UiIconId.None;
    private string _title = string.Empty;
    private string _help = string.Empty;
    private PanelContainer? _ring;
    private TextureRect? _icon;
    private UiLabel? _titleLabel;
    private UiLabel? _helpLabel;

    public UiInfoRow() => MouseFilter = MouseFilterEnum.Pass;

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid info row icon: {value}. Keeping {_iconId}.");
                return;
            }

            _iconId = value;
            Refresh();
        }
    }

    [Export]
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            Refresh();
        }
    }

    [Export(PropertyHint.MultilineText)]
    public string Help
    {
        get => _help;
        set
        {
            _help = value;
            Refresh();
        }
    }

    public override void _EnterTree() => RequestReady();

    public override void _Ready() => Refresh();

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, Refresh);
        }
    }

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        // Re-found after a C# assembly reload, which clears managed fields but keeps the children.
        EnsureContent();
        AddThemeConstantOverride("separation", UiSize.Space.S2);

        var halo = UiThemeLookup.Color(this, UiTokens.Color.Halo);
        _ring!.AddThemeStyleboxOverride("panel", UiThemeLookup.CreateStyleBox(
            Colors.Transparent,
            halo,
            borderWidth: UiSize.Stroke.InfoRing,
            radius: UiSize.Radius.Pill));
        bool hasIcon = IconId != UiIconId.None;
        _icon!.Visible = hasIcon;
        _icon.Texture = hasIcon ? UiIcons.Load(IconId, UiIconSize.Standard) : null;
        _icon.SelfModulate = halo;

        _titleLabel!.Text = Title;
        _helpLabel!.Text = Help;
        _helpLabel.Visible = Help.Length > 0;
    }

    private void EnsureContent()
    {
        if (_ring is not null)
        {
            return;
        }

        if (GetNodeOrNull<PanelContainer>(_ringName) is { } ring
            && GetNodeOrNull<VBoxContainer>(_textName) is { } text)
        {
            _ring = ring;
            _icon = ring.GetNode<TextureRect>("Icon");
            _titleLabel = text.GetNode<UiLabel>("Title");
            _helpLabel = text.GetNode<UiLabel>("Help");
            return;
        }

        _ring = new PanelContainer
        {
            Name = _ringName,
            CustomMinimumSize = Vector2.One * UiSize.Control.Small,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        _icon = new TextureRect
        {
            Name = "Icon",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = Vector2.One * UiIcons.Pixels(UiIconSize.Standard),
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        _ring.AddChild(_icon);

        var textColumn = new VBoxContainer
        {
            Name = _textName,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        textColumn.AddThemeConstantOverride("separation", 0);
        _titleLabel = new UiLabel
        {
            Name = "Title",
            TextStyle = UiTokens.Typography.SmallStrong,
            TextColor = UiTokens.Color.Ink,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _helpLabel = new UiLabel
        {
            Name = "Help",
            TextStyle = UiTokens.Typography.Note,
            TextColor = UiTokens.Color.Muted,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        textColumn.AddChild(_titleLabel);
        textColumn.AddChild(_helpLabel);

        AddChild(_ring);
        AddChild(textColumn);
    }
}
