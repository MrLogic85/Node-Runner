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
        var size = UiChoiceStyle.IndicatorSize(UiTokens.Neon, isSwitch);

        size.ShouldBe(new Vector2(width, 24));
    }

    [Fact]
    public void SwitchThumb_MovesBetweenReferenceInsetsWithoutChangingSize()
    {
        var tokens = UiTokens.Neon;
        var track = new Rect2(Vector2.Zero, UiChoiceStyle.IndicatorSize(tokens, true));

        UiChoiceStyle.ThumbCenter(tokens, track, false).ShouldBe(new Vector2(13, 12));
        UiChoiceStyle.ThumbCenter(tokens, track, true).ShouldBe(new Vector2(31, 12));
        tokens.Icon.ShouldBe(16);
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
        var tokens = paper ? UiTokens.Paper : UiTokens.Neon;

        foreach (var isSwitch in new[] { true, false })
        {
            var off = UiChoiceStyle.Resolve(tokens, isSwitch, false);
            var on = UiChoiceStyle.Resolve(tokens, isSwitch, true);

            off.Background.ShouldBe(Colors.Transparent);
            off.Border.ShouldBe(tokens.LineStrong);
            on.Background.ShouldBe(isSwitch ? tokens.AccentSoft : tokens.Accent);
            on.Border.ShouldBe(tokens.Accent);
            off.Mark.ShouldBe(isSwitch ? tokens.LineStrong : tokens.OnAccent);
            on.Mark.ShouldBe(isSwitch ? tokens.Accent : tokens.OnAccent);
        }
    }
}
