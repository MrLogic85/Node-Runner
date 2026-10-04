using Microsoft.CodeAnalysis.CSharp;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// <c>project/locale/messages.pot</c> holds every text the player can see (#778). When text
/// changes, regenerate it with <c>NODE_RUNNER_UPDATE_POT=1 dotnet test tests/NodeRunner.Ui.Tests
/// --filter TranslationTemplateTests</c>; see docs/LOCALIZATION.md.
/// </summary>
public sealed class TranslationTemplateTests
{
    private const string _updateVariable = "NODE_RUNNER_UPDATE_POT";

    // The UiText factories the extractor reads, without the rest of the App layer.
    private static readonly CSharpSources.Source _uiText = Source(
        "libs/NodeRunner.App/ViewModels/UiText.cs",
        """
        namespace NodeRunner.App.ViewModels;
        public sealed record UiText(string Message)
        {
            public static UiText Plain(string message) => new(message);
            public static UiText Format(string template, params object[] args) => new(template);
            public static UiText Counted(string singular, string plural, long count) => new(singular);
            public UiText InContext(string context) => this;
        }
        """);

    // Components with exported text, like UiSlider's step labels and UiSegmentedSwitch's segments.
    private static readonly CSharpSources.Source _components = Source(
        "project/src/ui/lib/Components.cs",
        """
        namespace NodeRunner.Ui.Lib;
        public partial class UiSteps : Godot.Control { [Godot.Export] public string[] StepLabels { get; set; } = []; }
        public partial class UiSegment : Godot.Resource { [Godot.Export] public string Text { get; set; } = ""; }
        """);

    private static readonly CSharpSources.Source _galleryScreen = Source(
        "project/src/ui/screens/GalleryScreen.cs",
        """
        namespace NodeRunner.Ui.Screens;
        public abstract partial class GalleryScreen : Godot.Control { }
        """);

    private static readonly CSharpSources.Source _gallery = Source(
        "project/src/ui/screens/TestGallery.cs",
        """
        namespace NodeRunner.Ui.Screens;
        public partial class TestGallery : GalleryScreen
        {
            void Show()
            {
                new Godot.Label().Text = "Gallery label";
                _ = NodeRunner.App.ViewModels.UiText.Plain("Gallery text");
            }
        }
        """);

    [Fact]
    public void Template_matches_the_text_in_the_game()
    {
        var extraction = TranslationTemplate.Current;
        var pot = TranslationTemplate.Render(extraction);
        var path = Path.Combine(SceneNodes.FindRepositoryRoot(), TranslationTemplate.Path);
        if (Environment.GetEnvironmentVariable(_updateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pot);
        }

        extraction.Problems.ShouldBeEmpty();
        File.Exists(path).ShouldBeTrue($"Create {TranslationTemplate.Path}: run the UI tests with {_updateVariable}=1.");
        File.ReadAllText(path).ReplaceLineEndings("\n").ShouldBe(pot, $"Shown text changed. Regenerate {TranslationTemplate.Path}: run the UI tests with {_updateVariable}=1.");
    }

    [Fact]
    public void Text_set_in_code_is_followed_to_where_it_is_shown()
    {
        var ids = Ids(Code(
            """
            const string Heading = "Heading";
            void Show(bool ready)
            {
                var label = new Label();
                var text = ready ? "Ready" : Fallback();
                label.Text = text;
                Tip(label);
                new Button().TooltipText = Heading;
            }
            static string Fallback() => "Not ready";
            static void Tip(Control control, string tip = "Tip") => control.TooltipText = tip;
            """));

        ids.ShouldBe(["Heading", "Not ready", "Ready", "Tip"], ignoreOrder: true);
    }

    [Fact]
    public void Text_that_is_never_shown_is_left_out()
    {
        var ids = Ids(Code(
            """
            void Fail() => throw new System.InvalidOperationException("Something broke.");
            void Hidden() => new Label().Language = "en";
            """));

        ids.ShouldBeEmpty();
    }

