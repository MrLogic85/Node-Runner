namespace NodeRunner.Ui.Tests;

/// <summary>A translation uses each of its msgid's <c>{0}</c>-style placeholders exactly once; docs/LOCALIZATION.md.</summary>
public sealed class TranslationPlaceholdersTests
{
    [Fact]
    public void Every_translation_keeps_its_placeholders()
    {
        var root = SceneNodes.FindRepositoryRoot();
        var problems = Directory.EnumerateFiles(Path.Combine(root, TranslationPlaceholders.Folder), "*.po")
            .Order(StringComparer.Ordinal)
            .SelectMany(path => TranslationPlaceholders.Problems(Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path)))
            .ToList();

        problems.ShouldBeEmpty(string.Join("\n", problems));
    }

    [Fact]
    public void Matching_untranslated_header_and_obsolete_entries_pass()
    {
        Problems(
            """
            msgid ""
            msgstr ""
            "Language: sv\n"
            "Plural-Forms: nplurals=2; plural=(n != 1);\n"

            #: a.cs
            msgid "{0} m of {1}"
            msgstr "{1}: {0} m"

            #, fuzzy
            msgid "Say \"{0}\""
            msgstr "Säg \"{0}\""

            msgid "{0} beam"
            msgid_plural "{0} beams"
            msgstr[0] "{0} balk"
            msgstr[1] ""

            msgid "Not yet {0}"
            msgstr ""

            msgid "Literal {{0}} and {0:0.0} m"
            msgstr "Bokstavligen {{1}} och {0,4:0.0} m"

            #~ msgid "Gone {0}"
            #~ msgstr "Borta"
            """).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Borta", "uses none, not {0}")]
    [InlineData("{0} och {0}", "uses {0}{0}, not {0}")]
    [InlineData("{0} av {1}", "uses {0}{1}, not {0}")]
    public void A_lost_repeated_or_added_placeholder_is_named(string translation, string problem) =>
        Problems($"msgid \"{{0}} left\"\nmsgstr \"{translation}\"\n")
            .ShouldHaveSingleItem().ShouldBe($"sv.po: msgid \"{{0}} left\" msgstr {problem}");

    [Fact]
    public void Each_plural_form_keeps_every_placeholder()
    {
        var problems = Problems(
            """
            msgid "{0} beam"
            msgid_plural "{0} beams"
            msgstr[0] "en balk"
            msgstr[1] "{0} balkar"
            """);

        problems.ShouldHaveSingleItem().ShouldBe("sv.po: msgid \"{0} beam\" msgstr[0] uses none, not {0}");
    }

    [Fact]
    public void A_fuzzy_entry_is_checked_with_its_context_and_wrapped_lines()
    {
        var problems = Problems(
            """
            #, fuzzy
            msgctxt "part"
            msgid ""
            "{0} long "
            "message"
            msgstr ""
            "ett långt "
            "meddelande {"
            "1}"
            """);

        problems.ShouldHaveSingleItem().ShouldBe("sv.po: msgid \"{0} long message\" (msgctxt \"part\") msgstr uses {1}, not {0}");
    }

    private static List<string> Problems(string po) => TranslationPlaceholders.Problems("sv.po", po).ToList();
}
