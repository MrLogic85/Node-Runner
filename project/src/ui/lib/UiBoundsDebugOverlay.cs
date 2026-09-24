using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Developer overlay that draws global bounds for visible UI controls.</summary>
public partial class UiBoundsDebugOverlay : Control
{
    private static readonly Color[] _palette =
    [
        new(0.12f, 0.95f, 1f, 0.72f),
        new(1f, 0.45f, 0.65f, 0.72f),
        new(1f, 0.83f, 0.22f, 0.72f),
        new(0.5f, 1f, 0.42f, 0.72f),
        new(0.72f, 0.55f, 1f, 0.72f),
    ];

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
        var color = _palette[depth % _palette.Length];
        DrawRect(rect, color with { A = 0.08f }, filled: true);
        DrawRect(rect, color, filled: false, width: 1f, antialiased: true);

        foreach (var child in control.GetChildren().OfType<Control>())
        {
            DrawBounds(child, depth + 1);
        }
    }
}
