using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Single-line text input with standard and compact sizes.</summary>
[Tool]
[GlobalClass]
public partial class UiTextField : VBoxContainer, ISerializationListener
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

    private Label? _label;
    private LineEdit? _editor;
    private TextureRect? _stateIcon;
    private Label? _errorLabel;
    private string _textValue = "Creation name";
    private string _labelText = string.Empty;
    private string _errorText = string.Empty;
    private string _placeholderText = string.Empty;
    private Func<string>? _placeholderSource;
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
            ApplyPlaceholder();
        }
    }

    /// <summary>
    /// Already translated placeholder shown instead of <see cref="PlaceholderText"/>; asked again
    /// when the language changes, with the editor's own auto-translation off meanwhile, so it is not
    /// translated twice. Null shows PlaceholderText.
    /// </summary>
    public Func<string>? PlaceholderSource
    {
        get => _placeholderSource;
        set
        {
            _placeholderSource = value;
            ApplyPlaceholder();
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

    public override void _EnterTree() => RequestReady();

    public override void _Ready()
    {
        InitializeContent();
    }

    private readonly UiUnsavedState _unsaved = new("theme_override_constants/separation");

    public override void _Notification(int what)
    {
        if (_unsaved.Handle(this, what, Refresh))
        {
            return;
        }

        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, Refresh);
        }
        else if (what == NotificationTranslationChanged && _placeholderSource is not null)
        {
            ApplyPlaceholder();
        }
    }

    private void ApplyPlaceholder()
    {
        if (_editor is null)
        {
            return;
        }

        _editor.AutoTranslateMode = _placeholderSource is null ? AutoTranslateModeEnum.Inherit : AutoTranslateModeEnum.Disabled;
        _editor.PlaceholderText = _placeholderSource?.Invoke() ?? _placeholderText;
    }

    private void InitializeContent()
    {
        AddThemeConstantOverride("separation", (int)UiSize.Space.S1);
        RecoverContent();
        EnsureContent();
        Refresh();
    }

    private void RecoverContent()
    {
        _label = GetChildren(includeInternal: true)
            .OfType<Label>()
            .FirstOrDefault(label => label.Name == "Label");
        _editor = GetChildren(includeInternal: true)
            .OfType<LineEdit>()
            .FirstOrDefault();
        _errorLabel = GetChildren(includeInternal: true)
            .OfType<Label>()
            .FirstOrDefault(label => label.Name == "ErrorLabel");
        _stateIcon = _editor?.GetChildren(includeInternal: true)
            .OfType<TextureRect>()
            .FirstOrDefault();
    }

    private void EnsureContent()
    {
        if (_label is null)
        {
            _label = UiFieldAndRows.Label(string.Empty, UiTokens.Typography.Overline, UiTokens.Color.Muted);
            _label.Name = "Label";
            AddChild(_label, false, InternalMode.Front);
        }

        if (_editor is null)
        {
            _editor = new LineEdit
            {
                Name = "Editor",
                Text = _textValue,
                PlaceholderText = _placeholderText,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                CaretBlink = true,
                MouseFilter = MouseFilterEnum.Pass,
            };
            AddChild(_editor, false, InternalMode.Front);
        }

        ConnectEditorEvents();

        if (_errorLabel is null)
        {
            _errorLabel = UiFieldAndRows.Label(string.Empty, UiTokens.Typography.Note, UiTokens.Color.Danger);
            _errorLabel.Name = "ErrorLabel";
            AddChild(_errorLabel, false, InternalMode.Front);
        }
    }

    private void ConnectEditorEvents()
    {
        ConnectIfMissing(_editor!, Control.SignalName.FocusEntered, new Callable(this, MethodName.OnFocusEntered));
        ConnectIfMissing(_editor!, Control.SignalName.FocusExited, new Callable(this, MethodName.OnFocusExited));
        ConnectIfMissing(_editor!, Control.SignalName.Resized, new Callable(this, MethodName.LayoutStateIcon));
        ConnectIfMissing(_editor!, LineEdit.SignalName.TextChanged, new Callable(this, MethodName.OnTextChanged));
        ConnectIfMissing(_editor!, LineEdit.SignalName.TextSubmitted, new Callable(this, MethodName.OnTextSubmitted));
    }

    private void DisconnectEditorEvents()
    {
        if (_editor is null)
        {
            return;
        }

        DisconnectIfConnected(_editor, Control.SignalName.FocusEntered, new Callable(this, MethodName.OnFocusEntered));
        DisconnectIfConnected(_editor, Control.SignalName.FocusExited, new Callable(this, MethodName.OnFocusExited));
        DisconnectIfConnected(_editor, Control.SignalName.Resized, new Callable(this, MethodName.LayoutStateIcon));
        DisconnectIfConnected(_editor, LineEdit.SignalName.TextChanged, new Callable(this, MethodName.OnTextChanged));
        DisconnectIfConnected(_editor, LineEdit.SignalName.TextSubmitted, new Callable(this, MethodName.OnTextSubmitted));
    }

    private static void ConnectIfMissing(GodotObject target, StringName signal, Callable callback)
    {
        if (!target.IsConnected(signal, callback))
        {
            target.Connect(signal, callback);
        }
    }

    private static void DisconnectIfConnected(GodotObject target, StringName signal, Callable callback)
    {
        if (target.IsConnected(signal, callback))
        {
            target.Disconnect(signal, callback);
        }
    }

    public override void _ExitTree() => DisconnectEditorEvents();

    public void OnBeforeSerialize() => DisconnectEditorEvents();

    public void OnAfterDeserialize() => CallDeferred(MethodName.RestoreContent);

    private void RestoreContent()
    {
        if (IsInsideTree())
        {
            InitializeContent();
        }
    }

    private void OnTextSubmitted(string _) => FinishEditing();

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

        AddThemeConstantOverride("separation", (int)UiSize.Space.S1);

        if (_label is not null)
        {
            _label.Text = LabelText;
            _label.Visible = !string.IsNullOrWhiteSpace(LabelText);
            UiThemeLookup.ApplyTextStyle(_label, UiTokens.Typography.Overline, UiTokens.Color.Muted);
        }

        var visibleHeight = InputSize == TextInputSize.Compact ? UiSize.Control.Small : UiSize.Control.Default;
        var border = State switch
        {
            TextInputState.Error => UiThemeLookup.Color(this, UiTokens.Color.Danger),
            TextInputState.Editing => UiThemeLookup.Color(this, UiTokens.Color.Accent),
            _ => UiThemeLookup.Color(this, UiTokens.Color.LineStrong),
        };
        if (_editor is not null)
        {
            if (!_editor.HasFocus() && _editor.Text != _textValue)
            {
                _editor.Text = _textValue;
            }

            ApplyPlaceholder();
            _editor.CustomMinimumSize = new Vector2(0, visibleHeight);
            UiThemeLookup.ApplyTextStyle(
                _editor,
                InputSize == TextInputSize.Compact ? UiTokens.Typography.BodyStrong : UiTokens.Typography.Heading,
                State == TextInputState.Error ? UiTokens.Color.Danger : UiTokens.Color.Ink);
            var style = UiThemeLookup.CreateStyleBox(UiThemeLookup.Color(this, UiTokens.Color.PanelRaised),
                border,
                State == TextInputState.Rest ? UiSize.Stroke.Hair : UiSize.Stroke.Signal,
                horizontalPadding: UiSize.Space.S2,
                verticalPadding: 0);
            style.ContentMarginRight = UiSize.Space.S2 + UiSize.Icon.Default + UiSize.Space.S2;
            if (State == TextInputState.Editing)
            {
                UiGlow.ApplyToControl(style, UiThemeLookup.Color(this, UiTokens.Color.Accent), UiThemeLookup.EffectsEnabled(this));
            }

            _editor.AddThemeStyleboxOverride("normal", style);
            _editor.AddThemeStyleboxOverride("focus", style);
            _editor.AddThemeStyleboxOverride("read_only", style);
        }

        RefreshStateIcon(border);
        LayoutStateIcon();

        if (_errorLabel is not null)
        {
            _errorLabel.Text = ErrorText;
            _errorLabel.Visible = State == TextInputState.Error && !string.IsNullOrWhiteSpace(ErrorText);
            UiThemeLookup.ApplyTextStyle(_errorLabel, UiTokens.Typography.Note, UiTokens.Color.Danger);
        }
    }

    private void RefreshStateIcon(Color iconColor)
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
        _stateIcon = UiIcons.Create(actionIcon, UiIconSize.Standard, iconColor);
        _stateIcon.MouseFilter = MouseFilterEnum.Ignore;
        _editor.AddChild(_stateIcon, false, InternalMode.Front);
    }

    private void LayoutStateIcon()
    {
        if (_editor is null || _stateIcon is null)
        {
            return;
        }

        var iconSize = _stateIcon.CustomMinimumSize;
        _stateIcon.Position = new Vector2(
            _editor.Size.X - UiSize.Space.S2 - iconSize.X,
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
