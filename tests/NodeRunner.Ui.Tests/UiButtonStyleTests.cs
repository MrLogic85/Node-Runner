using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiButtonStyleTests
{
    private static readonly ThemeFile _neon = ThemeFile.For(UiTokenType.Neon);
    private static readonly UiButtonStyle[] _styles = [UiButtonStyle.Primary, UiButtonStyle.Secondary, UiButtonStyle.Tertiary, UiButtonStyle.Flat];

    private static Color Neon(UiTokens.Color token) => _neon.Color(token);

    [Fact]
    public void Styles_ResolveCanonicalRestAndSelectedColors()
    {
        UiButtonStyle.Primary.Resolve(Neon).ShouldBe(new UiResolvedButtonStyle(
            Neon(UiTokens.Color.Accent), Neon(UiTokens.Color.Accent), Neon(UiTokens.Color.OnAccent), Neon(UiTokens.Color.Halo), Neon(UiTokens.Color.Accent)));
        UiButtonStyle.Secondary.Resolve(Neon).ShouldBe(new UiResolvedButtonStyle(
            Neon(UiTokens.Color.PanelRaised), Neon(UiTokens.Color.LineStrong), Neon(UiTokens.Color.Ink), Neon(UiTokens.Color.Accent), null));
        UiButtonStyle.Tertiary.Resolve(Neon).ShouldBe(new UiResolvedButtonStyle(
            Neon(UiTokens.Color.PanelRaised), Neon(UiTokens.Color.Danger), Neon(UiTokens.Color.Danger), Neon(UiTokens.Color.Danger), null));
        UiButtonStyle.Flat.Resolve(Neon).ShouldBe(new UiResolvedButtonStyle(
            Colors.Transparent, Colors.Transparent, Neon(UiTokens.Color.Ink), Neon(UiTokens.Color.Ink), null));
    }

    /// <summary>Selected uses each style's selected color in place of the rest glow; only Primary glows at rest; disabled never glows.</summary>
    [Fact]
    public void Glow_follows_selected_rest_and_disabled()
    {
        foreach (var style in _styles)
        {
            var resolved = style.Resolve(Neon);
            style.BorderFor(Neon, selected: true).ShouldBe(resolved.Selected);
            style.GlowBaseFor(Neon, selected: true).ShouldBe(resolved.Selected);
            style.GlowBaseFor(Neon, selected: false).ShouldBe(style == UiButtonStyle.Primary ? Neon(UiTokens.Color.Accent) : null);
            style.GlowBaseFor(Neon, selected: false, enabled: false).ShouldBeNull();
            style.GlowBaseFor(Neon, selected: true, enabled: false).ShouldBeNull();
        }

        var halo = Neon(UiTokens.Color.Halo);
        UiGlow.FromBase(halo, enabled: true).ShouldBe(halo.ScaleAlpha(UiGlow.Opacity));
        UiGlow.FromBase(halo, ThemeFile.For(UiTokenType.Paper).Flag(UiTokens.Flag.EffectsEnabled)).ShouldBe(Colors.Transparent);
    }

    [Theory]
    [InlineData(UiButtonContentLayout.Row, 40)]
    [InlineData(UiButtonContentLayout.RowCompact, 32)]
    [InlineData(UiButtonContentLayout.Stacked, 48)]
    public void Metrics_LayoutDeterminesSize(UiButtonContentLayout layout, float expected)
    {
        var metrics = new UiButtonMetrics(48, 40, 32, 16, 4, 2);

        metrics.MinimumSize(layout).ShouldBe(new Vector2(expected, expected));
        // The badge anchors to the control's top-right corner.
        metrics.BadgePosition(metrics.MinimumSize(layout)).ShouldBe(new Vector2(expected - 12, -4));
    }

    [Theory]
    [InlineData(UiButtonContentLayout.Row, true, UiIconSize.Standard)]
    [InlineData(UiButtonContentLayout.RowCompact, false, UiIconSize.Large)]
    [InlineData(UiButtonContentLayout.Stacked, true, UiIconSize.Large)]
    public void Metrics_LayoutAndLabelDetermineIconSize(UiButtonContentLayout layout, bool hasLabel, UiIconSize expected) =>
        UiButtonMetrics.IconSize(layout, hasLabel).ShouldBe(expected);
}
