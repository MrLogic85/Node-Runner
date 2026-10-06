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
        BindAuthoredControls(_scrollContent);
        UiNativeScroll.AllowGesturesToBubble(_scrollContent);
        Callable.From(ResetScrollPosition).CallDeferred();
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
            "MissingPicker" =>
            [
                new("Front thigh", UiIconId.Beam, IconTint: UiTokens.Color.Detail),
            ],
            "LockedPicker" or "DisabledPicker" =>
            [
                new("Wheel 1", UiIconId.Beam),
            ],
            "FixedPartPicker" =>
            [
                new("Front thigh", UiIconId.Beam, IconTint: UiTokens.Color.Detail),
                new("Front shin", UiIconId.Beam, IconTint: UiTokens.Color.Detail),
            ],
            _ => picker.Options,
        };
    }

}
