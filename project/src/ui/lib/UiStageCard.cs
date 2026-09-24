using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Numbered stage card for a signal-flow column.</summary>
public partial class UiStageCard : UiCard
{
    [Signal]
    public delegate void StageSelectedEventHandler();

    private Control[] _body = [];
    private string _numberText = "1";
    private string _title = "Senses";
    private string _note = string.Empty;
    private bool _selected;
    private bool _collapsed;

    [Export]
    public string NumberText
    {
        get => _numberText;
        set
        {
            _numberText = value;
            Rebuild();
        }
    }

    [Export]
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            Rebuild();
        }
    }

    [Export]
    public string Note
    {
        get => _note;
        set
        {
            _note = value;
            Rebuild();
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
            Rebuild();
        }
    }

    public override UiTokens Tokens
    {
        get => base.Tokens;
        set
        {
            base.Tokens = value;
            Rebuild();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        SizeVariant = CardSize.Snug;
        Glow = true;
        RefreshCardStyle();
        base._Ready();
        Rebuild();
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
        _body = body;
        Rebuild();
    }

    private void RefreshCardStyle()
    {
        Kind = Selected ? CardVariant.Selected : CardVariant.Frame;
    }

    private void Rebuild()
    {
        if (!IsInsideTree())
        {
            return;
        }

        foreach (var child in _body)
        {
            child.GetParent()?.RemoveChild(child);
        }

        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        RefreshCardStyle();
        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        stack.AddThemeConstantOverride("separation", (int)Tokens.Space1);
        AddChild(stack);

        var header = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        header.AddThemeConstantOverride("separation", (int)Tokens.Space2);
        var number = new UiNumber { Text = NumberText, Tokens = Tokens };
        header.AddChild(number);
        var titleLabel = UiFieldAndRows.Label(Title, Tokens, Tokens.StageText, Tokens.Ink, HorizontalAlignment.Left);
        titleLabel.CustomMinimumSize = Vector2.Zero;
        titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(titleLabel);
        if (!string.IsNullOrWhiteSpace(Note))
        {
            var noteLabel = UiFieldAndRows.Label(Note, Tokens, Tokens.CaptionText, Tokens.Muted, HorizontalAlignment.Right);
            noteLabel.CustomMinimumSize = new Vector2(Tokens.ColumnSmallWidth, 0);
            noteLabel.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            header.AddChild(noteLabel);
        }

        stack.AddChild(header);
        var bodyContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Visible = !Collapsed,
        };
        bodyContainer.AddThemeConstantOverride("separation", (int)Tokens.Space1);
        stack.AddChild(bodyContainer);
        foreach (var child in _body)
        {
            child.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            UiTokenApplier.Apply(child, Tokens);
            bodyContainer.AddChild(child);
        }
    }
}
