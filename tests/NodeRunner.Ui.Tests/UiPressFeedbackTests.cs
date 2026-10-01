using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiPressFeedbackTests
{
    [Theory]
    [InlineData(BaseButton.DrawMode.Pressed, true)]
    [InlineData(BaseButton.DrawMode.HoverPressed, true)]
    [InlineData(BaseButton.DrawMode.Normal, false)]
    [InlineData(BaseButton.DrawMode.Hover, false)]
    [InlineData(BaseButton.DrawMode.Disabled, false)]
    public void Only_a_press_shows_the_tint(BaseButton.DrawMode mode, bool shows)
    {
        UiPressFeedback.Shows(mode).ShouldBe(shows);
    }

    [Theory]
    [InlineData(UiTokens.Color.Accent, false, UiTokens.Color.OnAccent)]
    [InlineData(UiTokens.Color.PanelRaised, false, UiTokens.Color.Accent)]
    [InlineData(UiTokens.Color.Transparent, false, UiTokens.Color.Accent)]
    [InlineData(UiTokens.Color.Panel, false, UiTokens.Color.Accent)]
    [InlineData(UiTokens.Color.PanelRaised, true, UiTokens.Color.Danger)]
    [InlineData(UiTokens.Color.Transparent, true, UiTokens.Color.Danger)]
    [InlineData(UiTokens.Color.Panel, true, UiTokens.Color.Danger)]
    public void The_tint_shows_on_every_fill_and_keeps_danger_red(UiTokens.Color fill, bool danger, UiTokens.Color tint)
    {
        UiPressFeedback.TintColorOver(fill, danger).ShouldBe(tint);
    }

    [Theory]
    [InlineData("ui/lib/UiButton.cs")]
    [InlineData("ui/lib/UiMenuActionItem.cs")]
    public void Buttons_and_menu_rows_share_the_feedback(string path)
    {
        var text = CSharpSources.Project.Single(source => source.Path == path).Tree.ToString();

        text.ShouldContain("UiPressFeedback.Shows(");
        text.ShouldNotContain("DrawMode.Hover");
    }
}
