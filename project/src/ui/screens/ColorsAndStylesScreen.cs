using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Visual inventory of the design foundations: the colour tokens in both themes,
/// icon sizes, the icon set and the text styles. The whole page is authored in its scene.
/// It also sets the UI size for the session (#299) until Settings has it (#201).
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
        BindUiSize();
        Callable.From(ResetScrollPosition).CallDeferred();
    }

    private void BindUiSize()
    {
        var switcher = GetNode<UiSegmentedSwitch>("%UiSizeSwitcher");
        if (UiScale.Of(this) is not { } uiScale)
        {
            return;
        }

        var sizes = UiScale.MarkedPercents;
        switcher.SelectedIndex = Math.Max(0, sizes.ToList().IndexOf(uiScale.Percent));
        switcher.SelectionChanged += index => uiScale.SetPercent(sizes[index]);
    }

    private void ResetScrollPosition()
    {
        if (_scroll is not null)
        {
            _scroll.ScrollVertical = 0;
        }
    }
}
