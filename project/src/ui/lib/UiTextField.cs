using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>The canonical text entry with rest, edit, and invalid states.</summary>
public partial class UiTextField : PanelContainer
{
    private UiTokens _tokens = UiTokens.Neon;
    private LineEdit? _editor;
    private Button? _actionButton;
    private Label? _messageLabel;
    private string _textValue = "Creation name";
    private UiComponentContracts.ValidationState _state;

    [Export]
    public string TextValue
    {
        get => _editor?.Text ?? _textValue;
        set
        {
            _textValue = value;
            if (_editor is not null)
            {
                _editor.Text = value;
            }
        }
    }

    [Export]
    public string ValidationMessage { get; set; } = "A creation needs a name";

    [Export]
    public UiComponentContracts.ValidationState State
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            Refresh();
            EmitSignal(SignalName.ValidationStateChanged, (long)value);
        }
    }

    [Export]
    public bool PanelSize { get; set; }

    [Signal]
    public delegate void EditStartedEventHandler();

    [Signal]
    public delegate void EditConfirmedEventHandler(string value);

    [Signal]
    public delegate void TextEditedEventHandler(string value);

    [Signal]
    public delegate void ValidationStateChangedEventHandler(long state);

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            Refresh();
        }
    }

    public override void _Ready()
    {
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        AddChild(stack);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        stack.AddChild(row);

        _editor = new LineEdit
        {
            Text = _textValue,
            PlaceholderText = "Creation name",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _editor.TextChanged += OnTextChanged;
        _editor.TextSubmitted += _ => ConfirmEditing();
        row.AddChild(_editor);

        _actionButton = new Button
        {
            Flat = true,
            CustomMinimumSize = new Vector2(_tokens.ControlSmall, _tokens.ControlSmall),
        };
        _actionButton.Pressed += OnActionPressed;
        row.AddChild(_actionButton);

        _messageLabel = UiFieldAndRows.Label(ValidationMessage, _tokens, _tokens.NoteText, _tokens.Danger);
        stack.AddChild(_messageLabel);
        Refresh();
    }

    /// <summary>Starts an edit while preserving the value currently shown by the field.</summary>
    public void BeginEditing()
    {
        if (State == UiComponentContracts.ValidationState.Editing)
        {
            return;
        }

        State = UiComponentContracts.ValidationState.Editing;
        _editor?.GrabFocus();
        EmitSignal(SignalName.EditStarted);
    }

    /// <summary>Ends the edit and lets the owner validate or persist the submitted value.</summary>
    public void ConfirmEditing()
    {
        if (State != UiComponentContracts.ValidationState.Editing)
        {
            return;
        }

        _textValue = _editor?.Text ?? _textValue;
        State = UiComponentContracts.ValidationState.Rest;
        EmitSignal(SignalName.EditConfirmed, _textValue);
    }

    /// <summary>Shows a validation error supplied by the field's owner.</summary>
    public void ShowValidationError(string message)
    {
        ValidationMessage = message;
        State = UiComponentContracts.ValidationState.Invalid;
    }

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        CustomMinimumSize = new Vector2(0, PanelSize ? _tokens.ControlSmall : _tokens.ControlHeight);
        var border = State switch
        {
            UiComponentContracts.ValidationState.Invalid => _tokens.Danger,
            UiComponentContracts.ValidationState.Editing => _tokens.Accent,
            _ => _tokens.LineStrong,
        };
        AddThemeStyleboxOverride("panel", _tokens.ControlStyle(
            _tokens.PanelRaised,
            border,
            State == UiComponentContracts.ValidationState.Rest ? _tokens.StrokeHair : _tokens.StrokeSignal,
            glow: State == UiComponentContracts.ValidationState.Editing,
            horizontalPadding: _tokens.Space2,
            verticalPadding: _tokens.Space1));

        if (_editor is not null)
        {
            _editor.Text = _textValue;
            _editor.Editable = State == UiComponentContracts.ValidationState.Editing;
            _editor.PlaceholderText = State == UiComponentContracts.ValidationState.Invalid ? ValidationMessage : "Creation name";
            _tokens.ApplyTextStyle(_editor, PanelSize ? _tokens.BodyStrongText : _tokens.HeadingText);
            _editor.AddThemeColorOverride("font_color", State == UiComponentContracts.ValidationState.Invalid ? _tokens.Danger : _tokens.Ink);
        }

        if (_actionButton is not null)
        {
            _actionButton.Text = string.Empty;
            var actionIcon = State switch
            {
                UiComponentContracts.ValidationState.Invalid => UiIconId.Warn,
                UiComponentContracts.ValidationState.Editing => UiIconId.Check,
                _ => UiIconId.Edit,
            };
            var actionColor = State == UiComponentContracts.ValidationState.Invalid ? _tokens.Danger : _tokens.Accent;
            _tokens.ApplyTextStyle(_actionButton, _tokens.LabelText);
            _actionButton.AddThemeColorOverride("font_color", actionColor);
            UiIcons.Apply(_actionButton, actionIcon, UiIconSize.Standard, actionColor);
        }

        if (_messageLabel is not null)
        {
            _messageLabel.Visible = State == UiComponentContracts.ValidationState.Invalid;
            _messageLabel.Text = ValidationMessage;
            _tokens.ApplyTextStyle(_messageLabel, _tokens.NoteText);
            _messageLabel.AddThemeColorOverride("font_color", _tokens.Danger);
        }
    }

    private void OnActionPressed()
    {
        if (State == UiComponentContracts.ValidationState.Editing)
        {
            ConfirmEditing();
            return;
        }

        BeginEditing();
    }

    private void OnTextChanged(string value)
    {
        _textValue = value;
        EmitSignal(SignalName.TextEdited, value);
    }
}
