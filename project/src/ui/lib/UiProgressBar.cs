using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Token-backed six-pixel progress bar.</summary>
public partial class UiProgressBar : Control
{
    private float _percent = 50;

    [Export(PropertyHint.Range, "0,100,1")]
    public float Percent
    {
        get => _percent;
        set
        {
            _percent = (float)UiComponentContracts.ClampPercent(value);
            QueueRedraw();
        }
    }

    [Export]
    public bool Bad { get; set; }

    [Export]
    public bool ShowPercent { get; set; }

    private float _barHeight = 6;

    [Export(PropertyHint.Range, "1,24,1")]
    public float BarHeight
    {
        get => _barHeight;
        set
        {
            _barHeight = Mathf.Clamp(value, 1, UiSize.Control.ExtraSmall);
            ApplyGeometry();
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ApplyGeometry();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged && IsNodeReady())
        {
            ApplyGeometry();
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var height = Mathf.Min(Size.Y, BarHeight);
        var y = (Size.Y - height) * 0.5f;
        DrawRect(
            new Rect2(0, y, Size.X, height),
            UiThemeLookup.Color(this, UiTokens.Color.Line),
            filled: true);
        DrawRect(
            new Rect2(0, y, Size.X * Percent / 100f, height),
            UiThemeLookup.Color(this, Bad ? UiTokens.Color.Danger : UiTokens.Color.Accent),
            filled: true);
        if (!ShowPercent)
        {
            return;
        }

        var text = UiComponentContracts.FormatPercent(Percent);
        DrawString(
            UiThemeLookup.Font(this, UiTokens.Typography.ReadoutSmall),
            new Vector2(Size.X - 34, Mathf.Max(12, y - 2)),
            text,
            HorizontalAlignment.Left,
            34,
            UiThemeLookup.FontSize(this, UiTokens.Typography.ReadoutSmall),
            UiThemeLookup.Color(this, UiTokens.Color.Ink));
    }

    private void ApplyGeometry()
    {
        CustomMinimumSize = new Vector2(0, BarHeight);
    }
}
