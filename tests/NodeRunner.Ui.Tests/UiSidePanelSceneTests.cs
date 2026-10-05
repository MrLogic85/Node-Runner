namespace NodeRunner.Ui.Tests;

/// <summary>The side panel's own scene (#321, #801).</summary>
public sealed class UiSidePanelSceneTests
{
    private static readonly SceneNodes.SceneNode[] _panel = [.. SceneNodes.InScene("ui/UiSidePanel.tscn")];

    [Fact]
    public void Content_ScrollsUnderAFixedHeader_ClippedAtThePanelsEdges()
    {
        Children("/SidePanelExpanded").ShouldBe(["SidePanelHeaderInset", "SidePanelScroll"]);
        var scroll = _panel.Single(node => node.Name == "SidePanelScroll");
        scroll.Type.ShouldBe("ScrollContainer");
        scroll.Node.Body.ShouldContain("horizontal_scroll_mode = 0");
        scroll.Node.Body.ShouldContain("vertical_scroll_mode = 3");
        Children("/SidePanelScroll").ShouldBe(["SidePanelContentInset"]);
        Children("/SidePanelContentInset").ShouldBe(["SidePanelContent"]);

        // The scroll reaches the panel's sides and bottom; the padding is inside it, so a slider
        // thumb's glow or a button's edge is clipped by the panel, not short of it.
        var padding = _panel.Single(node => node.Name == "SidePanelPadding").Node.Body;
        padding.ShouldNotContain("margin_left");
        padding.ShouldNotContain("margin_right");
        padding.ShouldNotContain("margin_bottom");
        var inset = _panel.Single(node => node.Name == "SidePanelContentInset").Node.Body;
        inset.ShouldContain("margin_left = 12");
        inset.ShouldContain("margin_right = 12");
        inset.ShouldContain("margin_bottom = 8");
    }

    private static string[] Children(string parent) =>
        [.. _panel.Where(node => node.Parent?.EndsWith(parent, StringComparison.Ordinal) == true).Select(node => node.Name)];
}
