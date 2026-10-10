namespace NodeRunner.Ui.Tests;

/// <summary><c>UiPickList.ShowInfoUnder</c> moves its Info line between its rows, so Info must be a direct child.</summary>
public sealed class UiPickListTests
{
    [Fact]
    public void EveryPickList_PointsInfoAtOneOfItsOwnChildren()
    {
        var lists = SceneNodes.Files()
            .SelectMany(file => SceneNodes.Parse(file.Scene, file.Text).ToList() is var nodes
                ? nodes.Where(node => node.Script?.EndsWith("/UiPickList.cs", StringComparison.Ordinal) == true)
                    .Select(list => (List: list, Nodes: nodes))
                : [])
            .ToList();

        lists.ShouldNotBeEmpty();
        lists.ShouldAllBe(
            entry => entry.Nodes.Any(node => node.Name == InfoOf(entry.List) && node.Parent == PathOf(entry.List)),
            "Set a UiPickList's Info to a node directly under the list.");
    }

    private static string? InfoOf(SceneNodes.SceneNode list) =>
        System.Text.RegularExpressions.Regex.Match(list.Node.Body, "\nInfo = NodePath\\(\"(?<name>[^\"/]+)\"\\)") is { Success: true } match
            ? match.Groups["name"].Value
            : null;

    private static string PathOf(SceneNodes.SceneNode node) => node.Parent switch
    {
        null => ".",
        "." => node.Name,
        var parent => $"{parent}/{node.Name}",
    };
}
