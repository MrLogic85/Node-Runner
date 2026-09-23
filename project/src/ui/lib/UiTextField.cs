using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Single-line text input with standard and compact sizes.</summary>
public partial class UiTextField : VBoxContainer
{
    public enum TextInputSize
    {
        Standard,
        Compact,
    }

    public enum TextInputState
    {
        Rest,
        Editing,
        Error,
    }

    private UiTokens _tokens = UiTokens.Neon;
    private Label? _label;
    private LineEdit? _editor;
    private TextureRect? _stateIcon;
    private Label? _errorLabel;
    private string _textValue = "Creation name";
    private string _labelText = string.Empty;
    private string _errorText = string.Empty;
    private string _placeholderText = string.Empty;
    private TextInputSize _size = TextInputSize.Standard;
    private TextInputState _state;
    private bool _placeCaretAtEndOnFocus;
    private bool _holdErrorUntilTextChanges;

    public Func<string, bool> ValidateValue { get; set; } = static _ => true;

    [Export]
    public string TextValue
    {
        get => _editor?.Text ?? _textValue;
        set
        {
            _textValue = value;
            if (_editor is not null && _editor.Text != value)
            {
                _editor.Text = value;
            }
        }
    }

    [Export]
    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value;
            Refresh();
        }
    }

    [Export]
    public string ErrorText
    {
        get => _errorText;
        set
        {
            _errorText = value;
            Refresh();
        }
    }

    [Export]
    public string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            _placeholderText = value;
            if (_editor is not null)
            {
                _editor.PlaceholderText = value;
            }
        }
    }

    [Export]
    public TextInputSize InputSize
    {
        get => _size;
        set
        {
            _size = value;
            Refresh();
        }
    }

    [Export]
    public TextInputState State
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
            EmitSignal(SignalName.StateChanged, (long)value);
        }
    }

    [Signal]
    public delegate void EditingStartedEventHandler();

    [Signal]
    public delegate void EditingFinishedEventHandler(string value);

    [Signal]
    public delegate void TextEditedEventHandler(string value);

    [Signal]
    public delegate void StateChangedEventHandler(long state);

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
        AddThemeConstantOverride("separation", (int)_tokens.Space1);

        _label = UiFieldAndRows.Label(string.Empty, _tokens, _tokens.OverlineText, _tokens.Muted);
        AddChild(_label);

        _editor = new LineEdit
        {
            Text = _textValue,
            PlaceholderText = _placeholderText,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CaretBlink = true,
            MouseFilter = MouseFilterEnum.Pass,
        };
        _editor.FocusEntered += OnFocusEntered;
        _editor.FocusExited += OnFocusExited;
        _editor.TextChanged += OnTextChanged;
        _editor.TextSubmitted += _ => FinishEditing();
        _editor.Resized += LayoutStateIcon;
        AddChild(_editor);

        _errorLabel = UiFieldAndRows.Label(string.Empty, _tokens, _tokens.NoteText, _tokens.Danger);
        AddChild(_errorLabel);
        Refresh();
    }

    public void BeginEditing()
    {
        if (_editor?.HasFocus() == true)
        {
            StartEditing();
            return;
        }

        _placeCaretAtEndOnFocus = true;
        _editor?.GrabFocus();
        PlaceCaretAtEnd();
    }

    public void FinishEditing()
    {
        _textValue = _editor?.Text ?? _textValue;
        if (!ValidateValue(_textValue))
        {
            _holdErrorUntilTextChanges = true;
            State = TextInputState.Error;
            ReopenEditorAfterInvalidSubmit();
            return;
        }

        _holdErrorUntilTextChanges = false;
        if (State == TextInputState.Editing)
        {
            State = TextInputState.Rest;
        }

        EmitSignal(SignalName.EditingFinished, _textValue);
        if (_editor?.HasFocus() == true)
        {
            _editor.ReleaseFocus();
        }
    }

    public void ShowError(string message)
    {
        _holdErrorUntilTextChanges = true;
        ErrorText = message;
        State = TextInputState.Error;
    }

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        AddThemeConstantOverride("separation", (int)_tokens.Space1);

        if (_label is not null)
        {
            _label.Text = LabelText;
            _label.Visible = !string.IsNullOrWhiteSpace(LabelText);
            _tokens.ApplyTextStyle(_label, _tokens.OverlineText);
            _label.AddThemeColorOverride("font_color", _tokens.Muted);
        }

        var visibleHeight = InputSize == TextInputSize.Compact ? _tokens.ControlSmall : _tokens.ControlHeight;
        if (_editor is not null)
        {
            if (!_editor.HasFocus() && _editor.Text != _textValue)
            {
                _editor.Text = _textValue;
            }

            _editor.PlaceholderText = PlaceholderText;
            _editor.CustomMinimumSize = new Vector2(0, visibleHeight);
            _tokens.ApplyTextStyle(_editor, InputSize == TextInputSize.Compact ? _tokens.BodyStrongText : _tokens.HeadingText);
            _editor.AddThemeColorOverride("font_color", State == TextInputState.Error ? _tokens.Danger : _tokens.Ink);
            _editor.AddThemeColorOverride("font_placeholder_color", _tokens.Muted);
            _editor.AddThemeColorOverride("caret_color", _tokens.Accent);
            var border = State switch
            {
                TextInputState.Error => _tokens.Danger,
                TextInputState.Editing => _tokens.Accent,
                _ => _tokens.LineStrong,
            };
            var style = _tokens.ControlStyle(
                _tokens.PanelRaised,
                border,
                State == TextInputState.Rest ? _tokens.StrokeHair : _tokens.StrokeSignal,
                horizontalPadding: _tokens.Space2,
                verticalPadding: 0);
            style.ContentMarginRight = _tokens.Space2 + _tokens.Icon + _tokens.Space2;
            if (State == TextInputState.Editing)
            {
                style.ShadowColor = _tokens.EffectsEnabled ? _tokens.AccentSoft : Colors.Transparent;
                style.ShadowSize = _tokens.EffectsEnabled ? 3 : 0;
            }

            _editor.AddThemeStyleboxOverride("normal", style);
            _editor.AddThemeStyleboxOverride("focus", style);
            _editor.AddThemeStyleboxOverride("read_only", style);
        }

        RefreshStateIcon();
        LayoutStateIcon();

        if (_errorLabel is not null)
        {
            _errorLabel.Text = ErrorText;
            _errorLabel.Visible = State == TextInputState.Error && !string.IsNullOrWhiteSpace(ErrorText);
            _tokens.ApplyTextStyle(_errorLabel, _tokens.NoteText);
            _errorLabel.AddThemeColorOverride("font_color", _tokens.Danger);
        }
    }

    private void RefreshStateIcon()
    {
        if (_editor is null || !IsInstanceValid(_editor))
        {
            return;
        }

        _stateIcon?.QueueFree();
        var actionIcon = State switch
        {
            TextInputState.Error => UiIconId.Warn,
            _ => UiIconId.Edit,
        };
        var actionColor = State == TextInputState.Error ? _tokens.Danger : _tokens.Accent;
        _stateIcon = UiIcons.Create(actionIcon, UiIconSize.Standard, actionColor);
        _stateIcon.MouseFilter = MouseFilterEnum.Ignore;
        _editor.AddChild(_stateIcon);
    }

    private void LayoutStateIcon()
    {
        if (_editor is null || _stateIcon is null)
        {
            return;
        }

        var iconSize = _stateIcon.CustomMinimumSize;
        _stateIcon.Position = new Vector2(
            _editor.Size.X - _tokens.Space2 - iconSize.X,
            (_editor.Size.Y - iconSize.Y) * 0.5f);
        _stateIcon.Size = iconSize;
    }

    private void OnFocusEntered()
    {
        if (_placeCaretAtEndOnFocus)
        {
            PlaceCaretAtEnd();
            _placeCaretAtEndOnFocus = false;
        }

        StartEditing();
    }

    private void OnFocusExited()
    {
        if (State == TextInputState.Editing)
        {
            FinishEditing();
        }
    }

    private void OnTextChanged(string value)
    {
        _textValue = value;
        if (_holdErrorUntilTextChanges)
        {
            _holdErrorUntilTextChanges = false;
            State = TextInputState.Editing;
        }

        EmitSignal(SignalName.TextEdited, value);
    }

    private void PlaceCaretAtEnd()
    {
        if (_editor is not null)
        {
            _editor.CaretColumn = _editor.Text.Length;
        }
    }

    private void StartEditing()
    {
        if (State == TextInputState.Editing)
        {
            return;
        }

        if (State == TextInputState.Error && _holdErrorUntilTextChanges)
        {
            return;
        }

        State = TextInputState.Editing;
        EmitSignal(SignalName.EditingStarted);
    }

    private void ReopenEditorAfterInvalidSubmit()
    {
        if (_editor is null || !IsInstanceValid(_editor))
        {
            return;
        }

        Callable.From(RestoreEditorAfterInvalidSubmit).CallDeferred();
    }

    private void RestoreEditorAfterInvalidSubmit()
    {
        if (_editor is null || !IsInstanceValid(_editor))
        {
            return;
        }

        if (_editor.HasFocus())
        {
            _editor.ReleaseFocus();
        }

        _editor.GrabFocus();
    }

}
