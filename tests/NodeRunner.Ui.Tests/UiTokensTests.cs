using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiTokensTests
{
    [Fact]
    public void Neon_MapsEveryCanonicalColor()
    {
        var tokens = UiTokens.Neon;

        AssertColor(tokens.Background, 0x07, 0x0b, 0x14);
        AssertColor(tokens.Panel, 0x0d, 0x14, 0x24);
        AssertColor(tokens.PanelRaised, 0x13, 0x1c, 0x31);
        AssertColor(tokens.Line, 0x23, 0x30, 0x4d);
        AssertColor(tokens.LineStrong, 0x55, 0x73, 0xa6);
        AssertColor(tokens.Ink, 0xe6, 0xf1, 0xff);
        AssertColor(tokens.Muted, 0x8f, 0xa3, 0xc4);
        AssertColor(tokens.Accent, 0x19, 0xf0, 0xff);
        AssertColor(tokens.Edge, 0x19, 0xf0, 0xff, 0x59);
        AssertColor(tokens.AccentSoft, 0x19, 0xf0, 0xff, 0x1f);
        AssertColor(tokens.AccentGlow, 0x19, 0xf0, 0xff, 0x40);
        AssertColor(tokens.OnAccent, 0x04, 0x12, 0x1a);
        AssertColor(tokens.Halo, 0xff, 0xb3, 0x47);
        AssertColor(tokens.Danger, 0xff, 0x6b, 0x87);
        AssertColor(tokens.Scrim, 0x04, 0x08, 0x10, 0xbd);
        AssertColor(tokens.Output, 0xff, 0xe1, 0x4d);
        tokens.GlowRadius.ShouldBe(16);
        tokens.EffectsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Paper_MapsEveryCanonicalColor()
    {
        var tokens = UiTokens.Paper;

        AssertColor(tokens.Background, 0xf4, 0xf1, 0xea);
        AssertColor(tokens.Panel, 0xff, 0xff, 0xff);
        AssertColor(tokens.PanelRaised, 0xe9, 0xe5, 0xdb);
        AssertColor(tokens.Line, 0xcf, 0xc8, 0xb8);
        AssertColor(tokens.LineStrong, 0x7a, 0x74, 0x66);
        AssertColor(tokens.Ink, 0x1b, 0x1a, 0x17);
        AssertColor(tokens.Muted, 0x5a, 0x56, 0x48);
        AssertColor(tokens.Accent, 0x00, 0x6d, 0x77);
        AssertColor(tokens.Edge, 0xcf, 0xc8, 0xb8);
        AssertColor(tokens.AccentSoft, 0x00, 0x6d, 0x77, 0x1a);
        AssertColor(tokens.AccentGlow, 0x00, 0x6d, 0x77, 0x00);
        AssertColor(tokens.OnAccent, 0xff, 0xff, 0xff);
        AssertColor(tokens.Halo, 0xb4, 0x5f, 0x00);
        AssertColor(tokens.Danger, 0xb3, 0x26, 0x1e);
        AssertColor(tokens.Scrim, 0x1b, 0x1a, 0x17, 0x73);
        AssertColor(tokens.Output, 0x7a, 0x5c, 0x00);
        tokens.GlowRadius.ShouldBe(0);
        tokens.EffectsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Dimensions_MapEveryCanonicalValue()
    {
        var tokens = UiTokens.Neon;

        new[]
        {
            tokens.Space1, tokens.Space2, tokens.Space3, tokens.Space4, tokens.Space5,
        }.ShouldBe(new[] { 4f, 8f, 12f, 16f, 24f });
        new[]
        {
            tokens.ControlExtraSmall, tokens.ControlSmall, tokens.ControlHeight, tokens.TouchTarget,
        }.ShouldBe(new[] { 24f, 32f, 40f, 48f });
        new[]
        {
            tokens.BadgeMinimumSize, tokens.BadgeOffset, tokens.ButtonSelectedStroke,
        }.ShouldBe(new[] { 16f, 4f, 2f });
        new[]
        {
            tokens.IconSmall, tokens.Icon, tokens.IconLarge, tokens.IconExtraLarge,
        }.ShouldBe(new[] { 12f, 16f, 20f, 24f });
        new[]
        {
            tokens.RailWidth, tokens.SidePanelWidth, tokens.MenuWidth, tokens.DialogWidth,
            tokens.BrainWidth, tokens.CardWidth, tokens.TileWidth, tokens.WellWidth,
            tokens.SheetWidth, tokens.SheetWideWidth,
        }.ShouldBe(new[] { 56f, 176f, 200f, 300f, 460f, 326f, 156f, 250f, 720f, 880f });
        new[]
        {
            tokens.ScreenBodyHeight, tokens.StageHeight, tokens.ThumbnailHeight,
        }.ShouldBe(new[] { 312f, 170f, 100f });
        new[]
        {
            tokens.ColumnExtraSmallWidth, tokens.ColumnSmallWidth, tokens.ColumnMediumWidth,
            tokens.ColumnLargeWidth, tokens.ColumnExtraLargeWidth,
        }.ShouldBe(new[] { 40f, 52f, 76f, 96f, 128f });
        new[]
        {
            tokens.RadiusSmall, tokens.RadiusMedium, tokens.RadiusLarge, tokens.RadiusPill,
        }.ShouldBe(new[] { 4f, 8f, 12f, 999f });
        new[] { tokens.StrokeHair, tokens.StrokeBeam, tokens.StrokeSignal }
            .ShouldBe(new[] { 1f, 3f, 2f });
        new[]
        {
            tokens.SliderThumbDiameter, tokens.SliderTrackWidth, tokens.SliderMarkerHeight,
            tokens.SliderStepTickHeight, tokens.SliderDisabledDashLength,
            tokens.SliderSteppedHeight,
        }.ShouldBe(new[] { 18f, 4f, 16f, 10f, 4f, 60f });
        UiTokens.LogicalCanvasWidth.ShouldBe(640);
        UiTokens.LogicalCanvasHeight.ShouldBe(360);
    }

    [Fact]
    public void Typography_MapsEveryCanonicalStyle()
    {
        var tokens = UiTokens.Neon;

        AssertStyle(tokens.TitleText, UiTokens.FontFamily.Display, 28, 32, 700);
        AssertStyle(tokens.HeadingText, UiTokens.FontFamily.Display, 16, 20, 600);
        AssertStyle(tokens.SubheadingText, UiTokens.FontFamily.Display, 14, 18, 600);
        AssertStyle(tokens.StageText, UiTokens.FontFamily.Display, 11, 14, 600, 0.06f, true);
        AssertStyle(tokens.BodyText, UiTokens.FontFamily.Body, 13, 18, 400);
        AssertStyle(tokens.BodyStrongText, UiTokens.FontFamily.Body, 13, 18, 600);
        AssertStyle(tokens.SmallText, UiTokens.FontFamily.Body, 12, 16, 400);
        AssertStyle(tokens.SmallStrongText, UiTokens.FontFamily.Body, 12, 16, 600);
        AssertStyle(tokens.LabelText, UiTokens.FontFamily.Body, 12, 16, 600, 0.04f, true);
        AssertStyle(tokens.NoteText, UiTokens.FontFamily.Body, 11, 14, 400);
        AssertStyle(tokens.NoteStrongText, UiTokens.FontFamily.Body, 11, 14, 600);
        AssertStyle(tokens.CaptionText, UiTokens.FontFamily.Body, 10, 13, 500);
        AssertStyle(tokens.OverlineText, UiTokens.FontFamily.Body, 10, 13, 600, 0.06f, true);
        AssertStyle(tokens.ReadoutLargeText, UiTokens.FontFamily.Mono, 22, 24, 600);
        AssertStyle(tokens.ReadoutText, UiTokens.FontFamily.Mono, 13, 16, 500);
        AssertStyle(tokens.ReadoutMediumText, UiTokens.FontFamily.Mono, 12, 16, 500);
        AssertStyle(tokens.ReadoutSmallText, UiTokens.FontFamily.Mono, 10, 13, 500);
    }

    [Fact]
    public void Typography_UsesAFontAssetForEveryRequiredWeight()
    {
        var tokens = UiTokens.Neon;
        var styles = new[]
        {
            tokens.TitleText, tokens.HeadingText, tokens.SubheadingText, tokens.StageText,
            tokens.BodyText, tokens.BodyStrongText, tokens.SmallText, tokens.SmallStrongText,
            tokens.LabelText, tokens.NoteText, tokens.NoteStrongText, tokens.CaptionText,
            tokens.OverlineText, tokens.ReadoutLargeText, tokens.ReadoutText,
            tokens.ReadoutMediumText, tokens.ReadoutSmallText,
        };

        var fontPaths = styles.Select(UiTokens.FontPathFor).ToArray();
        fontPaths.ShouldBe(new[]
        {
            "res://assets/fonts/ChakraPetch/ChakraPetch-Bold.ttf",
            "res://assets/fonts/ChakraPetch/ChakraPetch-SemiBold.ttf",
            "res://assets/fonts/ChakraPetch/ChakraPetch-SemiBold.ttf",
            "res://assets/fonts/ChakraPetch/ChakraPetch-SemiBold.ttf",
            "res://assets/fonts/Barlow/Barlow-Regular.ttf",
            "res://assets/fonts/Barlow/Barlow-SemiBold.ttf",
            "res://assets/fonts/Barlow/Barlow-Regular.ttf",
            "res://assets/fonts/Barlow/Barlow-SemiBold.ttf",
            "res://assets/fonts/Barlow/Barlow-SemiBold.ttf",
            "res://assets/fonts/Barlow/Barlow-Regular.ttf",
            "res://assets/fonts/Barlow/Barlow-SemiBold.ttf",
            "res://assets/fonts/Barlow/Barlow-Medium.ttf",
            "res://assets/fonts/Barlow/Barlow-SemiBold.ttf",
            "res://assets/fonts/JetBrainsMono/JetBrainsMono-SemiBold.ttf",
            "res://assets/fonts/JetBrainsMono/JetBrainsMono-Medium.ttf",
            "res://assets/fonts/JetBrainsMono/JetBrainsMono-Medium.ttf",
            "res://assets/fonts/JetBrainsMono/JetBrainsMono-Medium.ttf",
        });

        var projectRoot = Path.Combine(FindRepositoryRoot(), "project");
        foreach (var fontPath in fontPaths.Distinct())
        {
            var assetPath = Path.Combine(projectRoot, fontPath["res://".Length..]);
            File.Exists(assetPath).ShouldBeTrue($"Missing font asset: {fontPath}");
            File.Exists($"{assetPath}.import").ShouldBeTrue($"Missing Godot import metadata: {fontPath}.import");
        }
    }

    [Fact]
    public void EffectsLite_PreservesTokensAndRemovesGlow()
    {
        var tokens = UiTokens.Neon.WithEffects(false);

        tokens.Background.ShouldBe(UiTokens.Neon.Background);
        tokens.Output.ShouldBe(UiTokens.Neon.Output);
        tokens.TitleText.ShouldBe(UiTokens.Neon.TitleText);
        tokens.SidePanelWidth.ShouldBe(UiTokens.Neon.SidePanelWidth);
        tokens.AccentGlow.ShouldBe(Colors.Transparent);
        tokens.GlowRadius.ShouldBe(0);
        tokens.EffectsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void ControlMetrics_KeepDistinctPaddingAndFocusContracts()
    {
        var tokens = UiTokens.Neon;

        UiSpacing.ControlHorizontalPadding(tokens).ShouldBe(16);
        UiSpacing.SegmentedControlHorizontalPadding(tokens).ShouldBe(12);
    }

    private static void AssertColor(Color actual, byte red, byte green, byte blue, byte alpha = 0xff)
    {
        actual.R8.ShouldBe(red);
        actual.G8.ShouldBe(green);
        actual.B8.ShouldBe(blue);
        actual.A8.ShouldBe(alpha);
    }

    private static void AssertStyle(
        UiTokens.TextStyle actual,
        UiTokens.FontFamily family,
        float size,
        float lineHeight,
        int weight,
        float letterSpacing = 0,
        bool uppercase = false)
    {
        actual.Family.ShouldBe(family);
        actual.FontSize.ShouldBe(size);
        actual.LineHeight.ShouldBe(lineHeight);
        actual.FontWeight.ShouldBe(weight);
        actual.LetterSpacing.ShouldBe(letterSpacing);
        actual.Uppercase.ShouldBe(uppercase);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NodeRunner.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the Node Runner repository root.");
    }
}
