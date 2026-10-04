using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Small fact chip (<c>c_chip</c>), one size (control-xs), with an optional icon. Kind colours the
/// border, text and icon alike. The icon is icon-sm, or icon (16) for a part glyph.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiChip : PanelContainer
{
    public enum ChipKind
    {
        Neutral,
        Warning,
        Danger,
        Ok,
    }

    private ChipKind _kind;
    private string _text = string.Empty;
    private UiIconId _iconId = UiIconId.None;
    private bool _glyphSizedIcon;
    private UiIconCaption? _content;

    [Export]
    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            Refresh();
        }
    }

    [Export]
    public ChipKind Kind
    {
        get => _kind;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid chip kind: {value}. Keeping {_kind}.");
                return;
            }

            _kind = value;
            Refresh();
        }
    }

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid chip icon: {value}. Keeping {_iconId}.");
                return;
            }

            _iconId = value;
            Refresh();
        }
    }

    /// <summary>
    /// Draws a UI icon at the part-glyph size (icon, 16), for a chip that sits beside chips
    /// showing part glyphs. A part glyph is always that size.
    /// </summary>
    [Export]
    public bool GlyphSizedIcon
    {
        get => _glyphSizedIcon;
        set
        {
            _glyphSizedIcon = value;
            Refresh();
        }
    }

    /// <summary>Text, icon and border colour for a kind; neutral text is ink on a line-strong border.</summary>
    public static (UiTokens.Color Border, UiTokens.Color Content) ColorsFor(ChipKind kind) => kind switch
    {
        ChipKind.Warning => (UiTokens.Color.Halo, UiTokens.Color.Halo),
        ChipKind.Danger => (UiTokens.Color.Danger, UiTokens.Color.Danger),
        ChipKind.Ok => (UiTokens.Color.Accent, UiTokens.Color.Accent),
        _ => (UiTokens.Color.LineStrong, UiTokens.Color.Ink),
    };

    /// <summary>The icon size of a chip, and of a callout, which shares the chip's caption.</summary>
    public static UiIconSize IconSizeFor(UiIconId icon, bool glyphSized) =>
        glyphSized || UiIcons.IsPartGlyph(icon) ? UiIconSize.Standard : UiIconSize.Small;

    public override void _EnterTree() => RequestReady();

    public UiChip() => MouseFilter = MouseFilterEnum.Pass;

    public override void _Ready() => Refresh();

    private readonly UiUnsavedState _unsaved = new("theme_override_styles/panel");

    public override void _Notification(int what)
    {
        if (_unsaved.Handle(this, what, Refresh))
        {
            return;
        }

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

        // Re-found after a C# assembly reload, which clears managed fields but keeps the child.
        _content ??= UiIconCaption.Ensure(this);

        var (border, content) = ColorsFor(Kind);
        _content.Row.CustomMinimumSize = new Vector2(0, UiSize.Control.ExtraSmall);
        UiTranslation.ShareContext(this, _content.Label);
        _content.Apply(Text, IconId, IconSizeFor(IconId, GlyphSizedIcon), content, UiThemeLookup.Color(this, content));
        AddThemeStyleboxOverride("panel", UiThemeLookup.CreateStyleBox(
            UiThemeLookup.Color(this, UiTokens.Color.PanelRaised),
            UiThemeLookup.Color(this, border),
            radius: UiSize.Radius.Pill,
            horizontalPadding: UiSize.Space.S2 + UiSize.Stroke.Hair,
            verticalPadding: 0));
    }
}
