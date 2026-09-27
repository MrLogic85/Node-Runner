using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Ringed 16px step number used to lead numbered stage and chain items.</summary>
[Tool]
[GlobalClass]
public partial class UiNumber : Control, ISerializationListener
{
    private const int _ringPoints = 32;
    private Label? _label;
    private string _text = "1";

    [Export]
    public string Text
    {
        get => _text;
        set
        {
            _text = value;
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
        _label = GetChildren(includeInternal: true)
            .OfType<Label>()
            .FirstOrDefault();
        if (_label is null)
        {
            _label = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            AddChild(_label, false, InternalMode.Front);
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
        const float stroke = UiSize.Stroke.Number;
        var center = Size * 0.5f;
        const float radius = (UiSize.Widget.NumberDiameter - stroke) * 0.5f;
        DrawArc(
            center,
            radius,
            0,
            Mathf.Tau,
            _ringPoints,
            UiThemeLookup.Color(this, UiTokens.Color.Accent),
            stroke,
            antialiased: false);
    }

    private void ApplyGeometry()
    {
        CustomMinimumSize = new Vector2(UiSize.Widget.NumberDiameter, UiSize.Widget.NumberDiameter);
        LayoutLabel();
    }

    private void RefreshLabel()
    {
        if (_label is null)
        {
            return;
        }

        _label.Text = Text;
        UiThemeLookup.ApplyTextStyle(_label, UiTokens.Typography.ReadoutSmall, UiTokens.Color.Accent);
        LayoutLabel();
    }

    private void LayoutLabel()
    {
        if (_label is null)
        {
            return;
        }

        _label.Position = Vector2.Zero;
        _label.Size = Size;
    }
}
