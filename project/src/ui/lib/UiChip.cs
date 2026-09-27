using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Small token-backed status, lock, unlock, or brain-shape label.</summary>
public partial class UiChip : PanelContainer
{
    public enum ChipKind
    {
        Neutral,
        Accent,
        Locked,
        Danger,
        Warning,
        Bad,
        Ok,
    }

    private ChipKind _kind;
    private string _text = string.Empty;
    private string _iconText = string.Empty;
    private UiIconId? _iconId;

    [Export]
    public ChipKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            Refresh();
        }
    }

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

    public UiIconId? IconId
    {
        get => _iconId;
        set
        {
            _iconId = value;
            Refresh();
        }
    }

    [Export]
    public string IconText
    {
        get => _iconText;
        set
        {
            _iconText = value;
            _iconId = UiIconGlyphs.TryParse(value, out var icon) ? icon : null;
            Refresh();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        Refresh();
    }

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

        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        CustomMinimumSize = new Vector2(0, UiSize.Control.ExtraSmall);
        var (lineToken, textToken) = Kind switch
        {
            ChipKind.Accent or ChipKind.Ok => (UiTokens.Color.Accent, UiTokens.Color.Accent),
            ChipKind.Locked => (UiTokens.Color.Muted, UiTokens.Color.Muted),
            ChipKind.Danger or ChipKind.Bad => (UiTokens.Color.Danger, UiTokens.Color.Danger),
            ChipKind.Warning => (UiTokens.Color.Halo, UiTokens.Color.Halo),
            _ => (UiTokens.Color.Edge, UiTokens.Color.Ink),
        };
        Color color = UiThemeLookup.Color(this, lineToken);
        Color textColor = UiThemeLookup.Color(this, textToken);

        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", UiSize.Space.S1);
        AddChild(row);
        if (IconId is { } icon)
        {
            row.AddChild(UiFieldAndRows.Icon(icon, UiIconSize.Small, textColor));
        }

        row.AddChild(UiFieldAndRows.Label(Text, UiTokens.Typography.Caption, textToken, HorizontalAlignment.Center));
        AddThemeStyleboxOverride("panel", UiThemeLookup.CreateStyleBox(UiThemeLookup.Color(this, UiTokens.Color.PanelRaised),
            color,
            radius: UiSize.Radius.Pill,
            horizontalPadding: UiSize.Space.S2,
            verticalPadding: UiSize.Space.S1));
    }

}
