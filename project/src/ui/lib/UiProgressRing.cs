using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>44px centred progress ring with a percentage or completion check.</summary>
public partial class UiProgressRing : Control
{
    private UiTokens _tokens = UiTokens.Neon;
    private float _percent = 72;

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

    private bool _done;

    [Export]
    public bool Done
    {
        get => _done;
        set
        {
            _done = value;
            QueueRedraw();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            ApplyGeometry();
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        ApplyGeometry();
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        var radius = UiComponentContracts.ProgressRingDiameter * 0.5f;
        DrawArc(center, radius, 0, Mathf.Tau, 48, _tokens.LineStrong, _tokens.StrokeHair, antialiased: true);
        DrawArc(center, radius, -Mathf.Pi / 2, -Mathf.Pi / 2 + (Mathf.Tau * Percent / 100f), 48, Done ? _tokens.Halo : _tokens.Accent, _tokens.StrokeSignal, antialiased: true);
        if (Done)
        {
            var check = UiIcons.Load(UiIconId.Check, UiIconSize.Standard);
            var iconSize = UiIcons.Pixels(UiIconSize.Standard);
            DrawTextureRect(check, new Rect2(center - new Vector2(iconSize * 0.5f, iconSize * 0.5f), new Vector2(iconSize, iconSize)), false, _tokens.Halo);

            return;
        }

        DrawString(ThemeDB.FallbackFont, center + new Vector2(-14, 5), UiComponentContracts.FormatPercent(Percent), HorizontalAlignment.Center, 28, (int)_tokens.ReadoutSmallText.FontSize, _tokens.Ink);
    }

    private void ApplyGeometry()
    {
        CustomMinimumSize = new Vector2(_tokens.TouchTarget, _tokens.TouchTarget);
    }
}
