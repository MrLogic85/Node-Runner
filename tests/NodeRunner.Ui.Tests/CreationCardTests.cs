namespace NodeRunner.Ui.Tests;

/// <summary>
/// Guards #770: a creation card's thumbnail shows the creature with the shared parts in its own
/// world, which renders only when the thumbnail refreshes (update mode Disabled until
/// <c>UiWorldView.RequestRender</c>) and lets taps through to the card.
/// </summary>
public sealed class CreationCardTests
{
    private const string _thumbnail = "Frame/Stack/ThumbnailSlot/Thumbnail";

    private static readonly SceneNodes.SceneNode[] _card = [.. SceneNodes.InScene("widgets/CreationCard.tscn")];

    [Fact]
    public void Thumbnail_world_scales_with_the_ui_and_lets_taps_through()
    {
        var view = _card.Single(node => node.Name == "View" && node.Parent == _thumbnail);
        view.Instance.ShouldBe("res://scenes/ui/UiWorldView.tscn");
        view.Node.Body.ShouldContain("\nScalesWithUi = true");
        view.Node.Body.ShouldContain("\nmouse_filter = 2");
    }

    [Fact]
    public void Thumbnail_world_renders_only_on_request()
    {
        var viewport = _card.Single(node => node.Name == "WorldViewport" && node.Parent == $"{_thumbnail}/View");
        viewport.Node.Body.ShouldContain("\nrender_target_update_mode = 0");
    }

    [Fact]
    public void Thumbnail_parts_live_in_its_world()
    {
        var parts = _card.Single(node => node.Script == "res://src/ui/widgets/CreatureParts.cs");
        parts.Name.ShouldBe("Parts");
        parts.Parent.ShouldBe($"{_thumbnail}/View/WorldViewport");
    }
}
