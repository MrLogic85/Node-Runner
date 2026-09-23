using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>44px centred progress ring with a percentage or completion check.</summary>
public partial class UiProgressRing : Control
{
    private const float _ringRadius = 17;
    private const float _ringStrokeWidth = 3;

    private UiTokens _tokens = UiTokens.Neon;
    private float _progress = 0.72f;
    private Label? _percentLabel;

    [Export(PropertyHint.Range, "0,1,0.001")]
    public float Progress
    {
        get => _progress;
        set
        {
            _progress = (float)UiComponentContracts.ClampProgress(value);
            RefreshLabel();
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
            RefreshLabel();
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        _percentLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_percentLabel);
        Resized += LayoutLabel;
        ApplyGeometry();
        MouseFilter = MouseFilterEnum.Ignore;
        RefreshLabel();
    }

    public override void _ExitTree()
    {
        Resized -= LayoutLabel;
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        DrawArc(center, _ringRadius, 0, Mathf.Tau, 48, _tokens.Line, _ringStrokeWidth, antialiased: true);
        DrawProgressArc(center);
        if (IsDone)
        {
            var check = UiIcons.Load(UiIconId.Check, UiIconSize.Standard);
            var iconSize = UiIcons.Pixels(UiIconSize.Standard);
            DrawTextureRect(check, new Rect2(center - new Vector2(iconSize * 0.5f, iconSize * 0.5f), new Vector2(iconSize, iconSize)), false, _tokens.Accent);

            return;
        }
    }

    private void ApplyGeometry()
    {
        CustomMinimumSize = new Vector2(_tokens.TouchTarget, _tokens.TouchTarget);
        LayoutLabel();
    }

    private void RefreshLabel()
    {
        if (_percentLabel is null)
        {
            return;
        }

        _percentLabel.Text = UiComponentContracts.FormatProgressPercent(Progress);
        _percentLabel.Visible = !IsDone;
        _tokens.ApplyTextStyle(_percentLabel, _tokens.ReadoutMediumText);
        _percentLabel.AddThemeColorOverride("font_color", _tokens.Ink);
        LayoutLabel();
    }

    private void LayoutLabel()
    {
        if (_percentLabel is null)
        {
            return;
        }

        _percentLabel.Position = Vector2.Zero;
        _percentLabel.Size = Size;
    }

    private void DrawProgressArc(Vector2 center)
    {
        var percent = UiComponentContracts.ProgressPercent(Progress);
        if (percent <= 0)
        {
            return;
        }

        var progress = percent / 100f;
        var startAngle = -Mathf.Pi / 2;
        var endAngle = startAngle + (Mathf.Tau * progress);
        DrawArc(center, _ringRadius, startAngle, endAngle, 48, _tokens.Accent, _ringStrokeWidth, antialiased: true);
        if (percent >= 100)
        {
            return;
        }

        DrawCircle(PointOnRing(center, startAngle), _ringStrokeWidth * 0.5f, _tokens.Accent);
        DrawCircle(PointOnRing(center, endAngle), _ringStrokeWidth * 0.5f, _tokens.Accent);
    }

    private static Vector2 PointOnRing(Vector2 center, float angle) =>
        center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _ringRadius;

    private bool IsDone => UiComponentContracts.IsProgressComplete(Progress);
}
