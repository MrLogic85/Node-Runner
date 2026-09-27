using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiButtonStyleTests
{
    [Fact]
    public void Styles_ResolveCanonicalRestAndSelectedColors()
    {
        var tokens = ThemeFile.For(UiTokenType.Neon);
        Color Resolve(UiTokens.Color token) => ResolveColor(tokens, token);

        UiButtonStyle.Primary.Resolve(Resolve).ShouldBe(new UiResolvedButtonStyle(
            tokens.Color(UiTokens.Color.Accent), tokens.Color(UiTokens.Color.Accent), tokens.Color(UiTokens.Color.OnAccent), tokens.Color(UiTokens.Color.Halo), tokens.Color(UiTokens.Color.Accent)));
        UiButtonStyle.Secondary.Resolve(Resolve).ShouldBe(new UiResolvedButtonStyle(
            tokens.Color(UiTokens.Color.PanelRaised), tokens.Color(UiTokens.Color.LineStrong), tokens.Color(UiTokens.Color.Ink), tokens.Color(UiTokens.Color.Accent), null));
        UiButtonStyle.Tertiary.Resolve(Resolve).ShouldBe(new UiResolvedButtonStyle(
            tokens.Color(UiTokens.Color.PanelRaised), tokens.Color(UiTokens.Color.Danger), tokens.Color(UiTokens.Color.Danger), tokens.Color(UiTokens.Color.Danger), null));
        UiButtonStyle.Flat.Resolve(Resolve).ShouldBe(new UiResolvedButtonStyle(
            Colors.Transparent, Colors.Transparent, tokens.Color(UiTokens.Color.Ink), tokens.Color(UiTokens.Color.Ink), null));
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

        UiButtonStyle.Primary.RestGlowColor.ShouldBe(UiTokens.Color.Accent);
    }

    [Fact]
    public void SelectedState_ReplacesRestGlowAndUsesEachStylesSelectedColor()
    {
        var tokens = ThemeFile.For(UiTokenType.Neon);
        Color Resolve(UiTokens.Color token) => ResolveColor(tokens, token);
        foreach (var style in new[]
                 {
                     UiButtonStyle.Primary,
                     UiButtonStyle.Secondary,
                     UiButtonStyle.Tertiary,
                     UiButtonStyle.Flat,
                 })
        {
            var resolved = style.Resolve(Resolve);
            style.BorderFor(Resolve, selected: true).ShouldBe(resolved.Selected);
            style.GlowBaseFor(Resolve, selected: true).ShouldBe(resolved.Selected);
        }

        UiButtonStyle.Primary.GlowBaseFor(Resolve, selected: false).ShouldBe(tokens.Color(UiTokens.Color.Accent));
        UiButtonStyle.Secondary.GlowBaseFor(Resolve, selected: false).ShouldBeNull();
        UiButtonStyle.Tertiary.GlowBaseFor(Resolve, selected: false).ShouldBeNull();
        UiButtonStyle.Flat.GlowBaseFor(Resolve, selected: false).ShouldBeNull();
    }

    [Fact]
    public void SelectedPrimary_UsesTheSelectedGlowInsteadOfItsRestGlow()
    {
        var tokens = ThemeFile.For(UiTokenType.Neon);
        var primary = UiButtonStyle.Primary;
        Color Resolve(UiTokens.Color token) => ResolveColor(tokens, token);

        primary.GlowBaseFor(Resolve, selected: true).ShouldBe(tokens.Color(UiTokens.Color.Halo));
        primary.GlowBaseFor(Resolve, selected: true).ShouldNotBe(
            primary.GlowBaseFor(Resolve, selected: false));
        UiGlow.FromBase(
                primary.GlowBaseFor(Resolve, selected: true)!.Value,
                enabled: true)
            .ShouldBe(tokens.Color(UiTokens.Color.Halo).ScaleAlpha(UiGlow.Opacity));
    }

    [Fact]
    public void Glow_IsDerivedFromSelectedBaseColorAndSuppressedForEffectsLite()
    {
        var selected = UiButtonStyle.Primary.Resolve(color => ResolveColor(ThemeFile.For(UiTokenType.Neon), color)).Selected;
        UiGlow.FromBase(selected, enabled: true).ShouldBe(
            selected.ScaleAlpha(UiGlow.Opacity));
        UiGlow.FromBase(selected, enabled: false).ShouldBe(Colors.Transparent);
        UiGlow.FromBase(
            UiButtonStyle.Primary.Resolve(color => ResolveColor(ThemeFile.For(UiTokenType.Paper), color)).Selected,
            ThemeFile.For(UiTokenType.Paper).Flag(UiTokens.Flag.EffectsEnabled)).ShouldBe(Colors.Transparent);
    }

    [Fact]
    public void DisabledStyles_SuppressRestAndSelectedGlowForEveryKind()
    {
        var tokens = ThemeFile.For(UiTokenType.Neon);
        Color Resolve(UiTokens.Color token) => ResolveColor(tokens, token);
        foreach (var style in new[]
                 {
                     UiButtonStyle.Primary,
                     UiButtonStyle.Secondary,
                     UiButtonStyle.Tertiary,
                     UiButtonStyle.Flat,
                 })
        {
            style.GlowBaseFor(Resolve, selected: false, enabled: false).ShouldBeNull();
            style.GlowBaseFor(Resolve, selected: true, enabled: false).ShouldBeNull();
        }

    }

    [Fact]
    public void Metrics_UseVisibleSizesWithoutExtraTouchMargins()
    {
        var metrics = UiButtonMetrics.Default;

        metrics.TouchTarget.ShouldBe(UiSize.Control.Touch);
        metrics.MinimumSize(UiButtonContentLayout.RowCompact).ShouldBe(new Vector2(UiSize.Control.Small, UiSize.Control.Small));
        metrics.MinimumSize(UiButtonContentLayout.Row).ShouldBe(new Vector2(UiSize.Control.Default, UiSize.Control.Default));
        metrics.MinimumSize(UiButtonContentLayout.Stacked).ShouldBe(new Vector2(UiSize.Control.Touch, UiSize.Control.Touch));
        metrics.BadgeMinimumSize.ShouldBe(UiSize.Widget.BadgeMinimumSize);
        metrics.BadgeOffset.ShouldBe(UiSize.Widget.BadgeOffset);
        metrics.SelectedStroke.ShouldBe(UiSize.Stroke.ButtonSelected);
    }

    [Fact]
    public void BadgePosition_AnchorsToTheControlCornerForEverySize()
    {
        var metrics = new UiButtonMetrics(48, 40, 32, 16, 4, 2);

        metrics.BadgePosition(metrics.MinimumSize(UiButtonContentLayout.RowCompact))
            .ShouldBe(new Vector2(20, -4));
        metrics.BadgePosition(metrics.MinimumSize(UiButtonContentLayout.Row))
            .ShouldBe(new Vector2(28, -4));
        metrics.BadgePosition(metrics.MinimumSize(UiButtonContentLayout.Stacked))
            .ShouldBe(new Vector2(36, -4));
        metrics.BadgePosition(new Vector2(120, 40))
            .ShouldBe(new Vector2(108, -4));
    }

    [Theory]
    [InlineData(UiButtonContentLayout.Row, 40)]
    [InlineData(UiButtonContentLayout.RowCompact, 32)]
    [InlineData(UiButtonContentLayout.Stacked, 48)]
    public void Metrics_LayoutDeterminesSize(UiButtonContentLayout layout, float expected)
    {
        new UiButtonMetrics(48, 40, 32, 16, 4, 2).MinimumSize(layout)
            .ShouldBe(new Vector2(expected, expected));
    }

    [Fact]
    public void Metrics_OnlyLayoutDeterminesIconSize()
    {
        UiButtonMetrics.IconSize(UiButtonContentLayout.Row).ShouldBe(UiIconSize.Standard);
        UiButtonMetrics.IconSize(UiButtonContentLayout.RowCompact).ShouldBe(UiIconSize.Standard);
        UiButtonMetrics.IconSize(UiButtonContentLayout.Stacked).ShouldBe(UiIconSize.Large);
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
        var metrics = new UiButtonMetrics(48, 40, 32, 16, 4, 2);
        foreach (var layout in new[]
                 {
                     UiButtonContentLayout.Row,
                     UiButtonContentLayout.RowCompact,
                     UiButtonContentLayout.Stacked,
                 })
        {
            var frame = new Rect2(Vector2.Zero, metrics.MinimumSize(layout));

            UiButtonMetrics.ProgressLayout(frame, 0.5f)
                .Fill
                .Size
                .ShouldBe(frame.Size);
        }
    }

    private static Color ResolveColor(ThemeFile palette, UiTokens.Color token) => palette.Color(token);
}
