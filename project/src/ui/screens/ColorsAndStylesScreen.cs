using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Visual inventory of the design foundations: the colour tokens in both themes,
/// icon sizes, the icon set and the text styles. The whole page is authored in its scene.
/// It also sets the UI size for the session (#299, #738) until Settings has it (#201).
/// </summary>
public partial class ColorsAndStylesScreen : GalleryScreen
{
    // The segments, in order. Min and 100% keep their authored text; Auto and Max add their value.
    private static readonly UiSizeChoice[] _uiSizeChoices =
        [UiSizeChoice.Fixed(UiScale.MinPercent), UiSizeChoice.Fixed(UiScale.OnePixelPercent), UiSizeChoice.Auto, UiSizeChoice.Max];

    private ScrollContainer? _scroll;
    private UiScale? _uiScale;
    private UiSegmentedSwitch? _uiSizeSwitcher;
    private string[] _uiSizeNames = [];

    protected override GalleryPage Page => GalleryPage.ColorsAndStyles;

    public override void _Ready()
    {
        base._Ready();
        _scroll = GetNode<ScrollContainer>("%Scroll");
        UiNativeScroll.AllowGesturesToBubble(GetNode<MarginContainer>("%ContentFrame"));
        BindUiSize();
        Callable.From(ResetScrollPosition).CallDeferred();
    }

    public override void _ExitTree()
    {
        _uiScale?.Changed -= ShowUiSize;
    }

    private void BindUiSize()
    {
        _uiScale = UiScale.Of(this);
        if (_uiScale is null)
        {
            return;
        }

        _uiSizeSwitcher = GetNode<UiSegmentedSwitch>("%UiSizeSwitcher");
        // Own copies, changed in place: assigning Segments would rebuild the buttons and grab focus.
        _uiSizeNames = [.. _uiSizeSwitcher.Segments.Select(segment => segment.Text)];
        _uiSizeSwitcher.Segments = [.. _uiSizeSwitcher.Segments.Select(segment => (UiSegment)segment.Duplicate())];
        _uiSizeSwitcher.SelectionChanged += ChooseUiSize;
        _uiScale.Changed += ShowUiSize;
        ShowUiSize();
    }

    private void ChooseUiSize(int index) => _uiScale?.Choose(_uiSizeChoices[index]);

    private void ShowUiSize()
    {
        if (_uiScale is not { } uiScale || _uiSizeSwitcher is not { } switcher)
        {
            return;
        }

        for (var index = 0; index < _uiSizeChoices.Length; index++)
        {
            var choice = _uiSizeChoices[index];
            var segment = switcher.Segments[index];
            if (choice.Kind != UiSizeChoice.Mode.Fixed)
            {
                segment.Text = $"{_uiSizeNames[index]} {choice.Resolve(uiScale.AutoPercent, uiScale.MaxPercent)}%";
            }

            segment.Disabled = !choice.Fits(uiScale.MaxPercent);
        }

        switcher.SelectedIndex = Array.IndexOf(_uiSizeChoices, uiScale.Chosen);
    }

    private void ResetScrollPosition()
    {
        if (_scroll is not null)
        {
            _scroll.ScrollVertical = 0;
        }
    }
}
