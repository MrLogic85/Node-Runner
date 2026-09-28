using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Callout (<c>c_call</c>): a small note that points at a spot in a figure. Its position is data
/// set by the caller; kind only colours the border.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiCallout : PanelContainer
{
    /// <summary>Shares chip's names: warn (the default), danger explains a refusal, ok marks a target.</summary>
    public enum CalloutKind
    {
        Warning,
        Danger,
        Ok,
    }

    private CalloutKind _kind;
    private string _text = string.Empty;
    private UiIconId _iconId = UiIconId.None;
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
    public CalloutKind Kind
    {
        get => _kind;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid callout kind: {value}. Keeping {_kind}.");
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
                GD.PushError($"Invalid callout icon: {value}. Keeping {_iconId}.");
                return;
            }

            _iconId = value;
            Refresh();
        }
    }

    public static UiTokens.Color BorderFor(CalloutKind kind) => kind switch
    {
        CalloutKind.Danger => UiTokens.Color.Danger,
        CalloutKind.Ok => UiTokens.Color.Accent,
        _ => UiTokens.Color.Halo,
    };

    public override void _EnterTree() => RequestReady();

    public UiCallout() => MouseFilter = MouseFilterEnum.Ignore;

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

        _content.Apply(Text, IconId, UiTokens.Color.Ink, UiThemeLookup.Color(this, UiTokens.Color.Ink));
        AddThemeStyleboxOverride("panel", UiThemeLookup.CreateStyleBox(
            UiThemeLookup.Color(this, UiTokens.Color.Panel),
            UiThemeLookup.Color(this, BorderFor(Kind)),
            radius: UiSize.Radius.Medium,
            horizontalPadding: UiSize.Space.S2 + UiSize.Stroke.Hair,
            verticalPadding: UiSize.Space.S1 + UiSize.Stroke.Hair));
    }
}
