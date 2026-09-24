using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiLabelTests
{
    [Fact]
    public void Presets_ResolveCanonicalTypographyInInspectorOrder()
    {
        foreach (var tokens in new[] { UiTokens.Neon, UiTokens.Paper, UiTokens.Neon.WithEffects(false) })
        {
            Enum.GetValues<UiLabel.TextPreset>().Select(preset => UiLabel.ResolveStyle(preset, tokens))
                .ShouldBe(new[]
                {
                    tokens.TitleText, tokens.HeadingText, tokens.SubheadingText, tokens.StageText,
                    tokens.BodyText, tokens.BodyStrongText, tokens.SmallText, tokens.SmallStrongText,
                    tokens.LabelText, tokens.NoteText, tokens.NoteStrongText, tokens.CaptionText,
                    tokens.OverlineText, tokens.ReadoutLargeText, tokens.ReadoutText,
                    tokens.ReadoutMediumText, tokens.ReadoutSmallText,
                });
        }
    }

    [Fact]
    public void ResolveStyle_UsesSuppliedTokensRatherThanDuplicatedDefaults()
    {
        var tokens = new UiTokens { BodyText = new(UiTokens.FontFamily.Body, 15, 20, 500) };

        UiLabel.ResolveStyle(UiLabel.TextPreset.Body, tokens).ShouldBe(tokens.BodyText);
    }

    [Fact]
    public void ResolveStyle_RejectsUnknownPreset()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => UiLabel.ResolveStyle((UiLabel.TextPreset)999, UiTokens.Neon));
    }
}
