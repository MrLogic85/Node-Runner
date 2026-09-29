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
/// Each page scene authors its own copy of the frame, toolbar and menu under the
/// same unique names and marks its own page item as selected.
/// A page with a Back action also takes Android Back and Escape; an open menu or
/// popup handles them first. Without one, Android Back keeps its default.
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
    private bool _ownsBack;

    // A page that replaces another enters before the old one leaves, so the pages share
    // one hold on QuitOnGoBack rather than each saving and restoring it.
    private static int _backOwners;
    private static bool _quitOnBackBeforePages;

    // The theme goes on before the children are ready so they measure against it (#301).
    public override void _EnterTree()
    {
        ApplyTheme();
        if (ShowCloseAction && !Engine.IsEditorHint())
        {
            _ownsBack = true;
            if (_backOwners++ == 0)
            {
                _quitOnBackBeforePages = GetTree().QuitOnGoBack;
            }

            GetTree().QuitOnGoBack = false;
        }
    }

    public override void _ExitTree()
    {
        if (_ownsBack)
        {
            _ownsBack = false;
            if (--_backOwners == 0)
            {
                GetTree().QuitOnGoBack = _quitOnBackBeforePages;
            }
        }
    }

    // The screen hears Android Back before the menu and popups inside it, so it can
    // leave Back to them; acting deferred keeps one Back from also reaching the next page.
    public override void _Notification(int what)
    {
        if (what == NotificationWMGoBackRequest && CanTakeBack())
        {
            Callable.From(RequestClose).CallDeferred();
        }
    }

    // The menu takes Escape first as it is deeper in the tree.
    public override void _UnhandledKeyInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("ui_cancel") && CanTakeBack())
        {
            GetViewport().SetInputAsHandled();
            RequestClose();
        }
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

    private bool CanTakeBack() =>
        _ownsBack && IsVisibleInTree() && Toolbar is { Menu.Visible: false } && !HasOpenPopup;

    private void RequestClose() => EmitSignal(SignalName.CloseRequested);

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
        toolbar.BackPressed += RequestClose;

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
        Theme = UiThemes.For(_themeIndex == 1 ? UiTokenType.Paper : UiTokenType.Neon);
        OnThemeApplied();
    }
}
