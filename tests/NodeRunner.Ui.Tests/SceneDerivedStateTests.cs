namespace NodeRunner.Ui.Tests;

/// <summary>
/// Saved scenes hold only what their author set (#309). Styling comes from the Theme or from
/// component code, and each component keeps the values it derives out of the scene through
/// <c>UiUnsavedState</c>; a baked value would pin one theme's look or a stale computed size.
/// </summary>
public sealed class SceneDerivedStateTests
{
    // Component script → properties its code derives on its own node.
    private static readonly Dictionary<string, string[]> _derived = new()
    {
        ["UiButton.cs"] = ["custom_minimum_size"],
        ["UiSidePanel.cs"] = ["custom_minimum_size"],
        ["UiCard.cs"] = ["clip_children"],
        ["UiStageCard.cs"] = ["clip_children"],
        ["UiNotificationContent.cs"] = ["clip_children", "Kind"],
        ["UiMenu.cs"] = ["clip_children"],
        ["UiPartRow.cs"] = ["self_modulate"],
        ["UiIconTabs.cs"] = ["theme_override_constants/separation"],
        ["UiInfoRow.cs"] = ["theme_override_constants/separation"],
        ["UiSegmentedSwitch.cs"] = ["theme_override_constants/separation"],
        ["UiTextField.cs"] = ["theme_override_constants/separation"],
        ["UiNameField.cs"] = ["theme_override_constants/separation"],
        ["UiValueRow.cs"] = ["theme_override_constants/separation"],
    };

    [Fact]
    public void Scenes_DoNotPinStylesOnNodes()
    {
        SceneNodes.All()
            .Where(node => SceneNodes.StyleOverrideGroups.Any(
                group => node.Body.Contains("\n" + group, StringComparison.Ordinal)))
            .Select(node => node.ToString())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Scenes_DoNotStoreComponentDerivedProperties()
    {
        _derived
            .SelectMany(entry => SceneNodes.WithScript(entry.Key)
                .SelectMany(node => entry.Value
                    .Where(property => node.Body.Contains("\n" + property + " = ", StringComparison.Ordinal))
                    .Select(property => $"{node} {property}")))
            .ShouldBeEmpty();
    }
}