    [Fact]
    public void Ui_text_keeps_its_plural_and_context()
    {
        var entries = Code(
            """
            NodeRunner.App.ViewModels.UiText[] Texts(int count) =>
            [
                NodeRunner.App.ViewModels.UiText.Plain("Start"),
                NodeRunner.App.ViewModels.UiText.Plain("Start").InContext("verb"),
                NodeRunner.App.ViewModels.UiText.Format("{0} m", 2),
                NodeRunner.App.ViewModels.UiText.Counted("{0} node", "{0} nodes", count),
            ];
            """).Entries.Keys;

        entries.ShouldBe(
            [
                new TranslationTemplate.Entry(null, "Start", null),
                new TranslationTemplate.Entry("verb", "Start", null),
                new TranslationTemplate.Entry(null, "{0} m", null),
                new TranslationTemplate.Entry(null, "{0} node", "{0} nodes"),
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Ui_text_from_a_record_is_followed_to_its_literal()
    {
        var ids = Ids(Code(
            """
            sealed record Unit(string Readout);
            static readonly Unit Speed = new("{0} m/s");
            NodeRunner.App.ViewModels.UiText Readout(double value) => NodeRunner.App.ViewModels.UiText.Format(Speed.Readout, value);
            """));

        ids.ShouldBe(["{0} m/s"]);
    }

    [Fact]
    public void Counted_text_that_is_not_literal_is_a_problem()
    {
        var extraction = Code(
            """
            NodeRunner.App.ViewModels.UiText Counted(string one, int count) =>
                NodeRunner.App.ViewModels.UiText.Counted(one, "{0} nodes", count);
            """);

        extraction.Problems.ShouldHaveSingleItem().ShouldContain("counted text and a context need literal text");
    }

    [Fact]
    public void Ui_text_that_cannot_be_traced_to_a_literal_is_a_problem()
    {
        var extraction = Code(
            """
            NodeRunner.App.ViewModels.UiText Greeting(string name) =>
                NodeRunner.App.ViewModels.UiText.Plain($"Hello {name}");
            """);

        extraction.Problems.ShouldHaveSingleItem().ShouldContain("a UiText needs literal text");
    }

    [Fact]
    public void One_text_cannot_be_both_plain_and_counted()
    {
        var extraction = Code(
            """
            NodeRunner.App.ViewModels.UiText[] Texts(int count) =>
            [
                NodeRunner.App.ViewModels.UiText.Plain("{0} node"),
                NodeRunner.App.ViewModels.UiText.Counted("{0} node", "{0} nodes", count),
            ];
            """);

        extraction.Problems.ShouldHaveSingleItem().ShouldContain("\"{0} node\" is used both");
    }

    [Fact]
    public void Scene_text_keeps_its_own_node_context_and_skips_untranslated_nodes()
    {
        // Written with Windows line endings, as git may check scenes out.
        var entries = SceneWithWindowsLineEndings(
            """
            [gd_scene format=3]

            [sub_resource type="Resource" id="Segment_a"]
            Text = "Segment"

            [node name="Root" type="Button"]
            translation_context = &"menu"
            text = "Open"

            [node name="Title" type="Label" parent="."]
            translation_context = "heading"
            text = "Title"
            tooltip_text = "Two
            lines"
            metadata/_note = "Not shown"

            [node name="Plain" type="Label" parent="."]
            text = "Plain"

            [node name="Names" type="Control" parent="."]
            auto_translate_mode = 2

            [node name="Name" type="Label" parent="Names"]
            text = "Walker"

            [node name="Again" type="Label" parent="Names/Name"]
            auto_translate_mode = 1
            text = "Translated again"

            [node name="Steps" type="Control" parent="."]
            StepLabels = PackedStringArray("Low", "High")
            Segments = [SubResource("Segment_a")]
            """);

        entries.ShouldBe(
            [
                new TranslationTemplate.Entry("menu", "Open", null),
                new TranslationTemplate.Entry("heading", "Title", null),
                new TranslationTemplate.Entry("heading", "Two\nlines", null),
                new TranslationTemplate.Entry(null, "Plain", null),
                new TranslationTemplate.Entry(null, "Translated again", null),
                new TranslationTemplate.Entry(null, "Low", null),
                new TranslationTemplate.Entry(null, "High", null),
                new TranslationTemplate.Entry(null, "Segment", null),
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Gallery_scenes_stay_in_English()
    {
        var entries = Scene(
            """
            [gd_scene format=3]

            [ext_resource type="Script" path="res://src/ui/screens/TestGallery.cs" id="1"]

            [node name="Root" type="Control"]
            script = ExtResource("1")

            [node name="Title" type="Label" parent="."]
            text = "Gallery"
            """);

        entries.ShouldBeEmpty();
    }

    [Fact]
    public void Template_lists_context_and_plural_in_gettext_form()
    {
        var pot = TranslationTemplate.Render(new TranslationTemplate.Extraction(
            new Dictionary<TranslationTemplate.Entry, SortedSet<string>>
            {
                [new TranslationTemplate.Entry("verb", "Say \"hi\"", null)] = ["a.cs"],
                [new TranslationTemplate.Entry(null, "{0} node", "{0} nodes")] = ["b.cs", "c.tscn"],
            },
            []));

        pot.ShouldEndWith(
            """

            #: b.cs
            #: c.tscn
            msgid "{0} node"
            msgid_plural "{0} nodes"
            msgstr[0] ""
            msgstr[1] ""

            #: a.cs
            msgctxt "verb"
            msgid "Say \"hi\""
            msgstr ""

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Gallery_code_stays_in_English()
    {
        var extraction = TranslationTemplate.Extract([_uiText, _galleryScreen, _gallery], []);

        extraction.Entries.ShouldBeEmpty();
        extraction.Problems.ShouldBeEmpty();
    }

    private static CSharpSources.Source Source(string path, string code) => new(path, CSharpSyntaxTree.ParseText(code));

    private static TranslationTemplate.Extraction Code(string member) =>
        TranslationTemplate.Extract([_uiText, CSharpSources.Snippet(member)], []);

    private static string[] Ids(TranslationTemplate.Extraction extraction) =>
        extraction.Entries.Keys.Select(entry => entry.Id).ToArray();

    private static IEnumerable<TranslationTemplate.Entry> SceneWithWindowsLineEndings(string text) => Scene(text.ReplaceLineEndings("\r\n"));

    private static IEnumerable<TranslationTemplate.Entry> Scene(string text) =>
        TranslationTemplate.Extract([_uiText, _components, _galleryScreen, _gallery], [("project/scenes/Test.tscn", text)]).Entries.Keys;
}
