using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A Button icon that samples with <see cref="UiIcons.IconFilter"/> while the Button's text keeps
/// Nearest (#734). The Button lays out and tints it as usual; it draws <see cref="Icon"/> on its own
/// child canvas item, freed when the Button leaves the tree or <see cref="Detach"/>es it.
/// Godot's <c>CanvasTexture</c> can set a filter, but draws a <c>DPITexture</c> at its unscaled
/// RID, losing the oversampling; a child TextureRect would redo the Button's icon layout and tint.
/// </summary>
public sealed partial class UiLinearIcon : Texture2D
{
    private readonly Button? _button;
    private Texture2D? _icon;
    private Rid _layer;
    private Rid _parent;

    public UiLinearIcon(Button button)
    {
        _button = button;
        button.TreeExiting += Release;
    }

    // For Godot; an icon without a Button never frees its layer.
    private UiLinearIcon()
    {
    }

    public Texture2D? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            if (_layer.IsValid)
            {
                RenderingServer.CanvasItemClear(_layer);
            }
            EmitChanged();
        }
    }

    /// <summary>Frees the layer and stops following the Button; call when the Button drops this icon.</summary>
    public void Detach()
    {
        if (_button is not null && IsInstanceValid(_button))
        {
            _button.TreeExiting -= Release;
        }
        Release();
    }

    public override int _GetWidth() => _icon?.GetWidth() ?? 0;

    public override int _GetHeight() => _icon?.GetHeight() ?? 0;

    public override bool _HasAlpha() => true;

    public override bool _IsPixelOpaque(int x, int y) => false;

    public override void _Draw(Rid toCanvasItem, Vector2 pos, Color modulate, bool transpose) =>
        _DrawRect(toCanvasItem, new Rect2(pos, GetSize()), tile: false, modulate, transpose);

    public override void _DrawRect(Rid toCanvasItem, Rect2 rect, bool tile, Color modulate, bool transpose)
    {
        if (_icon is not null)
        {
            _icon.DrawRect(Layer(toCanvasItem), rect, tile, modulate, transpose);
        }
    }

    public override void _DrawRectRegion(Rid toCanvasItem, Rect2 rect, Rect2 srcRect, Color modulate, bool transpose, bool clipUV)
    {
        if (_icon is not null)
        {
            _icon.DrawRectRegion(Layer(toCanvasItem), rect, srcRect, modulate, transpose, clipUV);
        }
    }

    // C# RefCounted scripts never get NotificationPredelete, so the Button's tree exit frees the layer;
    // its next draw makes a new one.
    private void Release()
    {
        if (_layer.IsValid)
        {
            RenderingServer.FreeRid(_layer);
        }
        _layer = default;
        _parent = default;
    }

    // The Button redraws its whole canvas item, so the layer is cleared and drawn with it.
    private Rid Layer(Rid parent)
    {
        if (!_layer.IsValid)
        {
            _layer = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetDefaultTextureFilter(_layer, (RenderingServer.CanvasItemTextureFilter)UiIcons.IconFilter);
        }
        if (_parent != parent)
        {
            _parent = parent;
            RenderingServer.CanvasItemSetParent(_layer, parent);
        }

        RenderingServer.CanvasItemClear(_layer);
        return _layer;
    }
}
