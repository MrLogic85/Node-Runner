using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiIconsTests
{
    [Fact]
    public void NoIcon_IsNotAResourceAndDoesNotRenumberExistingIcons()
    {
        ((int)UiIconId.None).ShouldBe(-1);
        ((int)UiIconId.Back).ShouldBe(0);
        ((int)UiIconId.PartWing).ShouldBe(54);
        ((int)UiIconId.MapStairs).ShouldBe(60);
        UiIcons.AllIds.ShouldNotContain(UiIconId.None);
        Should.Throw<ArgumentOutOfRangeException>(() => UiIcons.PathFor(UiIconId.None));
    }

    [Fact]
    public void EveryTypedIcon_MapsToAnExistingCopiedSvg()
    {
        var projectRoot = Path.Combine(FindRepositoryRoot(), "project");

        UiIcons.AllIds.Count.ShouldBe(70);

        foreach (var icon in UiIcons.AllIds)
        {
            File.Exists(ToAssetPath(projectRoot, UiIcons.PathFor(icon))).ShouldBeTrue($"Missing icon: {icon}");
        }
    }

    [Fact]
    public void PartGlyphs_AreNeverAllowedAtSmall()
    {
        foreach (var icon in UiIcons.AllIds)
        {
            var isGlyph = UiIcons.PathFor(icon).StartsWith(UiIcons.PartRoot, StringComparison.Ordinal);
            UiIcons.IsPartGlyph(icon).ShouldBe(isGlyph, icon.ToString());
            UiIcons.IsAllowed(icon, UiIconSize.Small).ShouldBe(!isGlyph, icon.ToString());
            UiIcons.IsAllowed(icon, UiIconSize.Standard).ShouldBeTrue(icon.ToString());
        }

        Should.Throw<ArgumentException>(() => UiIcons.Load(UiIconId.PartWheel, UiIconSize.Small));
    }

    [Fact]
    public void IconSizes_UseOnlyTheFourCanonicalPixelMappings()
    {
        Enum.GetValues<UiIconSize>().Select(UiIcons.Pixels).ShouldBe([12, 16, 20, 24]);
    }

    [Fact]
    public void EveryCanonicalSvg_IsEmbeddedForRuntimeRasterization()
    {
        var resources = typeof(UiIcons).Assembly.GetManifestResourceNames();

        foreach (var icon in UiIcons.AllIds)
        {
            resources.ShouldContain(UiIcons.PathFor(icon)["res://assets/".Length..]);
        }
    }

    [Fact]
    public void CanonicalGalleryFixtures_DoNotContainPseudoIconStrings()
    {
        var gallery = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "project", "src", "ui", "screens", "ComponentGalleryScreen.cs"));

        foreach (var pseudoIcon in new[] { "IconText =", "Glyph =", "Glyphs =", "\"?\"", "\"!\"", "\"...\"", "🔒", "◎", "▶", "✓" })
        {
            gallery.ShouldNotContain(pseudoIcon);
        }
    }

    [Fact]
    public void ActiveProductUiCallers_DoNotUseLegacyIconAliasesOrPseudoIconLiterals()
    {
        var uiRoot = Path.Combine(FindRepositoryRoot(), "project", "src", "ui");
        var activeFiles = Directory.EnumerateFiles(Path.Combine(uiRoot, "screens"), "*.cs")
            .Concat(Directory.EnumerateFiles(Path.Combine(uiRoot, "widgets"), "*.cs"));
        var bannedSnippets = new[]
        {
            "IconText =",
            "Glyph =",
            "Glyphs =",
            "🔒",
            "⋯",
            "▶",
            "✎",
            "✓",
            "⚠",
            "⌫",
            "⧉",
            "◎",
            "⛓",
            "◌",
            "▣",
            "⌃",
            "⌄",
        };

        foreach (var path in activeFiles)
        {
            var text = File.ReadAllText(path);
            foreach (var snippet in bannedSnippets)
            {
                text.Contains(snippet, StringComparison.Ordinal).ShouldBeFalse($"{Path.GetRelativePath(uiRoot, path)} must use typed UiIconId APIs for icon visuals.");
            }
        }
    }

    private static string ToAssetPath(string projectRoot, string resourcePath) =>
        Path.Combine(projectRoot, resourcePath["res://".Length..]);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NodeRunner.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the Node Runner repository root.");
    }
}
