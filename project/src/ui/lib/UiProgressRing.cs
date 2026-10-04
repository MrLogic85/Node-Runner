using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>44px centred progress ring with a percentage or completion check.</summary>
[Tool]
[GlobalClass]
public partial class UiProgressRing : Control, ISerializationListener
{
    private const float _previewProgress = 0.72f;
    private const int _ringPoints = 48;
    private float _progress = _previewProgress;
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
        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, () =>
            {
                ApplyGeometry();
                RefreshLabel();
                QueueRedraw();
            });
        }
        else if (what == NotificationResized)
        {
            LayoutLabel();
        }
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        using (var pen = UiPixelPen.Begin(this))
        {
            pen.Ring(center, _ringRadius, UiThemeLookup.Color(this, UiTokens.Color.Line), UiSize.Stroke.Beam, _ringPoints);
            DrawProgressArc(pen, center);
        }

        if (IsDone)
        {
            var check = UiIcons.Load(UiIconId.Check, UiIconSize.Standard);
            var iconSize = UiIcons.Pixels(UiIconSize.Standard);
            DrawTextureRect(
                check,
                new Rect2(
                    center - new Vector2(iconSize * 0.5f, iconSize * 0.5f),
                    new Vector2(iconSize, iconSize)),
                false,
                UiThemeLookup.Color(this, UiTokens.Color.Accent));
        }
    }

    public override Vector2 _GetMinimumSize() => Vector2.One * UiSize.Control.Touch;

    private void ApplyGeometry()
    {
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
        UiThemeLookup.ApplyTextStyle(_percentLabel, UiTokens.Typography.ReadoutMedium, UiTokens.Color.Ink);
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

    private void DrawProgressArc(UiPixelPen pen, Vector2 center)
    {
        var percent = UiComponentContracts.ProgressPercent(Progress);
        if (percent <= 0)
        {
            return;
        }

        var progress = percent / (float)UiComponentContracts.FullPercent;
        var startAngle = -Mathf.Pi / 2;
        var endAngle = startAngle + (Mathf.Tau * progress);
        pen.Arc(center, _ringRadius, startAngle, endAngle, _ringPoints, UiThemeLookup.Color(this, UiTokens.Color.Accent), UiSize.Stroke.Beam);
        if (percent >= UiComponentContracts.FullPercent)
        {
            return;
        }

        const float capRadius = UiSize.Stroke.Beam * 0.5f;
        var accent = UiThemeLookup.Color(this, UiTokens.Color.Accent);
        pen.Disc(PointOnRing(center, startAngle), capRadius, accent);
        pen.Disc(PointOnRing(center, endAngle), capRadius, accent);
    }

    private Vector2 PointOnRing(Vector2 center, float angle) =>
        center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _ringRadius;

    private const float _ringRadius = UiSize.Control.Small * 0.5f;

    private bool IsDone => UiComponentContracts.IsProgressComplete(Progress);
}
