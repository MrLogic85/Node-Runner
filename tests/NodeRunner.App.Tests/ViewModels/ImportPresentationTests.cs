using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>The Import screen's states.</summary>
public sealed class ImportPresentationTests
{
    [Fact]
    public void Waiting_SaysWhatToDo_AndCannotAdd()
    {
        var waiting = ImportPresentation.Waiting;

        waiting.State.ShouldBe(ImportState.Waiting);
        waiting.NoteTitle.ShouldBeNull();
        waiting.NoteText.ShouldBe(UiText.Plain("Copy a creation's share code, then tap Paste."));
        waiting.CanAdd.ShouldBeFalse();
        waiting.PasteIsPrimary.ShouldBeTrue();
    }

    [Fact]
    public void ABuild_ShowsItsNameAndPartsByKind_AndCanBeAdded_WarningIfFromAnOlderVersion()
    {
        var walker = CreationExamples.Walker.Creature;
        var build = new CreationDef(Guid.NewGuid(), "Walker", walker);

        var preview = ImportPresentation.For(new ShareCodeRead(build, null), []);

        preview.State.ShouldBe(ImportState.Preview);
        preview.Build.ShouldBe(build);
        preview.Name.ShouldBe("Walker");
        preview.Parts.ShouldBe([
            new ImportPartRow(PartKind.Node, walker.Nodes.Count),
            new ImportPartRow(PartKind.Beam, walker.Beams.Count),
            new ImportPartRow(PartKind.Accelerometer, 1),
            new ImportPartRow(PartKind.Piston, walker.Pistons.Count),
        ]);
        preview.CanAdd.ShouldBeTrue();
        preview.PasteIsPrimary.ShouldBeFalse();
        preview.UpdatedNote.ShouldBeNull();
        ImportPresentation.For(new ShareCodeRead(build, null, UpdatedFrom: 6), []).UpdatedNote
            .ShouldBe(UiText.Plain("Shared from an older version, import may differ from original."));
    }

    [Theory]
    [InlineData(ShareCodeRefusal.Empty, "Nothing to paste", "Copy a creation's share code first, then tap Paste.")]
    [InlineData(ShareCodeRefusal.NotACreation, "Not a share code", "This text is not a Node Runner creation.")]
    [InlineData(ShareCodeRefusal.Damaged, "This code is damaged", "Part of it is missing or changed. Ask for the code again.")]
    [InlineData(ShareCodeRefusal.NewerVersion, "Made in a newer version", "Update Node Runner to open this creation.")]
    [InlineData(ShareCodeRefusal.NothingToBuild, "Nothing to build", "This creation has no parts yet.")]
    public void ARefusal_SaysWhy_AndCannotAdd(ShareCodeRefusal refusal, string title, string text)
    {
        var refused = ImportPresentation.For(ShareCodeRead.Refused(refusal), ["Walker"]);

        refused.State.ShouldBe(ImportState.Refused);
        refused.Build.ShouldBeNull();
        refused.Parts.ShouldBeEmpty();
        refused.NoteTitle.ShouldBe(UiText.Plain(title));
        refused.NoteText.ShouldBe(UiText.Plain(text));
        refused.CanAdd.ShouldBeFalse();
        refused.PasteIsPrimary.ShouldBeTrue();
        refused.NameLine("Walker").ShouldBeNull();
    }

    [Theory]
    [InlineData("walker", true)]
    [InlineData("  Walker ", true)]
    [InlineData("Walker 2", false)]
    [InlineData("   ", false)]
    public void NameLine_SaysTheNameIsTaken_InAnyCaseOrSpacing(string name, bool taken)
    {
        Walker(["Strider", "Walker"]).NameLine(name).ShouldBe(taken ? UiText.Plain("You already have one by this name.") : null);
    }

    private static ImportPresentation Walker(IReadOnlyList<string> takenNames) =>
        ImportPresentation.For(new ShareCodeRead(new CreationDef(Guid.NewGuid(), "Walker", CreationExamples.Walker.Creature), null), takenNames);
}
