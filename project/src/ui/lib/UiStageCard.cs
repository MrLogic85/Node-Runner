using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Numbered stage card for a signal-flow column, built from a shared authored scene.</summary>
[Tool]
[GlobalClass]
public partial class UiStageCard : UiCard
{
    [Signal]
    public delegate void StageSelectedEventHandler();

    private string _numberText = "1";
    private string _title = "Senses";
    private string _note = string.Empty;
    private bool _selected;
    private bool _collapsed;
    private UiNumber _number = null!;
    private Label _titleLabel = null!;
    private Label _noteLabel = null!;
    private VBoxContainer _body = null!;
    private bool _ready;

    [Export]
    public string NumberText
    {
        get => _numberText;
        set
        {
            _numberText = value;
            ApplyNumber();
        }
    }

    [Export]
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            ApplyTitle();
        }
    }

    [Export]
    public string Note
    {
        get => _note;
        set
        {
            _note = value;
            ApplyNote();
        }
    }

    [Export]
    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            RefreshCardStyle();
        }
    }

    [Export]
    public bool Collapsed
    {
        get => _collapsed;
        set
        {
            _collapsed = value;
            ApplyCollapsed();
        }
    }

    public override UiTokens Tokens
    {
        get => base.Tokens;
        set
        {
            base.Tokens = value;
            ApplyTokens();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        SizeVariant = CardSize.Snug;
        Glow = true;
        _number = GetNode<UiNumber>("%Number");
        _titleLabel = GetNode<Label>("%Title");
        _noteLabel = GetNode<Label>("%Note");
        _body = GetNode<VBoxContainer>("%Body");
        _ready = true;
        base._Ready();
        RefreshCardStyle();
        ApplyNumber();
        ApplyTitle();
        ApplyNote();
        ApplyCollapsed();
        ApplyTokens();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            EmitSignal(SignalName.StageSelected);
            AcceptEvent();
        }
    }

    public void SetBody(params Control[] body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!_ready)
        {
            return;
        }

        foreach (var child in _body.GetChildren().OfType<Control>().ToArray())
        {
            _body.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var child in body)
        {
            child.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            UiTokenApplier.Apply(child, Tokens);
            _body.AddChild(child);
        }
    }

    private void RefreshCardStyle()
    {
        Kind = Selected ? CardVariant.Selected : CardVariant.Frame;
    }

    private void ApplyNumber()
    {
        if (!_ready)
        {
            return;
        }

        _number.Text = NumberText;
    }

    private void ApplyTitle()
    {
        if (!_ready)
        {
            return;
        }

        _titleLabel.Text = Title;
    }

    private void ApplyNote()
    {
        if (!_ready)
        {
            return;
        }

        _noteLabel.Text = Note;
        _noteLabel.Visible = !string.IsNullOrWhiteSpace(Note);
    }

    private void ApplyCollapsed()
    {
        if (!_ready)
        {
            return;
        }

        _body.Visible = !Collapsed;
    }

    private void ApplyTokens()
    {
        if (!_ready)
        {
            return;
        }

        _number.Tokens = Tokens;
        Tokens.ApplyTextStyle(_titleLabel, Tokens.StageText);
        _titleLabel.AddThemeColorOverride("font_color", Tokens.Ink);
        Tokens.ApplyTextStyle(_noteLabel, Tokens.CaptionText);
        _noteLabel.AddThemeColorOverride("font_color", Tokens.Muted);
        foreach (var child in _body.GetChildren().OfType<Control>())
        {
            UiTokenApplier.Apply(child, Tokens);
        }
    }
}
