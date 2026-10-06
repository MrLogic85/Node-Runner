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
    private Func<string>? _valueSource;
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
    /// Already translated text shown instead of <see cref="ValueText"/>; asked again when the
    /// language changes, with the value label's own auto-translation off meanwhile. Null shows
    /// ValueText. A new source only rewrites the value, so a live value does not rebuild the row.
    /// </summary>
    public Func<string>? ValueSource
    {
        get => _valueSource;
        set
        {
            _valueSource = value;
            ApplyValue();
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

        if (what == NotificationThemeChanged && IsNodeReady())
        {
            Refresh();
        }
        else if (what == NotificationTranslationChanged && ValueSource is not null)
        {
            ApplyValue();
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
        ApplyValue();
    }

    private void ApplyValue()
    {
        if (_value is null || !IsInstanceValid(_value))
        {
            Refresh();
            return;
        }

        _value.AutoTranslateMode = ValueSource is null ? AutoTranslateModeEnum.Inherit : AutoTranslateModeEnum.Disabled;
        UiTranslation.ShareContext(this, _value);
        _value.Text = ValueSource?.Invoke() ?? ValueText;
    }
}
