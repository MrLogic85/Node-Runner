using Godot;
using NodeRunner.App.Navigation;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>The pages reachable from the gallery toolbar's overflow menu.</summary>
public enum GalleryPage
{
    Components,
    Toolbars,
    ColorsAndStyles,
    PopupGallery,
}

/// <summary>
/// Shared behaviour of the gallery pages that carry the gallery toolbar: Back,
/// the theme switch, debug bounds and page navigation from the overflow menu.
/// Each page scene authors its own copy of the frame, toolbar and menu under the
/// same unique names and marks its own page item as selected.
/// The router opens each page as its own scene. The pages replace each other, so
/// Back from any of them returns to where the library was opened from. A routed
/// page also takes Android Back and Escape; an open menu or popup handles them
/// first. A page run on its own has no Back and leaves Android Back to its default.
/// </summary>
public abstract partial class GalleryScreen : Control, IRoutedScene
{
    [Export]
    public bool ShowDebugBounds
    {
        get => _showDebugBounds;
        set
        {
            _showDebugBounds = value;
            if (_boundsOverlay is not null)
            {
                _boundsOverlay.Visible = value;
                _boundsOverlay.SetProcess(value);
            }

            _debugBoundsItem?.On = value;
        }
    }

    /// <summary>Selected theme-switch segment: Neon or Paper.</summary>
    public int ThemeIndex
    {
        get => _themeIndex;
        set
        {
            _themeIndex = value;
            if (IsNodeReady())
            {
                _themeSwitcher?.SelectedIndex = value;
                ApplyTheme();
            }
        }
    }

    protected abstract GalleryPage Page { get; }

    protected UiToolbar? Toolbar { get; private set; }

    /// <summary>True while a popup the page opened handles Back itself.</summary>
    protected virtual bool HasOpenPopup => false;

    private bool _showDebugBounds;
    private int _themeIndex;
    private UiBoundsDebugOverlay? _boundsOverlay;
    private UiMenuToggleItem? _debugBoundsItem;
    private UiSegmentedSwitch? _themeSwitcher;
    private ISceneNavigator? _navigator;

    public void Enter(SceneRoute route, ISceneNavigator navigator)
    {
        _navigator = navigator;
        var back = new UiBackHandler { CanTakeBack = () => Toolbar is { Menu.Visible: false } && !HasOpenPopup };
        back.BackRequested += RequestClose;
        AddChild(back, @internal: InternalMode.Front);
        if (route is IGalleryRoute gallery)
        {
            ThemeIndex = gallery.ThemeIndex;
            ShowDebugBounds = gallery.ShowDebugBounds;
        }
    }

    // The theme goes on before the children are ready so they measure against it (#301).
    public override void _EnterTree()
    {
        ApplyTheme();
    }

    public override void _Ready()
    {
        Name = GetType().Name;
        UiLayout.ApplyScreen(this);
        BindToolbar();
        _boundsOverlay = new UiBoundsDebugOverlay
        {
            RootPath = GetNode<Control>("%UiFrame").GetPath(),
        };
        AddChild(_boundsOverlay);
        ShowDebugBounds = ShowDebugBounds || ProjectSettings.GetSetting("ui/component_gallery_debug_bounds", false).AsBool();
    }

    private void RequestClose() => _navigator?.Back();

    // The pages replace each other: the page left is not kept for Back.
    private void OpenPage(GalleryPage page) =>
        _navigator?.Navigate(new SceneNavigation(RouteFor(page), KeepCurrent: false));

    private SceneRoute RouteFor(GalleryPage page) => page switch
    {
        GalleryPage.Toolbars => new ToolbarsRoute(ThemeIndex, ShowDebugBounds),
        GalleryPage.ColorsAndStyles => new ColorsAndStylesRoute(ThemeIndex, ShowDebugBounds),
        GalleryPage.PopupGallery => new PopupGalleryRoute(ThemeIndex, ShowDebugBounds),
        GalleryPage.Components => new ComponentGalleryRoute(ThemeIndex, ShowDebugBounds),
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
    };

    protected virtual void OnThemeApplied()
    {
    }

    private void BindToolbar()
    {
        var toolbar = GetNode<UiToolbar>("%Toolbar");
        Toolbar = toolbar;
        toolbar.ShowBack = _navigator is not null;
        toolbar.BackPressed += RequestClose;

        _themeSwitcher = GetNode<UiSegmentedSwitch>("%ThemeSwitcher");
        _themeSwitcher.SelectedIndex = _themeIndex;
        _themeSwitcher.SelectionChanged += index => ThemeIndex = index;

        _debugBoundsItem = GetNode<UiMenuToggleItem>("%ToolbarMenuDebugBounds");
        _debugBoundsItem.On = ShowDebugBounds;
        _debugBoundsItem.Toggled += on => ShowDebugBounds = on;
        BindPageItem(GetNode<UiMenuActionItem>("%ToolbarMenuComponents"), GalleryPage.Components);
        BindPageItem(GetNode<UiMenuActionItem>("%ToolbarMenuToolbars"), GalleryPage.Toolbars);
        BindPageItem(GetNode<UiMenuActionItem>("%ToolbarMenuColorsAndStyles"), GalleryPage.ColorsAndStyles);
        BindPageItem(GetNode<UiMenuActionItem>("%ToolbarMenuPopupGallery"), GalleryPage.PopupGallery);
        VisibilityChanged += () =>
        {
            if (!IsVisibleInTree())
            {
                toolbar.CloseMenu();
            }
        };
    }

    private void BindPageItem(UiMenuActionItem item, GalleryPage page) =>
        item.Activated += () =>
        {
            Toolbar?.CloseMenu();
            if (page != Page)
            {
                OpenPage(page);
            }
        };

    private void ApplyTheme()
    {
        Theme = UiThemes.For(_themeIndex == 1 ? UiTokenType.Paper : UiTokenType.Neon);
        OnThemeApplied();
    }
}
