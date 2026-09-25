using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Shared sizing, availability, and token contract for direct UiMenu items.</summary>
[Tool]
public abstract partial class UiMenuItem : Container
{
    public enum MenuItemSize
    {
        Standard,
        Compact,
    }

    private MenuItemSize _sizeVariant;
    private bool _disabled;
    private bool _selected;

    [Export]
    public MenuItemSize SizeVariant
    {
        get => _sizeVariant;
        set
        {
            if (_sizeVariant == value)
            {
                return;
            }
            _sizeVariant = value;
            RefreshItem();
            RefreshLayout();
        }
    }

    [Export]
    public bool Disabled
    {
        get => _disabled;
        set
        {
            if (_disabled == value)
            {
                return;
            }
            _disabled = value;
            RefreshItem();
        }
    }

    [Export]
    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }
            _selected = value;
            QueueRedraw();
            RefreshItem();
        }
    }

    public UiTokens Tokens
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
            RefreshItem();
            RefreshLayout();
            QueueRedraw();
        }
    } = UiTokens.Neon;

    protected float RowHeight =>
        SizeVariant == MenuItemSize.Compact
            ? Tokens.ControlSmall
            : Tokens.TouchTarget;

    protected float HorizontalPadding =>
        SizeVariant == MenuItemSize.Compact
            ? Tokens.Space2
            : Tokens.Space3;

    protected void RefreshLayout()
    {
        UpdateMinimumSize();
        QueueSort();
    }

    public override void _Draw()
    {
        if (Selected)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), Tokens.AccentSoft);
        }
    }

    protected abstract void RefreshItem();
}
