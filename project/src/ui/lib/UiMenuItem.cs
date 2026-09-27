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

    protected float RowHeight =>
        SizeVariant == MenuItemSize.Compact
            ? UiSize.Control.Small
            : UiSize.Control.Touch;

    protected float HorizontalPadding =>
        SizeVariant == MenuItemSize.Compact
            ? UiSize.Space.S2
            : UiSize.Space.S3;

    protected void RefreshLayout()
    {
        UpdateMinimumSize();
        QueueSort();
    }

    public override void _Draw()
    {
        if (Selected)
        {
            DrawRect(
                new Rect2(Vector2.Zero, Size),
                UiThemeLookup.Color(this, UiTokens.Color.Accent).WithAlpha(UiThemeLookup.Alpha(this, UiTokens.Alpha.Soft)));
        }
    }

    protected abstract void RefreshItem();
}
