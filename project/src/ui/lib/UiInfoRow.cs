using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Ringed icon, title, and one help line.</summary>
public partial class UiInfoRow : HBoxContainer
{
    private string _iconText = "?";
    private UiIconId _iconId = UiIconId.Warn;

    [Export]
    public string IconText
    {
        get => _iconText;
        set
        {
            _iconText = value;
            if (UiIconGlyphs.TryParse(value, out var icon))
            {
                _iconId = icon;
            }

            Rebuild();
        }
    }

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            _iconId = value;
            _iconText = value.ToString();
            Rebuild();
        }
    }

    [Export]
    public string Title { get; set; } = "Rotate handle";

    [Export]
    public string Help { get; set; } = "Drag the stem to rotate selected parts.";

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        Rebuild();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, Rebuild);
        }
    }

    private void Rebuild()
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

        var space2 = UiSize.Space.S2;
        var halo = UiThemeLookup.Color(this, UiTokens.Color.Halo);
        AddThemeConstantOverride("separation", space2);
        var iconFrame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(
                UiSize.Control.ExtraSmall,
                UiSize.Control.ExtraSmall),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        iconFrame.AddThemeStyleboxOverride(
            "panel",
            UiThemeLookup.CreateStyleBox(Colors.Transparent,
                halo,
                radius: UiSize.Radius.Pill));
        iconFrame.AddChild(UiFieldAndRows.Icon(IconId, UiIconSize.Small, halo));
        AddChild(iconFrame);
        var labels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        labels.AddThemeConstantOverride("separation", 0);
        AddChild(labels);
        labels.AddChild(UiFieldAndRows.Label(Title,
            UiTokens.Typography.BodyStrong,
            UiTokens.Color.Ink));
        labels.AddChild(UiFieldAndRows.Label(Help,
            UiTokens.Typography.Note,
            UiTokens.Color.Muted));
    }

}
