using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A screen's frame: the background fills the whole window, and the card with the screen's content
/// sits inside the display safe area, so no control lands under a camera cutout (#513).
/// The frame is never larger than the window, and the scene propagates that maximum down to the
/// card, so content that needs more room is clipped by the card instead of pushing the frame off
/// screen (#737).
/// </summary>
[Tool]
[GlobalClass]
public partial class UiFrame : PanelContainer
{
    private bool _ready;

    private ColorRect? _background;
    private MarginContainer? _margin;
    private UiSafeArea? _safeArea;

    // The margins the scene authors; the safe-area inset is added on top of them at runtime.
    private int _baseLeft;
    private int _baseTop;
    private int _baseRight;
    private int _baseBottom;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        _background = GetNode<ColorRect>("%Background");
        _margin = GetNode<MarginContainer>("MarginContainer");
        _baseLeft = _margin.GetThemeConstant("margin_left");
        _baseTop = _margin.GetThemeConstant("margin_top");
        _baseRight = _margin.GetThemeConstant("margin_right");
        _baseBottom = _margin.GetThemeConstant("margin_bottom");
        base._Ready();
        _ready = true;
        ApplyTokens();
        ApplySafeArea();
    }

    public override void _EnterTree()
    {
        _safeArea = UiSafeArea.Of(this);
        _safeArea?.Changed += ApplySafeArea;
        ApplySafeArea();
        if (!Engine.IsEditorHint())
        {
            GetViewport().SizeChanged += FitWindow;
            FitWindow();
        }
    }

    public override void _ExitTree()
    {
        _safeArea?.Changed -= ApplySafeArea;
        _safeArea = null;
        if (!Engine.IsEditorHint())
            GetViewport().SizeChanged -= FitWindow;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
            ApplyTokens();
    }

    private void ApplyTokens()
    {
        if (!_ready)
            return;

        _background?.Color = UiThemeLookup.Color(this, UiTokens.Color.Background);
    }

    private void FitWindow() => CustomMaximumSize = GetViewport().GetVisibleRect().Size;

    // Only runs outside the editor (there is no safe area there), so no runtime margin is saved into a scene.
    private void ApplySafeArea()
    {
        if (!_ready || _margin is null || _safeArea is null)
            return;

        var insets = _safeArea.Current;
        _margin.AddThemeConstantOverride("margin_left", _baseLeft + Mathf.CeilToInt(insets.Left));
        _margin.AddThemeConstantOverride("margin_top", _baseTop + Mathf.CeilToInt(insets.Top));
        _margin.AddThemeConstantOverride("margin_right", _baseRight + Mathf.CeilToInt(insets.Right));
        _margin.AddThemeConstantOverride("margin_bottom", _baseBottom + Mathf.CeilToInt(insets.Bottom));
    }
}
