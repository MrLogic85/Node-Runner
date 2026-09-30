using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Visual inventory of the design foundations: the colour tokens in both themes,
/// icon sizes, the icon set and the text styles. The whole page is authored in its scene.
/// </summary>
public partial class ColorsAndStylesScreen : GalleryScreen
{
    private ScrollContainer? _scroll;

    protected override GalleryPage Page => GalleryPage.ColorsAndStyles;

    public override void _Ready()
    {
        base._Ready();
        _scroll = GetNode<ScrollContainer>("%Scroll");
        UiNativeScroll.AllowGesturesToBubble(GetNode<MarginContainer>("%ContentFrame"));
        Callable.From(ResetScrollPosition).CallDeferred();
    }

    private void ResetScrollPosition()
    {
        if (_scroll is not null)
        {
            _scroll.ScrollVertical = 0;
        }
    }
}
