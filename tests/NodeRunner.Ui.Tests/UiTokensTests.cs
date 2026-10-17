using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>The palettes, sizes and type scale match the reference design's tokens.</summary>
public sealed class UiTokensTests
{
    [Fact]
    public void StrokeSizeTokens_ResolveToTheSameNamedUiSizeConstant()
    {
        foreach (var token in Enum.GetValues<UiTokens.Size.Stroke>())
        {
            var constant = typeof(UiSize.Stroke).GetField(token.ToString());

            constant.ShouldNotBeNull($"UiSize.Stroke has no constant for {token}");
            UiThemeLookup.Size(token).ShouldBe(Convert.ToSingle(constant.GetValue(null)), $"{token}");
        }
    }

    // Background, Panel, PanelRaised, Line, LineStrong, Ink, Muted, Accent, Edge, OnAccent, Halo, Danger, Scrim, Output, Detail.
    [Theory]
    [InlineData(UiTokenType.Neon, true, 0x1f, "070b14ff 0d1424ff 131c31ff 23304dff 5573a6ff e6f1ffff 8fa3c4ff 19f0ffff 1a7f79ff 04121aff ffb347ff ff6b87ff 040810bd ffe14dff b58cffff")]
    [InlineData(UiTokenType.Paper, false, 0x1a, "f4f1eaff ffffffff e9e5dbff cfc8b8ff 7a7466ff 1b1a17ff 5a5648ff 006d77ff cfc8b8ff ffffffff b45f00ff b3261eff 1b1a1773 7a5c00ff 6b3fc0ff")]
    public void Palette_MapsEveryCanonicalColor(UiTokenType type, bool effects, int softAlpha, string colors)
    {
        var theme = ThemeFile.For(type);
        UiTokens.Color[] order =
        [
            UiTokens.Color.Background, UiTokens.Color.Panel, UiTokens.Color.PanelRaised, UiTokens.Color.Line,
            UiTokens.Color.LineStrong, UiTokens.Color.Ink, UiTokens.Color.Muted, UiTokens.Color.Accent, UiTokens.Color.Edge,
            UiTokens.Color.OnAccent, UiTokens.Color.Halo, UiTokens.Color.Danger, UiTokens.Color.Scrim, UiTokens.Color.Output,
            UiTokens.Color.Detail,
        ];

        string.Join(' ', order.Select(color => theme.Color(color).ToHtml())).ShouldBe(colors);
        theme.Constant(UiThemes.TokenType, UiTokens.Name(UiTokens.Alpha.Soft)).ShouldBe(softAlpha);
        theme.Constant(UiThemes.TokenType, UiTokens.Name(UiTokens.Alpha.Shadow)).ShouldBe(0x52);
        theme.Flag(UiTokens.Flag.EffectsEnabled).ShouldBe(effects);
    }

    [Fact]
    public void Dimensions_MapEveryCanonicalValue()
    {
        new[] { UiSize.Space.S1, UiSize.Space.S2, UiSize.Space.S3, UiSize.Space.S4, UiSize.Space.S5 }.ShouldBe([4, 8, 12, 16, 24]);
        new[] { UiSize.Control.ExtraSmall, UiSize.Control.Small, UiSize.Control.Default, UiSize.Control.Touch }.ShouldBe([24, 32, 40, 48]);
        new[] { UiSize.Icon.Small, UiSize.Icon.Default, UiSize.Icon.Large, UiSize.Icon.ExtraLarge }.ShouldBe([12, 16, 20, 24]);
        new[] { UiSize.Radius.Small, UiSize.Radius.Medium, UiSize.Radius.Large, UiSize.Radius.Pill }.ShouldBe([4, 8, 12, 999]);
        new[] { UiSize.Stroke.Hair, UiSize.Stroke.Beam, UiSize.Stroke.Signal }.ShouldBe([1, 3, 2]);
        // Plain constants keep the fraction that a Godot theme constant would round away.
        UiSize.Stroke.Number.ShouldBe(1.5f);
        new[] { UiLayout.CanvasWidth, UiLayout.CanvasHeight, UiLayout.SidePanelWidth, UiLayout.MenuWidth, UiLayout.ColumnSmallWidth }
            .ShouldBe([640, 360, 176, 200, 52]);
    }

    // Letter spacing 0.04em-0.06em rounds up to 1px at these sizes.
    [Fact]
    public void Typography_MapsEveryCanonicalStyle()
    {
        var theme = ThemeFile.For(UiTokenType.Neon);
        var styles = Enum.GetValues<UiTokens.Typography>().Select(typography =>
        {
            var variation = UiTokens.Variation(typography);
            var path = theme.FontPath(variation);
            path.ShouldStartWith("res://assets/fonts/");
            var font = Path.ChangeExtension(path["res://assets/fonts/".Length..], null);
            var letterCase = UiTokens.IsUppercase(typography) ? " upper" : string.Empty;
            return $"{typography}={font} {theme.FontSize(variation)} {theme.SpacingGlyph(variation)}{letterCase}";
        });

        styles.ShouldBe(
        [
            "Title=ChakraPetch/ChakraPetch-Bold 28 0", "Heading=ChakraPetch/ChakraPetch-SemiBold 16 0",
            "Subheading=ChakraPetch/ChakraPetch-SemiBold 14 0", "Stage=ChakraPetch/ChakraPetch-SemiBold 11 1 upper",
            "Body=Barlow/Barlow-Regular 13 0", "BodyStrong=Barlow/Barlow-SemiBold 13 0",
            "Small=Barlow/Barlow-Regular 12 0", "SmallStrong=Barlow/Barlow-SemiBold 12 0", "Label=Barlow/Barlow-SemiBold 12 1 upper",
            "Note=Barlow/Barlow-Regular 11 0", "NoteStrong=Barlow/Barlow-SemiBold 11 0", "Caption=Barlow/Barlow-Medium 10 0",
            "Overline=Barlow/Barlow-SemiBold 10 1 upper", "ReadoutLarge=JetBrainsMono/JetBrainsMono-SemiBold 22 0",
            "Readout=JetBrainsMono/JetBrainsMono-Medium 13 0", "ReadoutMedium=JetBrainsMono/JetBrainsMono-Medium 12 0",
            "ReadoutSmall=JetBrainsMono/JetBrainsMono-Medium 10 0",
        ]);
    }

    [Fact]
    public void Typography_FontAssetsExistAndAreImported()
    {
        var theme = ThemeFile.For(UiTokenType.Neon);
        var missing = Enum.GetValues<UiTokens.Typography>()
            .Select(typography => theme.FontPath(UiTokens.Variation(typography)))
            .Distinct()
            .Select(fontPath => Path.Combine(ThemeFile.ProjectRoot, fontPath["res://".Length..]))
            .SelectMany(asset => new[] { asset, asset + ".import" })
            .Where(path => !File.Exists(path));

        missing.ShouldBeEmpty("Every theme font and its Godot import metadata exist.");
    }
}
