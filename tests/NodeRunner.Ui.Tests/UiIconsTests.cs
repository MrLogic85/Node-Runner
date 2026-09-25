using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiIconsTests
{
    [Fact]
    public void NoIcon_IsNotAResourceAndDoesNotRenumberExistingIcons()
    {
        ((int)UiIconId.None).ShouldBe(-1);
        ((int)UiIconId.Back).ShouldBe(0);
        UiIcons.AllIds.ShouldNotContain(UiIconId.None);
        Should.Throw<ArgumentOutOfRangeException>(() => UiIcons.PathFor(UiIconId.None));
    }

    [Fact]
    public void EveryTypedIcon_MapsToAnExistingCopiedSvg()
    {
        var projectRoot = Path.Combine(FindRepositoryRoot(), "project");

        UiIcons.AllIds.Count.ShouldBe(55);

        foreach (var icon in UiIcons.AllIds)
        {
            File.Exists(ToAssetPath(projectRoot, UiIcons.PathFor(icon))).ShouldBeTrue($"Missing icon: {icon}");
        }
    }

    [Fact]
    public void IconSizes_UseOnlyTheFourCanonicalPixelMappings()
    {
        Enum.GetValues<UiIconSize>().Select(UiIcons.Pixels).ShouldBe([12, 16, 20, 24]);
    }

    [Theory]
    [InlineData(UiIconSize.Small, 1920, 1080, 36)]
    [InlineData(UiIconSize.Standard, 1920, 1080, 48)]
    [InlineData(UiIconSize.Large, 1920, 1080, 60)]
    [InlineData(UiIconSize.ExtraLarge, 1920, 1080, 72)]
    [InlineData(UiIconSize.ExtraLarge, 1280, 720, 48)]
    public void RasterPixels_MatchesCanonicalSizeAtUiScale(
        UiIconSize size,
        int windowWidth,
        int windowHeight,
        int expected)
    {
        UiIcons.RasterPixels(size, windowWidth, windowHeight).ShouldBe(expected);
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

    [Theory]
    [InlineData("back", UiIconId.Back)]
    [InlineData("brain", UiIconId.Model)]
    [InlineData("model", UiIconId.Model)]
    [InlineData("padlock", UiIconId.Lock)]
    [InlineData("play", UiIconId.Play)]
    [InlineData("settings", UiIconId.Gear)]
    [InlineData("locked", UiIconId.Lock)]
    [InlineData("delete", UiIconId.Trash)]
    public void LegacyAliases_ResolveThroughOneCanonicalAdapter(string alias, UiIconId expected)
    {
        UiIconGlyphs.TryParse(alias, out var icon).ShouldBeTrue();
        icon.ShouldBe(expected);
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
