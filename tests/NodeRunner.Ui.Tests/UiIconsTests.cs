using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiIconsTests
{
    [Fact]
    public void Every_icon_has_an_svg_embedded_for_runtime_rasterization()
    {
        var resources = typeof(UiIcons).Assembly.GetManifestResourceNames();

        UiIcons.AllIds.ShouldNotContain(UiIconId.None);
        UiIcons.AllIds
            .Where(icon => !File.Exists(Path.Combine(ThemeFile.ProjectRoot, UiIcons.PathFor(icon)["res://".Length..]))
                || !resources.Contains(UiIcons.PathFor(icon)["res://assets/".Length..]))
            .ShouldBeEmpty("Copy the icon's SVG into project/assets/icons, where the build embeds it.");
    }

    [Fact]
    public void Part_glyphs_are_never_allowed_at_small()
    {
        foreach (var icon in UiIcons.AllIds)
        {
            var isGlyph = Path.GetFileName(UiIcons.PathFor(icon)).StartsWith(UiIcons.PartPrefix, StringComparison.Ordinal);
            UiIcons.IsPartGlyph(icon).ShouldBe(isGlyph, icon.ToString());
            UiIcons.IsAllowed(icon, UiIconSize.Small).ShouldBe(!isGlyph, icon.ToString());
            UiIcons.IsAllowed(icon, UiIconSize.Standard).ShouldBeTrue(icon.ToString());
        }

        Should.Throw<ArgumentException>(() => UiIcons.Load(UiIconId.PartWheel, UiIconSize.Small));
    }

    [Theory]
    [InlineData(UiIconId.Beam, "icon_beam.svg")]
    [InlineData(UiIconId.PartBeam, "part_beam.svg")]
    public void Notification_icon_keeps_the_library_when_names_overlap(UiIconId icon, string file) =>
        new UiNotificationIcon(icon).ResourcePath.ShouldBe(UiIcons.Root + file);

    [Theory]
    [InlineData(UiIconId.None)]
    [InlineData((UiIconId)999)]
    public void Notification_icon_rejects_a_missing_or_invalid_glyph(UiIconId icon) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new UiNotificationIcon(icon));

    // Screens and widgets draw icons through UiIconId, never a text stand-in.
    [Fact]
    public void Screens_and_widgets_use_no_pseudo_icon_text()
    {
        string[] banned =
        [
            "IconText =", "Glyph =", "Glyphs =", "\"?\"", "\"!\"", "\"...\"",
            "🔒", "⋯", "▶", "✎", "✓", "⚠", "⌫", "⧉", "◎", "⛓", "◌", "▣", "⌃", "⌄",
        ];

        CSharpSources.Project
            .Where(source => source.Path.StartsWith("ui/screens/", StringComparison.Ordinal) || source.Path.StartsWith("ui/widgets/", StringComparison.Ordinal))
            .SelectMany(source => banned
                .Where(snippet => source.Tree.ToString().Contains(snippet, StringComparison.Ordinal))
                .Select(snippet => $"{source.Path}: {snippet}"))
            .ShouldBeEmpty("Use typed UiIconId APIs for icon visuals.");
    }
}
