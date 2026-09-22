using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiButtonStyleTests
{
    [Fact]
    public void Styles_ResolveCanonicalRestAndSelectedColors()
    {
        var tokens = UiTokens.Neon;

        UiButtonStyle.Primary.Resolve(tokens).ShouldBe(new UiResolvedButtonStyle(
            tokens.Accent, tokens.Accent, tokens.OnAccent, tokens.Halo, tokens.Accent));
        UiButtonStyle.Secondary.Resolve(tokens).ShouldBe(new UiResolvedButtonStyle(
            tokens.PanelRaised, tokens.LineStrong, tokens.Ink, tokens.Accent, null));
        UiButtonStyle.Tertiary.Resolve(tokens).ShouldBe(new UiResolvedButtonStyle(
            tokens.PanelRaised, tokens.Danger, tokens.Danger, tokens.Danger, null));
        UiButtonStyle.Flat.Resolve(tokens).ShouldBe(new UiResolvedButtonStyle(
            Colors.Transparent, Colors.Transparent, tokens.Ink, tokens.Ink, null));
    }

    [Fact]
    public void Styles_OnlyPrimaryDefinesARestGlow()
    {
        foreach (var style in new[]
                 {
                     UiButtonStyle.Secondary,
                     UiButtonStyle.Tertiary,
                     UiButtonStyle.Flat,
                 })
        {
            style.RestGlowColor.ShouldBeNull();
        }

        UiButtonStyle.Primary.RestGlowColor.ShouldBe(UiColor.Accent);
    }

    [Fact]
    public void SelectedState_ReplacesRestGlowAndUsesEachStylesSelectedColor()
    {
        var tokens = UiTokens.Neon;
        foreach (var style in new[]
                 {
                     UiButtonStyle.Primary,
                     UiButtonStyle.Secondary,
                     UiButtonStyle.Tertiary,
                     UiButtonStyle.Flat,
                 })
        {
            var resolved = style.Resolve(tokens);
            style.BorderFor(tokens, selected: true).ShouldBe(resolved.Selected);
            style.GlowBaseFor(tokens, selected: true).ShouldBe(resolved.Selected);
        }

        UiButtonStyle.Primary.GlowBaseFor(tokens, selected: false).ShouldBe(tokens.Accent);
        UiButtonStyle.Secondary.GlowBaseFor(tokens, selected: false).ShouldBeNull();
        UiButtonStyle.Tertiary.GlowBaseFor(tokens, selected: false).ShouldBeNull();
        UiButtonStyle.Flat.GlowBaseFor(tokens, selected: false).ShouldBeNull();
    }

    [Fact]
    public void SelectedPrimary_UsesTheSelectedGlowInsteadOfItsRestGlow()
    {
        var tokens = UiTokens.Neon;
        var primary = UiButtonStyle.Primary;

        primary.GlowBaseFor(tokens, selected: true).ShouldBe(tokens.Halo);
        primary.GlowBaseFor(tokens, selected: true).ShouldNotBe(
            primary.GlowBaseFor(tokens, selected: false));
        UiGlow.FromButtonBase(
                primary.GlowBaseFor(tokens, selected: true)!.Value,
                enabled: true)
            .ShouldBe(UiTokens.MultiplyAlpha(tokens.Halo, UiGlow.ButtonOpacity));
    }

    [Fact]
    public void Glow_IsDerivedFromSelectedBaseColorAndSuppressedForEffectsLite()
    {
        var selected = UiButtonStyle.Primary.Resolve(UiTokens.Neon).Selected;
        UiGlow.FromButtonBase(selected, enabled: true).ShouldBe(
            UiTokens.MultiplyAlpha(selected, UiGlow.ButtonOpacity));
        UiGlow.FromButtonBase(selected, enabled: false).ShouldBe(Colors.Transparent);
        UiGlow.FromButtonBase(
            UiButtonStyle.Primary.Resolve(UiTokens.Paper).Selected,
            UiTokens.Paper.EffectsEnabled).ShouldBe(Colors.Transparent);
    }

    [Fact]
    public void DisabledStyles_SuppressRestAndSelectedGlowForEveryKind()
    {
        var tokens = UiTokens.Neon;
        foreach (var style in new[]
                 {
                     UiButtonStyle.Primary,
                     UiButtonStyle.Secondary,
                     UiButtonStyle.Tertiary,
                     UiButtonStyle.Flat,
                 })
        {
            style.GlowBaseFor(tokens, selected: false, enabled: false).ShouldBeNull();
            style.GlowBaseFor(tokens, selected: true, enabled: false).ShouldBeNull();
        }
    }

    [Fact]
    public void Metrics_KeepTouchTargetsAndScaleAllButtonGeometryFromTokens()
    {
        var metrics = UiButtonMetrics.From(new UiTokens
        {
            TouchTarget = 96,
            ControlHeight = 80,
            ControlSmall = 64,
            BadgeMinimumSize = 32,
            BadgeOffset = 8,
            ButtonSelectedStroke = 4,
        });

        metrics.TouchTarget.ShouldBe(96);
        metrics.VisibleControlSize(UiButtonContentLayout.Row, compact: false).ShouldBe(80);
        metrics.VisibleControlSize(UiButtonContentLayout.Row, compact: true).ShouldBe(64);
        metrics.VisibleControlSize(UiButtonContentLayout.Icon, compact: false).ShouldBe(80);
        metrics.VisibleControlSize(UiButtonContentLayout.Icon, compact: true).ShouldBe(64);
        metrics.VisibleControlSize(UiButtonContentLayout.Stack, compact: true).ShouldBe(96);
        metrics.BadgeMinimumSize.ShouldBe(32);
        metrics.BadgeOffset.ShouldBe(8);
        metrics.SelectedStroke.ShouldBe(4);
    }

    [Fact]
    public void BadgePosition_AnchorsToTheVisibleIconFrameRatherThanItsTouchTarget()
    {
        var metrics = UiButtonMetrics.From(UiTokens.Neon);

        metrics.BadgePosition(
                new Vector2(metrics.TouchTarget, metrics.TouchTarget),
                UiButtonContentLayout.Icon,
                compact: true)
            .ShouldBe(new Vector2(28, 4));
        metrics.BadgePosition(
                new Vector2(metrics.TouchTarget, metrics.TouchTarget),
                UiButtonContentLayout.Icon,
                compact: false)
            .ShouldBe(new Vector2(32, 0));
        metrics.BadgePosition(
                new Vector2(120, metrics.TouchTarget),
                UiButtonContentLayout.Row,
                compact: false)
            .ShouldBe(new Vector2(108, 0));
    }

    [Fact]
    public void VisibleFrame_ConstrainsHoldLayersWithoutClippingBadgeOrGlowSpace()
    {
        var metrics = UiButtonMetrics.From(UiTokens.Neon);

        metrics.VisibleFrame(
                new Vector2(metrics.TouchTarget, metrics.TouchTarget),
                UiButtonContentLayout.Icon,
                compact: false)
            .ShouldBe(new Rect2(4, 4, 40, 40));
        metrics.VisibleFrame(
                new Vector2(metrics.TouchTarget, metrics.TouchTarget),
                UiButtonContentLayout.Icon,
                compact: true)
            .ShouldBe(new Rect2(8, 8, 32, 32));
        metrics.VisibleFrame(
                new Vector2(120, metrics.TouchTarget),
                UiButtonContentLayout.Row,
                compact: false)
            .ShouldBe(new Rect2(0, 4, 120, 40));
    }

    [Fact]
    public void ProgressLayout_KeepsFillAtFullFrameSoRoundedCornersNeverCollapse()
    {
        var frame = new Rect2(0, 4, 200, 40);

        // Regression: sizing the fill itself to the held fraction made Godot
        // shrink its corner radii to fit, so early hold frames drew a square
        // edged strip outside the button's rounded contour.
        foreach (var progress in new[] { 0f, 0.02f, 0.25f, 0.5f, 0.99f, 1f })
        {
            UiButtonMetrics.ProgressLayout(frame, progress)
                .Fill
                .ShouldBe(new Rect2(0, 0, 200, 40));
        }
    }

    [Fact]
    public void ProgressLayout_RevealWindowStaysInsideTheFillAndTracksProgress()
    {
        var frame = new Rect2(12, 4, 160, 40);
        var previous = 0f;
        for (var step = 0; step <= 10; step++)
        {
            var layout = UiButtonMetrics.ProgressLayout(frame, step / 10f);

            layout.Reveal.Position.ShouldBe(Vector2.Zero);
            layout.Reveal.Size.Y.ShouldBe(layout.Fill.Size.Y);
            layout.Reveal.Size.X.ShouldBeGreaterThanOrEqualTo(previous);
            layout.Reveal.Size.X.ShouldBeLessThanOrEqualTo(layout.Fill.Size.X);
            previous = layout.Reveal.Size.X;
        }

        previous.ShouldBe(160);
    }

    [Fact]
    public void ProgressLayout_HidesTheFillBeforeAndAtTheStartOfAHold()
    {
        var frame = new Rect2(0, 4, 200, 40);

        UiButtonMetrics.ProgressLayout(frame, -1).Reveal.Size.X.ShouldBe(0);
        UiButtonMetrics.ProgressLayout(frame, 0).Reveal.Size.X.ShouldBe(0);
    }

    [Fact]
    public void ProgressLayout_MatchesTheVisibleFrameForEveryLayout()
    {
        var metrics = UiButtonMetrics.From(UiTokens.Neon);
        foreach (var layout in new[]
                 {
                     UiButtonContentLayout.Row,
                     UiButtonContentLayout.Icon,
                     UiButtonContentLayout.Stack,
                 })
        {
            foreach (var compact in new[] { false, true })
            {
                var frame = metrics.VisibleFrame(
                    new Vector2(200, metrics.TouchTarget),
                    layout,
                    compact);

                UiButtonMetrics.ProgressLayout(frame, 0.5f)
                    .Fill
                    .Size
                    .ShouldBe(frame.Size);
            }
        }
    }
}
