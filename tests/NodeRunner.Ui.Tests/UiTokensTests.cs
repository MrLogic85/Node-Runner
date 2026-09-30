using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

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

    [Fact]
    public void Neon_MapsEveryCanonicalColor()
    {
        var tokens = ThemeFile.For(UiTokenType.Neon);

        AssertColor(tokens.Color(UiTokens.Color.Background), 0x07, 0x0b, 0x14);
        AssertColor(tokens.Color(UiTokens.Color.Panel), 0x0d, 0x14, 0x24);
        AssertColor(tokens.Color(UiTokens.Color.PanelRaised), 0x13, 0x1c, 0x31);
        AssertColor(tokens.Color(UiTokens.Color.Line), 0x23, 0x30, 0x4d);
        AssertColor(tokens.Color(UiTokens.Color.LineStrong), 0x55, 0x73, 0xa6);
        AssertColor(tokens.Color(UiTokens.Color.Ink), 0xe6, 0xf1, 0xff);
        AssertColor(tokens.Color(UiTokens.Color.Muted), 0x8f, 0xa3, 0xc4);
        AssertColor(tokens.Color(UiTokens.Color.Accent), 0x19, 0xf0, 0xff);
        AssertColor(tokens.Color(UiTokens.Color.Edge), 0x1a, 0x7f, 0x79);
        tokens.Constant(UiThemes.TokenType, UiTokens.Name(UiTokens.Alpha.Soft)).ShouldBe(0x1f);
        AssertColor(tokens.Color(UiTokens.Color.OnAccent), 0x04, 0x12, 0x1a);
        AssertColor(tokens.Color(UiTokens.Color.Halo), 0xff, 0xb3, 0x47);
        AssertColor(tokens.Color(UiTokens.Color.Danger), 0xff, 0x6b, 0x87);
        AssertColor(tokens.Color(UiTokens.Color.Scrim), 0x04, 0x08, 0x10, 0xbd);
        AssertColor(tokens.Color(UiTokens.Color.Output), 0xff, 0xe1, 0x4d);
        tokens.Flag(UiTokens.Flag.EffectsEnabled).ShouldBeTrue();
    }

    [Fact]
    public void Paper_MapsEveryCanonicalColor()
    {
        var tokens = ThemeFile.For(UiTokenType.Paper);

        AssertColor(tokens.Color(UiTokens.Color.Background), 0xf4, 0xf1, 0xea);
        AssertColor(tokens.Color(UiTokens.Color.Panel), 0xff, 0xff, 0xff);
        AssertColor(tokens.Color(UiTokens.Color.PanelRaised), 0xe9, 0xe5, 0xdb);
        AssertColor(tokens.Color(UiTokens.Color.Line), 0xcf, 0xc8, 0xb8);
        AssertColor(tokens.Color(UiTokens.Color.LineStrong), 0x7a, 0x74, 0x66);
        AssertColor(tokens.Color(UiTokens.Color.Ink), 0x1b, 0x1a, 0x17);
        AssertColor(tokens.Color(UiTokens.Color.Muted), 0x5a, 0x56, 0x48);
        AssertColor(tokens.Color(UiTokens.Color.Accent), 0x00, 0x6d, 0x77);
        AssertColor(tokens.Color(UiTokens.Color.Edge), 0xcf, 0xc8, 0xb8);
        tokens.Constant(UiThemes.TokenType, UiTokens.Name(UiTokens.Alpha.Soft)).ShouldBe(0x1a);
        AssertColor(tokens.Color(UiTokens.Color.OnAccent), 0xff, 0xff, 0xff);
        AssertColor(tokens.Color(UiTokens.Color.Halo), 0xb4, 0x5f, 0x00);
        AssertColor(tokens.Color(UiTokens.Color.Danger), 0xb3, 0x26, 0x1e);
        AssertColor(tokens.Color(UiTokens.Color.Scrim), 0x1b, 0x1a, 0x17, 0x73);
        AssertColor(tokens.Color(UiTokens.Color.Output), 0x7a, 0x5c, 0x00);
        tokens.Flag(UiTokens.Flag.EffectsEnabled).ShouldBeFalse();
    }

    [Fact]
    public void Dimensions_MapEveryCanonicalValue()
    {
        new[]
        {
            UiSize.Space.S1, UiSize.Space.S2, UiSize.Space.S3, UiSize.Space.S4, UiSize.Space.S5,
        }.ShouldBe(new[] { 4, 8, 12, 16, 24 });
        new[]
        {
            UiSize.Control.ExtraSmall, UiSize.Control.Small, UiSize.Control.Default, UiSize.Control.Touch,
        }.ShouldBe(new[] { 24, 32, 40, 48 });
        new[]
        {
            UiSize.Widget.BadgeMinimumSize, UiSize.Widget.BadgeOffset, UiSize.Stroke.ButtonSelected,
        }.ShouldBe(new[] { 16, 4, 2 });
        new[]
        {
            UiSize.Icon.Small, UiSize.Icon.Default, UiSize.Icon.Large, UiSize.Icon.ExtraLarge,
        }.ShouldBe(new[] { 12, 16, 20, 24 });
        new[]
        {
            UiLayout.SidePanelWidth, UiLayout.MenuWidth, UiLayout.SidePanelTabWidth,
        }.ShouldBe(new[] { 176, 200, 28 });
        UiLayout.ColumnSmallWidth.ShouldBe(52);
        new[]
        {
            UiSize.Radius.Small, UiSize.Radius.Medium, UiSize.Radius.Large, UiSize.Radius.Pill,
        }.ShouldBe(new[] { 4, 8, 12, 999 });
        new[] { UiSize.Stroke.Hair, UiSize.Stroke.Beam, UiSize.Stroke.Signal }
            .ShouldBe(new[] { 1, 3, 2 });
        new[]
        {
            UiSize.Widget.SliderThumbDiameter, UiSize.Widget.SliderTrackWidth, UiSize.Widget.SliderMarkerHeight,
            UiSize.Widget.SliderStepTickHeight, UiSize.Widget.SliderDisabledDashLength,
            UiSize.Widget.SliderSteppedHeight,
        }.ShouldBe(new[] { 18, 4, 16, 10, 4, 60 });
        UiLayout.CanvasWidth.ShouldBe(640);
        UiLayout.CanvasHeight.ShouldBe(360);
    }

    [Fact]
    public void Typography_MapsEveryCanonicalStyle()
    {
        const string chakraBold = "res://assets/fonts/ChakraPetch/ChakraPetch-Bold.ttf";
        const string chakraSemiBold = "res://assets/fonts/ChakraPetch/ChakraPetch-SemiBold.ttf";
        const string barlowRegular = "res://assets/fonts/Barlow/Barlow-Regular.ttf";
        const string barlowMedium = "res://assets/fonts/Barlow/Barlow-Medium.ttf";
        const string barlowSemiBold = "res://assets/fonts/Barlow/Barlow-SemiBold.ttf";
        const string monoMedium = "res://assets/fonts/JetBrainsMono/JetBrainsMono-Medium.ttf";
        const string monoSemiBold = "res://assets/fonts/JetBrainsMono/JetBrainsMono-SemiBold.ttf";

        // Letter spacing 0.04em-0.06em rounds up to 1px at these sizes.
        AssertStyle(UiTokens.Typography.Title, chakraBold, 28);
        AssertStyle(UiTokens.Typography.Heading, chakraSemiBold, 16);
        AssertStyle(UiTokens.Typography.Subheading, chakraSemiBold, 14);
        AssertStyle(UiTokens.Typography.Stage, chakraSemiBold, 11, letterSpacing: 1);
        AssertStyle(UiTokens.Typography.Body, barlowRegular, 13);
        AssertStyle(UiTokens.Typography.BodyStrong, barlowSemiBold, 13);
        AssertStyle(UiTokens.Typography.Small, barlowRegular, 12);
        AssertStyle(UiTokens.Typography.SmallStrong, barlowSemiBold, 12);
        AssertStyle(UiTokens.Typography.Label, barlowSemiBold, 12, letterSpacing: 1);
        AssertStyle(UiTokens.Typography.Note, barlowRegular, 11);
        AssertStyle(UiTokens.Typography.NoteStrong, barlowSemiBold, 11);
        AssertStyle(UiTokens.Typography.Caption, barlowMedium, 10);
        AssertStyle(UiTokens.Typography.Overline, barlowSemiBold, 10, letterSpacing: 1);
        AssertStyle(UiTokens.Typography.ReadoutLarge, monoSemiBold, 22);
        AssertStyle(UiTokens.Typography.Readout, monoMedium, 13);
        AssertStyle(UiTokens.Typography.ReadoutMedium, monoMedium, 12);
        AssertStyle(UiTokens.Typography.ReadoutSmall, monoMedium, 10);
    }

    [Fact]
    public void Typography_FontAssetsExistAndAreImported()
    {
        var theme = ThemeFile.For(UiTokenType.Neon);
        var fontPaths = Enum.GetValues<UiTokens.Typography>()
            .Select(typography => theme.FontPath(UiTokens.Variation(typography)))
            .Distinct();

        foreach (var fontPath in fontPaths)
        {
            var assetPath = Path.Combine(ThemeFile.ProjectRoot, fontPath["res://".Length..]);
            File.Exists(assetPath).ShouldBeTrue($"Missing font asset: {fontPath}");
            File.Exists($"{assetPath}.import").ShouldBeTrue($"Missing Godot import metadata: {fontPath}.import");
        }
    }

    [Fact]
    public void ControlMetrics_KeepDistinctPaddingAndFocusContracts()
    {
        UiSize.Space.S4.ShouldBe(16);
        UiSize.Space.S3.ShouldBe(12);
    }

    private static void AssertColor(Color actual, byte red, byte green, byte blue, byte alpha = 0xff)
    {
        actual.R8.ShouldBe(red);
        actual.G8.ShouldBe(green);
        actual.B8.ShouldBe(blue);
        actual.A8.ShouldBe(alpha);
    }

    private static void AssertStyle(UiTokens.Typography typography, string fontPath, int size, int letterSpacing = 0)
    {
        var theme = ThemeFile.For(UiTokenType.Neon);
        var variation = UiTokens.Variation(typography);
        theme.FontPath(variation).ShouldBe(fontPath, variation);
        theme.FontSize(variation).ShouldBe(size, variation);
        theme.SpacingGlyph(variation).ShouldBe(letterSpacing, variation);
    }
}
