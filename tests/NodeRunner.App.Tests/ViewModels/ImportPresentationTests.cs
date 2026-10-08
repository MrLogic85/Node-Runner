using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>The Import screen's states (#899).</summary>
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
    }

    [Fact]
    public void ABuild_ShowsItsNameAndPartsByKind_AndCanBeAdded()
    {
        var walker = CreationExamples.Walker.Creature;
        var build = new CreationDef(Guid.NewGuid(), "Walker", walker);

        var preview = ImportPresentation.For(new ShareCodeRead(build, null));

        preview.State.ShouldBe(ImportState.Preview);
        preview.Build.ShouldBe(build);
        preview.Name.ShouldBe("Walker");
        preview.Parts.ShouldBe([
            new ImportPartRow(PartSettingsKind.Node, walker.Nodes.Count),
            new ImportPartRow(PartSettingsKind.Beam, walker.Beams.Count),
            new ImportPartRow(PartSettingsKind.Accelerometer, 1),
            new ImportPartRow(PartSettingsKind.Piston, walker.Pistons.Count),
        ]);
        preview.CanAdd.ShouldBeTrue();
    }

    [Fact]
    public void EveryKindOfPart_HasARow_InThePartsTrayOrder()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new(0, 0)), new NodeDef(2, new(100, 0)), new NodeDef(3, new(0, 100)), new NodeDef(4, new(100, 100))],
            [new BeamDef(5, 1, 2), new BeamDef(6, 3, 4)],
            [new SensorDef(9, 5, SensorKind.Accelerometer), new SensorDef(10, 6, SensorKind.Camera, aim: 0)],
            [new ServoDef(11, 2, 5, 7)],
            [new PistonDef(7, 2, 4)],
            [new SpringDef(8, 1, 3)],
            nextPartId: 12);

        var preview = ImportPresentation.For(new ShareCodeRead(new CreationDef(Guid.NewGuid(), "All", creature), null));

        preview.Parts.Select(row => (row.Kind, row.Count)).ShouldBe([
            (PartSettingsKind.Node, 4),
            (PartSettingsKind.Beam, 2),
            (PartSettingsKind.Accelerometer, 1),
            (PartSettingsKind.Camera, 1),
            (PartSettingsKind.Servo, 1),
            (PartSettingsKind.Piston, 1),
            (PartSettingsKind.Spring, 1),
        ]);
    }

    [Theory]
    [InlineData(ShareCodeRefusal.Empty, "Nothing to paste", "Copy a creation's share code first, then tap Paste.")]
    [InlineData(ShareCodeRefusal.NotACreation, "Not a share code", "This text is not a Node Runner creation.")]
    [InlineData(ShareCodeRefusal.Damaged, "This code is damaged", "Part of it is missing or changed. Ask for the code again.")]
    [InlineData(ShareCodeRefusal.NewerVersion, "Made in a newer version", "Update Node Runner to open this creation.")]
    [InlineData(ShareCodeRefusal.NothingToBuild, "Nothing to build", "This creation has no parts yet.")]
    public void ARefusal_SaysWhy_AndCannotAdd(ShareCodeRefusal refusal, string title, string text)
    {
        var refused = ImportPresentation.For(ShareCodeRead.Refused(refusal));

        refused.State.ShouldBe(ImportState.Refused);
        refused.Build.ShouldBeNull();
        refused.Parts.ShouldBeEmpty();
        refused.NoteTitle.ShouldBe(UiText.Plain(title));
        refused.NoteText.ShouldBe(UiText.Plain(text));
        refused.CanAdd.ShouldBeFalse();
    }
}
