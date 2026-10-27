namespace NodeRunner.Ui.Tests;

/// <summary>
/// Saved scenes hold only what their author set. Styling comes from the Theme or from
/// component code, and each component keeps the values it derives out of the scene through
/// <c>UiUnsavedState</c>; a baked value would pin one theme's look or a stale computed size.
/// </summary>
public sealed class SceneDerivedStateTests
{
    // UiIcons builds icons as DPITextures at runtime; a saved one is a generated icon baked into the scene.
    private static readonly string[] _generatedTextureTypes = ["Image", "ImageTexture", "DPITexture"];

    // UiLabel derives these only while it stands on another label's baseline; a plain label authors them.
    private static readonly string[] _baselineDerived = ["custom_minimum_size", "size_flags_vertical", "vertical_alignment"];

    // Component script → properties its code derives on its own node.
    private static readonly Dictionary<string, string[]> _derived = new()
    {
        ["UiButton.cs"] = ["custom_minimum_size"],
        ["UiSidePanel.cs"] = ["custom_minimum_size"],
        ["UiCard.cs"] = ["clip_children"],
        ["UiStageCard.cs"] = ["clip_children", "Kind"],
        ["UiThemeCard.cs"] = ["clip_children", "Kind"],
        ["UiFrameCard.cs"] = ["clip_children"],
        ["UiPopupCard.cs"] = ["clip_children"],
        ["UiNotificationContent.cs"] = ["clip_children"],
        ["UiMenu.cs"] = ["clip_children"],
        ["UiPartRow.cs"] = ["modulate"],
        ["UiIconTabs.cs"] = ["theme_override_constants/separation"],
        ["UiInfoRow.cs"] = ["theme_override_constants/separation"],
        ["UiSegmentedSwitch.cs"] = ["theme_override_constants/separation"],
        ["UiTextField.cs"] = ["theme_override_constants/separation"],
        ["UiNameField.cs"] = ["theme_override_constants/separation"],
    };

    [Fact]
    public void Scenes_save_no_pinned_style_generated_texture_or_derived_property()
    {
        var pinnedStyles = SceneNodes.All()
            .Where(node => SceneNodes.StyleOverrideGroups.Any(group => node.Body.Contains("\n" + group, StringComparison.Ordinal)))
            .Select(node => $"{node} pins a style");
        var textures = SceneNodes.Files()
            .SelectMany(file => _generatedTextureTypes
                .Where(type => file.Text.Contains($"[sub_resource type=\"{type}\"", StringComparison.Ordinal))
                .Select(type => $"{file.Scene}: saves a generated {type}"));
        var derived = _derived
            .SelectMany(entry => SceneNodes.WithScript(entry.Key).SelectMany(node => Saved(node, entry.Value)));
        var baseline = SceneNodes.WithScript("UiLabel.cs")
            .Where(node => node.Body.Contains("\nBaselineOf = ", StringComparison.Ordinal))
            .SelectMany(node => Saved(node, _baselineDerived));

        pinnedStyles.Concat(textures).Concat(derived).Concat(baseline).ShouldBeEmpty();
    }

    private static IEnumerable<string> Saved(SceneNodes.Node node, string[] properties) =>
        properties
            .Where(property => node.Body.Contains("\n" + property + " = ", StringComparison.Ordinal))
            .Select(property => $"{node} saves derived {property}");
}
