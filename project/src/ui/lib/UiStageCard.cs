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
    private Func<string>? _noteSource;
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

    /// <summary>
    /// Already translated text shown instead of <see cref="Note"/>; asked again when the language
    /// changes, with the note label's own auto-translation off meanwhile. Null shows Note.
    /// </summary>
    public Func<string>? NoteSource
    {
        get => _noteSource;
        set
        {
            _noteSource = value;
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
        ApplyThemeStyles();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            EmitSignal(SignalName.StageSelected);
            AcceptEvent();
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

        UiTranslation.ShareContext(this, _number);
        _number.Text = NumberText;
    }

    private void ApplyTitle()
    {
        if (!_ready)
        {
            return;
        }

        UiTranslation.ShareContext(this, _titleLabel);
        _titleLabel.Text = Title;
    }

    private void ApplyNote()
    {
        if (!_ready)
        {
            return;
        }

        var note = NoteSource?.Invoke() ?? Note;
        _noteLabel.AutoTranslateMode = NoteSource is null ? AutoTranslateModeEnum.Inherit : AutoTranslateModeEnum.Disabled;
        UiTranslation.ShareContext(this, _noteLabel);
        _noteLabel.Text = note;
        _noteLabel.Visible = !string.IsNullOrWhiteSpace(note);
    }

    private void ApplyCollapsed()
    {
        if (!_ready)
        {
            return;
        }

        // An empty body would still add the stack's separation under the header.
        _body.Visible = !Collapsed && _body.GetChildCount() > 0;
    }

    private readonly UiUnsavedState _unsaved = new([("%Title", Control.PropertyName.ThemeTypeVariation), ("%Title", Label.PropertyName.Uppercase), ("%Note", Control.PropertyName.ThemeTypeVariation), ("%Note", Label.PropertyName.Uppercase), ("%Note", CanvasItem.PropertyName.Visible), ("%Body", CanvasItem.PropertyName.Visible)]);

    public override void _Notification(int what)
    {
        // Before the base, so the card's own state is cleared after what these setters restyle.
        var saving = _unsaved.Handle(this, what, ApplyThemeStyles);
        base._Notification(what);
        if (saving)
        {
            return;
        }

        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, ApplyThemeStyles);
        }
        else if (what == NotificationTranslationChanged && NoteSource is not null)
        {
            ApplyNote();
        }
    }

    private void ApplyThemeStyles()
    {
        if (!_ready)
        {
            return;
        }

        UiThemeLookup.ApplyTextStyle(_titleLabel, UiTokens.Typography.Stage, UiTokens.Color.Ink);
        UiThemeLookup.ApplyTextStyle(_noteLabel, UiTokens.Typography.Caption, UiTokens.Color.Muted);
    }
}
