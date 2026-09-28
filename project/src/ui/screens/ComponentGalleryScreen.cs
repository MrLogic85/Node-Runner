using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Standalone component-kit gallery for Phase 1 visual verification. It keeps
/// reusable controls disconnected from game state and lets developers switch
/// tokens live to prove token updates do not require rebuilding the screen.
/// </summary>
public partial class ComponentGalleryScreen : GalleryScreen
{
    private ScrollContainer? _scroll;
    private Control? _scrollContent;

    protected override GalleryPage Page => GalleryPage.Components;

    public override void _Ready()
    {
        base._Ready();
        _scroll = GetNode<ScrollContainer>("%Scroll");
        _scrollContent = GetNode<MarginContainer>("%ContentFrame");
        var runtimeSections = GetNode<VBoxContainer>("%RuntimeSections");
        BindAuthoredControls(runtimeSections.GetParent<Control>());
        UiNativeScroll.AllowGesturesToBubble(_scrollContent);
        Callable.From(ResetScrollPosition).CallDeferred();
    }

    protected override void OpenPage(GalleryPage page)
    {
        switch (page)
        {
            case GalleryPage.Toolbars:
                OpenGalleryPage(GD.Load<PackedScene>("res://scenes/screens/ToolbarsScreen.tscn").Instantiate<ToolbarsScreen>());
                break;
            case GalleryPage.ColorsAndStyles:
                var screen = GD.Load<PackedScene>("res://scenes/screens/ColorsAndStylesScreen.tscn").Instantiate<ColorsAndStylesScreen>();
                screen.ShowCloseAction = true;
                screen.CloseRequested += () => ReturnFrom(screen);
                OpenOnTop(screen);
                break;
            case GalleryPage.PopupGallery:
                var gallery = GD.Load<PackedScene>("res://scenes/screens/PopupGalleryScreen.tscn").Instantiate<PopupGalleryScreen>();
                gallery.CloseRequested += () => ReturnFrom(gallery);
                OpenOnTop(gallery);
                break;
        }
    }

    protected override void OnThemeApplied()
    {
        if (_scrollContent is not null)
        {
            UiNativeScroll.AllowGesturesToBubble(_scrollContent);
        }
    }

    private void ResetScrollPosition()
    {
        if (_scroll is not null)
        {
            _scroll.ScrollVertical = 0;
        }
    }

    // A page carrying the gallery toolbar starts with this page's theme and
    // debug bounds, hands them back when it closes, and can switch straight
    // to another page.
    private void OpenGalleryPage(GalleryScreen page)
    {
        page.ShowCloseAction = true;
        page.ThemeIndex = ThemeIndex;
        page.ShowDebugBounds = ShowDebugBounds;
        page.CloseRequested += () => ReturnFrom(page);
        page.PageRequested += next =>
        {
            ReturnFrom(page);
            OpenPage(next);
        };
        OpenOnTop(page);
    }

    // Shows another gallery page in place of this one until it asks to close.
    private void OpenOnTop(Control screen)
    {
        Toolbar?.CloseMenu();
        GetParent().AddChild(screen);
        Hide();
    }

    private void ReturnFrom(Control screen)
    {
        if (screen is GalleryScreen page)
        {
            ThemeIndex = page.ThemeIndex;
            ShowDebugBounds = page.ShowDebugBounds;
        }

        screen.QueueFree();
        Show();
    }

    private void BindAuthoredControls(Control control)
    {
        if (control is UiButton button)
        {
            if (button.IsInGroup("gallery_toggle_button"))
            {
                button.Activated += () => button.Selected = !button.Selected;
            }
            return;
        }

        if (control is UiStageCard stageCard)
        {
            BindAuthoredStageCard(stageCard);
            return;
        }

        if (control is UiCard card)
        {
            foreach (var child in card.GetChildren().OfType<Control>())
            {
                BindAuthoredControls(child);
            }
            return;
        }

        if (control is UiMenu menu)
        {
            foreach (var child in menu.GetChildren().OfType<Control>())
            {
                BindAuthoredControls(child);
            }
            return;
        }

        if (control is UiPicker picker)
        {
            BindAuthoredPicker(picker);
            return;
        }

        if (control is UiToggleRow
            or UiCheckRow
            or UiSegmentedSwitch
            or UiIconTabs
            or UiSelectionHandle
            or UiNumber
            or UiSlider
            or UiProgressRing
            or UiPartRow
            or UiMenuItem
            or UiNameField
            or UiValueRow
            or UiNoteRow)
        {
            return;
        }

        foreach (var child in control.GetChildren().OfType<Control>())
        {
            BindAuthoredControls(child);
        }
    }

    private static void BindAuthoredPicker(UiPicker picker)
    {
        picker.Options = picker.Name.ToString() switch
        {
            "InteractivePicker" =>
            [
                new("Left thigh", UiIconId.Beam),
                new("Left shin", UiIconId.Beam, Note: "swaps"),
                new("Tail"),
            ],
            "LockedPicker" or "DisabledPicker" =>
            [
                new("Wheel 1", UiIconId.Beam),
            ],
            "FixedPartPicker" =>
            [
                new("Front thigh", UiIconId.Beam),
                new("Front shin", UiIconId.Beam),
            ],
            _ => picker.Options,
        };
    }

    private static void BindAuthoredStageCard(UiStageCard stageCard)
    {
        var body = stageCard.Name.ToString() switch
        {
            "Senses" => "9 readings · top first",
            "Thinks" => "24 neurons · 8 outputs",
            _ => string.Empty,
        };
        if (!string.IsNullOrWhiteSpace(body))
        {
            stageCard.SetBody(new Label
            {
                Text = body,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore,
            });
        }
    }

}
