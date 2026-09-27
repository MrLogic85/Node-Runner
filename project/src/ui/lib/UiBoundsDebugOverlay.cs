using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Developer overlay that draws global bounds for visible UI controls.</summary>
public partial class UiBoundsDebugOverlay : Control
{
    /// <summary>Theme hues cycled per tree depth so nested bounds stay distinguishable.</summary>
    private static readonly UiTokens.Color[] _depthColors =
    [
        UiTokens.Color.Accent,
        UiTokens.Color.Danger,
        UiTokens.Color.Halo,
        UiTokens.Color.Output,
        UiTokens.Color.Ink,
    ];

    private const float _outlineAlpha = 0.72f;
    private const float _fillAlpha = 0.08f;

    [Export]
    public NodePath RootPath { get; set; } = new(".");

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetProcess(true);
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (RootPath.IsEmpty)
        {
            return;
        }

        if (GetNodeOrNull<Control>(RootPath) is not { } root)
        {
            return;
        }

        DrawBounds(root, depth: 0);
    }

    private void DrawBounds(Control control, int depth)
    {
        if (control == this || !control.Visible || control.Size.X <= 0 || control.Size.Y <= 0)
        {
            return;
        }

        var rect = new Rect2(
            GetGlobalTransformWithCanvas().AffineInverse() * control.GetGlobalRect().Position,
            control.GetGlobalRect().Size);
        var color = UiThemeLookup.Color(this, _depthColors[depth % _depthColors.Length]).WithAlpha(_outlineAlpha);
        DrawRect(rect, color.WithAlpha(_fillAlpha), filled: true);
        DrawRect(rect, color, filled: false, width: 1f, antialiased: false);

        foreach (var child in control.GetChildren().OfType<Control>())
        {
            DrawBounds(child, depth + 1);
        }
    }
}
