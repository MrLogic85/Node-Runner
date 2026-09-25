using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Ringed 16px step number used to lead numbered stage and chain items.</summary>
[Tool]
[GlobalClass]
public partial class UiNumber : Control, ISerializationListener
{
    private UiTokens _tokens = UiTokens.Neon;
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
        if (what == NotificationResized)
        {
            LayoutLabel();
        }
    }

    public override void _Draw()
    {
        var stroke = _tokens.NumberStrokeWidth;
        var center = Size * 0.5f;
        var radius = (_tokens.NumberDiameter - stroke) * 0.5f;
        DrawArc(center, radius, 0, Mathf.Tau, 32, _tokens.Accent, stroke, antialiased: false);
    }

    private void ApplyGeometry()
    {
        CustomMinimumSize = new Vector2(
            _tokens.NumberDiameter,
            _tokens.NumberDiameter);
        LayoutLabel();
    }

    private void RefreshLabel()
    {
        if (_label is null)
        {
            return;
        }

        _label.Text = Text;
        _tokens.ApplyTextStyle(_label, _tokens.ReadoutSmallText);
        _label.AddThemeColorOverride("font_color", _tokens.Accent);
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
