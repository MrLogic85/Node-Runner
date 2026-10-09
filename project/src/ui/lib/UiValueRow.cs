using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Static label/value row with an optional icon before the value.</summary>
[Tool]
[GlobalClass]
public partial class UiValueRow : HBoxContainer
{
    private string _labelText = "";
    private string _valueText = "";
    private UiIconId _iconId = UiIconId.None;
    private Func<string>? _labelSource;
    private Func<string>? _valueSource;
    private Label? _label;
    private Label? _value;

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
    public string ValueText
    {
        get => _valueText;
        set
        {
            _valueText = value;
            Refresh();
        }
    }

    /// <summary>
    /// Code-set label text, asked again when the language changes. While set it is shown instead
    /// of <see cref="LabelText"/> and not translated again; null shows <see cref="LabelText"/>.
    /// </summary>
    public Func<string>? LabelSource
    {
        get => _labelSource;
        set
        {
            _labelSource = value;
            ShowTexts();
        }
    }

    /// <summary>Code-set value text, as <see cref="LabelSource"/> is for the label.</summary>
    public Func<string>? ValueSource
    {
        get => _valueSource;
        set
        {
            _valueSource = value;
            ShowTexts();
        }
    }

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            _iconId = value;
            Refresh();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Refresh();
    }

    private readonly UiUnsavedState _unsaved = new("theme_override_constants/separation");

    public override void _Notification(int what)
    {
        if (_unsaved.Handle(this, what, Refresh))
        {
            return;
        }

        // Godot sends a language change while it walks the tree, when this node may not add
        // children, so the labels keep their nodes and only take the new text.
        if (what == NotificationTranslationChanged)
        {
            ShowTexts();
            return;
        }

        if (what == NotificationThemeChanged && IsNodeReady())
        {
            Refresh();
        }
    }

    // Rebuild writes its own separation override, which re-sends NotificationThemeChanged; without
    // the guard a runtime text change rebuilds twice and doubles the row's children.
    private void Refresh() => UiThemeRefresh.Guarded(this, Rebuild);

    private void Rebuild()
    {
        if (!IsInsideTree())
        {
            return;
        }

        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var spacing = UiSize.Space.S2;
        AddThemeConstantOverride("separation", spacing);
        var label = UiFieldAndRows.Label(LabelText, UiTokens.Typography.Caption, UiTokens.Color.Muted);
        UiTranslation.ShareContext(this, label);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(label);
        _label = label;
        var readout = new HBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        readout.AddThemeConstantOverride("separation", UiSize.Space.S1);
        AddChild(readout);
        if (IconId != UiIconId.None)
        {
            var glyph = UiFieldAndRows.Icon(IconId, UiIconSize.Small, UiThemeLookup.Color(this, UiTokens.Color.Ink));
            glyph.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            readout.AddChild(glyph);
        }

        var value = UiFieldAndRows.Label(ValueText,
            UiTokens.Typography.ReadoutMedium,
            UiTokens.Color.Ink,
            HorizontalAlignment.Right);
        UiTranslation.ShareContext(this, value);
        value.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        value.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        readout.AddChild(value);
        _value = value;
        ShowTexts();
    }

    private void ShowTexts()
    {
        if (_label is null || _value is null || !IsInstanceValid(_label) || !IsInstanceValid(_value))
        {
            return;
        }

        UiTranslation.ShareContext(this, _label);
        UiTranslation.ShareContext(this, _value);
        _label.AutoTranslateMode = _labelSource is null ? AutoTranslateModeEnum.Inherit : AutoTranslateModeEnum.Disabled;
        _label.Text = _labelSource?.Invoke() ?? LabelText;
        _value.AutoTranslateMode = _valueSource is null ? AutoTranslateModeEnum.Inherit : AutoTranslateModeEnum.Disabled;
        _value.Text = _valueSource?.Invoke() ?? ValueText;
    }
}
