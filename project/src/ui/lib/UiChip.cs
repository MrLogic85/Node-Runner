using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Small token-backed status, lock, unlock, or brain-shape label.</summary>
public partial class UiChip : Label
{
    public enum ChipKind
    {
        Neutral,
        Accent,
        Locked,
        Danger,
    }

    private UiTokens _tokens = UiTokens.Neon;
    private ChipKind _kind;

    [Export]
    public ChipKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            Refresh();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            Refresh();
        }
    }

    public override void _Ready() => Refresh();

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        var estimatedTextWidth = Text.Length * _tokens.CaptionFontSize * 0.62f;
        CustomMinimumSize = new Vector2(estimatedTextWidth + (_tokens.Space2 * 2), 28);
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Center;
        _tokens.ApplyTextStyle(this, _tokens.CaptionText);

        var color = Kind switch
        {
            ChipKind.Accent => _tokens.Accent,
            ChipKind.Locked => _tokens.Muted,
            ChipKind.Danger => _tokens.Danger,
            _ => _tokens.Edge,
        };
        var textColor = Kind switch
        {
            ChipKind.Accent => _tokens.Accent,
            ChipKind.Locked => _tokens.Muted,
            ChipKind.Danger => _tokens.Danger,
            _ => _tokens.Ink,
        };
        AddThemeColorOverride("font_color", textColor);
        AddThemeStyleboxOverride("normal", _tokens.ControlStyle(
            _tokens.PanelRaised,
            color,
            radius: _tokens.RadiusPill));
    }
}
