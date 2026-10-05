using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// Draws every shadow in the Training arena as one picture faded once (#818), so where shadows
/// overlap they do not add up to a solid mass that hides the followed creature. The shadows are
/// drawn whole into a viewport of their own, which shares the arena's world and camera but draws
/// only what is on <see cref="ArenaVisibility.Shadows"/>; this node then shows that picture at the
/// theme's shadow opacity, and the arena's own viewport skips the shadows. The host puts it on
/// <see cref="ArenaLayers.Shadows"/>.
/// Godot's <c>CanvasGroup</c> would fade a group once too, but the parts' own draw layers would
/// escape it, and the shadows are not children of one node.
/// </summary>
public partial class ArenaShadows : Node2D
{
    private SubViewport? _shadowViewport;
    private SubViewport? _arenaViewport;
    private Vector2 _drawnSize;
    private VisualTheme _theme = VisualTheme.Neon;

    public VisualTheme Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            ApplyTheme();
        }
    }

    public override void _Ready()
    {
        _arenaViewport = GetViewport() as SubViewport
            ?? throw new InvalidOperationException($"{nameof(ArenaShadows)} must be in the arena's own viewport (UiWorldView).");
        _arenaViewport.CanvasCullMask &= ~ArenaVisibility.Shadows;
        _shadowViewport = new SubViewport
        {
            Name = "ShadowViewport",
            World2D = _arenaViewport.World2D,
            TransparentBg = true,
            Disable3D = true,
            CanvasCullMask = ArenaVisibility.Shadows,
        };
        AddChild(_shadowViewport);
        VisibilityLayer = ArenaVisibility.Arena;
        // The picture's colours are already multiplied by its own alpha, so it is blended as such,
        // as UiWorldView shows the arena's.
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.PremultAlpha };
        ApplyTheme();
        ApplyVisible();
        RenderingServer.FramePreDraw += FollowArena;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationVisibilityChanged)
        {
            ApplyVisible();
        }
    }

    public override void _ExitTree() => RenderingServer.FramePreDraw -= FollowArena;

    public override void _Draw()
    {
        if (_shadowViewport is not null)
        {
            DrawTextureRect(_shadowViewport.GetTexture(), new Rect2(Vector2.Zero, _drawnSize), tile: false);
        }
    }

    // Hidden, there is no picture to show, so the shadows' viewport stops drawing too.
    private void ApplyVisible()
    {
        if (_shadowViewport is not null)
        {
            _shadowViewport.RenderTargetUpdateMode = IsVisibleInTree()
                ? SubViewport.UpdateMode.Always
                : SubViewport.UpdateMode.Disabled;
        }
    }

    // A premultiplied picture fades by scaling every channel, not only its alpha.
    private void ApplyTheme()
    {
        var alpha = _theme.ShadowAlpha;
        Modulate = new Color(alpha, alpha, alpha, alpha);
    }

    // Just before each frame is drawn, after the camera has moved: the shadows' viewport takes the
    // arena's size and camera, and this node undoes the camera so the picture lies over the arena.
    private void FollowArena()
    {
        if (_arenaViewport is not { } arena || _shadowViewport is not { } shadows || !IsVisibleInTree())
        {
            return;
        }

        if (shadows.Size != arena.Size || shadows.Size2DOverride != arena.Size2DOverride)
        {
            shadows.Size = arena.Size;
            shadows.Size2DOverride = arena.Size2DOverride;
            shadows.Size2DOverrideStretch = arena.Size2DOverrideStretch;
        }

        shadows.CanvasTransform = arena.CanvasTransform;
        GlobalTransform = arena.CanvasTransform.AffineInverse();

        var size = arena.Size2DOverrideStretch && arena.Size2DOverride != Vector2I.Zero
            ? (Vector2)arena.Size2DOverride
            : (Vector2)arena.Size;
        if (size != _drawnSize)
        {
            _drawnSize = size;
            QueueRedraw();
        }
    }
}
