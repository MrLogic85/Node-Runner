using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>44px centred progress ring with a percentage or completion check.</summary>
[Tool]
[GlobalClass]
public partial class UiProgressRing : Control, ISerializationListener
{
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
        MouseFilter = MouseFilterEnum.Ignore;
        InitializeContent();
    }

    private void InitializeContent()
    {
        _percentLabel = GetChildren(includeInternal: true)
            .OfType<Label>()
            .FirstOrDefault(label => label.Name == "ProgressLabel");
        if (_percentLabel is null)
        {
            _percentLabel = new Label
            {
                Name = "ProgressLabel",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            AddChild(_percentLabel, false, InternalMode.Front);
        }

        ApplyGeometry();
        RefreshLabel();
    }

    public override void _EnterTree() => RequestReady();

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize() => CallDeferred(MethodName.RestoreContent);

    private void RestoreContent()
    {
        if (IsInsideTree())
        {
            InitializeContent();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            LayoutLabel();
        }
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        DrawArc(
            center,
            RingRadius,
            0,
            Mathf.Tau,
            48,
            _tokens.Line,
            _tokens.StrokeBeam,
            antialiased: false);
        DrawProgressArc(center);
        if (IsDone)
        {
            var check = UiIcons.Load(UiIconId.Check, UiIconSize.Standard);
            var iconSize = UiIcons.Pixels(UiIconSize.Standard);
            DrawTextureRect(check, new Rect2(center - new Vector2(iconSize * 0.5f, iconSize * 0.5f), new Vector2(iconSize, iconSize)), false, _tokens.Accent);
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
        DrawArc(
            center,
            RingRadius,
            startAngle,
            endAngle,
            48,
            _tokens.Accent,
            _tokens.StrokeBeam,
            antialiased: false);
        if (percent >= 100)
        {
            return;
        }

        var capRadius = _tokens.StrokeBeam * 0.5f;
        DrawCircle(PointOnRing(center, startAngle), capRadius, _tokens.Accent);
        DrawCircle(PointOnRing(center, endAngle), capRadius, _tokens.Accent);
    }

    private Vector2 PointOnRing(Vector2 center, float angle) =>
        center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * RingRadius;

    private float RingRadius => _tokens.ControlSmall * 0.5f;

    private bool IsDone => UiComponentContracts.IsProgressComplete(Progress);
}
