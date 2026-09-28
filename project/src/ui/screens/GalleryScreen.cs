using Godot;
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
/// Each page scene authors its own copy of the toolbar and menu under the same
/// unique names and marks its own page item as selected.
/// </summary>
public abstract partial class GalleryScreen : Control
{
    [Signal]
    public delegate void CloseRequestedEventHandler();

    [Signal]
    public delegate void PageRequestedEventHandler(GalleryPage page);

    [Export]
    public bool ShowCloseAction { get; set; }

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

    /// <summary>Selected theme-switch segment: Neon, Paper or Effects lite.</summary>
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

    private bool _showDebugBounds;
    private int _themeIndex;
    private UiBoundsDebugOverlay? _boundsOverlay;
    private UiMenuToggleItem? _debugBoundsItem;
    private UiSegmentedSwitch? _themeSwitcher;

    // The theme goes on before the children are ready so they measure against it (#301).
    public override void _EnterTree() => ApplyTheme();

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

    /// <summary>Opens another page. By default the page asks its host to do it.</summary>
    protected virtual void OpenPage(GalleryPage page) =>
        EmitSignal(SignalName.PageRequested, Variant.From(page));

    protected virtual void OnThemeApplied()
    {
    }

    private void BindToolbar()
    {
        var toolbar = GetNode<UiToolbar>("%Toolbar");
        Toolbar = toolbar;
        toolbar.ShowBack = ShowCloseAction;
        toolbar.BackPressed += () => EmitSignal(SignalName.CloseRequested);

        _themeSwitcher = GetNode<UiSegmentedSwitch>("%ThemeSwitcher");
        _themeSwitcher.SelectedIndex = _themeIndex;
        _themeSwitcher.SelectionChanged += index => ThemeIndex = index;

        _debugBoundsItem = GetNode<UiMenuToggleItem>("%ToolbarMenuDebugBounds");
        _debugBoundsItem.On = ShowDebugBounds;
        _debugBoundsItem.Toggled += on => ShowDebugBounds = on;
        BindPageItem("%ToolbarMenuComponents", GalleryPage.Components);
        BindPageItem("%ToolbarMenuToolbars", GalleryPage.Toolbars);
        BindPageItem("%ToolbarMenuColorsAndStyles", GalleryPage.ColorsAndStyles);
        BindPageItem("%ToolbarMenuPopupGallery", GalleryPage.PopupGallery);
        VisibilityChanged += () =>
        {
            if (!IsVisibleInTree())
            {
                toolbar.CloseMenu();
            }
        };
    }

    private void BindPageItem(string path, GalleryPage page) =>
        GetNode<UiMenuActionItem>(path).Activated += () =>
        {
            Toolbar?.CloseMenu();
            if (page != Page)
            {
                OpenPage(page);
            }
        };

    private void ApplyTheme()
    {
        Theme = UiThemes.For(_themeIndex switch
        {
            1 => UiTokenType.Paper,
            2 => UiTokenType.Light,
            _ => UiTokenType.Neon,
        });
        OnThemeApplied();
    }
}
