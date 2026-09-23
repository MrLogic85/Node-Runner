using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Editable label/value row.</summary>
public partial class UiValueRow : HBoxContainer
{
    private UiTokens _tokens = UiTokens.Neon;

    [Export]
    public string LabelText { get; set; } = "Strength";

    [Export]
    public string ValueText { get; set; } = "0.6";

    [Signal]
    public delegate void EditRequestedEventHandler();

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            Rebuild();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        Rebuild();
    }

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

        CustomMinimumSize = new Vector2(0, _tokens.ControlSmall);
        AddThemeConstantOverride("separation", (int)_tokens.Space2);
        var label = UiFieldAndRows.Label(LabelText, _tokens, _tokens.CaptionText, _tokens.Muted);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(label);
        var editButton = new Button
        {
            Text = ValueText,
            Flat = true,
            CustomMinimumSize = new Vector2(_tokens.ColumnSmallWidth, _tokens.ControlSmall),
            MouseFilter = MouseFilterEnum.Pass,
        };
        _tokens.ApplyTextStyle(editButton, _tokens.ReadoutMediumText);
        editButton.AddThemeColorOverride("font_color", _tokens.Ink);
        editButton.Pressed += () => EmitSignal(SignalName.EditRequested);
        AddChild(editButton);
        AddChild(UiFieldAndRows.Icon(UiIconId.Edit, UiIconSize.Small, _tokens.Accent));
    }
}
