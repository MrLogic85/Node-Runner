using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiChoiceStyleTests
{
    [Theory]
    [InlineData(true, 44)]
    [InlineData(false, 22)]
    public void Indicators_MatchNativeThemeTextureSize(bool isSwitch, float width)
    {
        var size = UiChoiceStyle.IndicatorSize(isSwitch);

        size.ShouldBe(new Vector2(width, 24));
    }

    [Fact]
    public void SwitchThumb_MovesBetweenReferenceInsetsWithoutChangingSize()
    {
        var track = new Rect2(Vector2.Zero, UiChoiceStyle.IndicatorSize(true));

        UiChoiceStyle.ThumbCenter(track, false).ShouldBe(new Vector2(13, 12));
        UiChoiceStyle.ThumbCenter(track, true).ShouldBe(new Vector2(31, 12));
        UiSize.Icon.Default.ShouldBe(16);
    }

    [Fact]
    public void DisabledOpacity_MatchesWholeRowReferenceDimming()
    {
        UiChoiceStyle.DisabledOpacity.ShouldBe(0.5f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChoiceColors_UseSemanticTokensInBothThemes(bool paper)
    {
        var tokens = paper ? ThemeFile.For(UiTokenType.Paper) : ThemeFile.For(UiTokenType.Neon);

        foreach (var isSwitch in new[] { true, false })
        {
            var off = UiChoiceStyle.Resolve(isSwitch, false, tokens.Color(UiTokens.Color.Accent), tokens.Color(UiTokens.Color.LineStrong), tokens.Color(UiTokens.Color.OnAccent), tokens.Color(UiTokens.Color.Accent).WithAlpha(tokens.Alpha(UiTokens.Alpha.Soft)));
            var on = UiChoiceStyle.Resolve(isSwitch, true, tokens.Color(UiTokens.Color.Accent), tokens.Color(UiTokens.Color.LineStrong), tokens.Color(UiTokens.Color.OnAccent), tokens.Color(UiTokens.Color.Accent).WithAlpha(tokens.Alpha(UiTokens.Alpha.Soft)));

            off.Background.ShouldBe(Colors.Transparent);
            off.Border.ShouldBe(tokens.Color(UiTokens.Color.LineStrong));
            on.Background.ShouldBe(
                isSwitch
                    ? tokens.Color(UiTokens.Color.Accent).WithAlpha(tokens.Alpha(UiTokens.Alpha.Soft))
                    : tokens.Color(UiTokens.Color.Accent));
            on.Border.ShouldBe(tokens.Color(UiTokens.Color.Accent));
            off.Mark.ShouldBe(isSwitch ? tokens.Color(UiTokens.Color.LineStrong) : tokens.Color(UiTokens.Color.OnAccent));
            on.Mark.ShouldBe(isSwitch ? tokens.Color(UiTokens.Color.Accent) : tokens.Color(UiTokens.Color.OnAccent));
        }
    }
}
