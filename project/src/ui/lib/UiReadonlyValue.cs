using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Dashed locked value row with an icon and reason.</summary>
public partial class UiReadonlyValue : PanelContainer
{
    private UiTokens _tokens = UiTokens.Neon;

    [Export]
    public string LabelText { get; set; } = "Length";

    [Export]
    public string ValueText { get; set; } = "56";

    private string _iconText = "lock";
    private UiIconId _iconId = UiIconId.Lock;

    [Export]
    public string IconText
    {
        get => _iconText;
        set
        {
            _iconText = value;
            if (UiIconGlyphs.TryParse(value, out var icon))
            {
                _iconId = icon;
            }

            Rebuild();
        }
    }

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            _iconId = value;
            _iconText = value.ToString();
            Rebuild();
        }
    }

    [Export]
    public string Reason { get; set; } = "locked by training";

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            Rebuild();
        }
    }

    public override void _Ready() => Rebuild();

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

        AddThemeStyleboxOverride("panel", UiFieldAndRows.DashedLike(_tokens, _tokens.LineStrong));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        AddChild(row);
        row.AddChild(UiFieldAndRows.Icon(IconId, UiIconSize.Standard, _tokens.Muted));
        var label = UiFieldAndRows.Label($"{LabelText} · {Reason}", _tokens, _tokens.CaptionText, _tokens.Muted);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label);
        row.AddChild(UiFieldAndRows.Label(ValueText, _tokens, _tokens.ReadoutMediumText, _tokens.Ink, HorizontalAlignment.Right));
        MouseFilter = MouseFilterEnum.Ignore;
        QueueRedraw();
    }

    public override void _Draw()
    {
        const float dashLength = 4;
        const float gapLength = 4;
        var inset = _tokens.StrokeHair * 0.5f;
        DrawDashedLine(new Vector2(inset, inset), new Vector2(Size.X - inset, inset), dashLength, gapLength);
        DrawDashedLine(new Vector2(inset, Size.Y - inset), new Vector2(Size.X - inset, Size.Y - inset), dashLength, gapLength);
        DrawDashedLine(new Vector2(inset, inset), new Vector2(inset, Size.Y - inset), dashLength, gapLength);
        DrawDashedLine(new Vector2(Size.X - inset, inset), new Vector2(Size.X - inset, Size.Y - inset), dashLength, gapLength);
    }

    private void DrawDashedLine(Vector2 start, Vector2 end, float dashLength, float gapLength)
    {
        var length = start.DistanceTo(end);
        var direction = (end - start).Normalized();
        for (var offset = 0f; offset < length; offset += dashLength + gapLength)
        {
            DrawLine(start + (direction * offset), start + (direction * Mathf.Min(offset + dashLength, length)), _tokens.LineStrong, _tokens.StrokeHair, antialiased: false);
        }
    }

}
