using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Tests;

/// <summary>The Build Parts tray and the glyphs Build and Training show for parts.</summary>
public sealed class BuildPartsTrayTests
{
    [Fact]
    public void Every_part_link_and_part_kind_has_its_own_glyph()
    {
        UiIconId[][] trayGlyphs =
        [
            [.. Enum.GetValues<BuildPart>().Select(BuildScreen.PartIcon)],
            [.. Enum.GetValues<BuildLink>().Select(BuildScreen.LinkIcon)],
        ];
        var kindGlyphs = Enum.GetValues<PartKind>().Select(PartIcons.For).ToList();

        foreach (var icons in trayGlyphs)
        {
            icons.ShouldAllBe(icon => UiIcons.IsPartGlyph(icon));
            icons.ShouldBeUnique();
        }

        kindGlyphs.ShouldNotContain(UiIconId.None);
        kindGlyphs.ShouldBeUnique();
    }

    // SideIcon has a glyph for every kind a side can be, a link or the Wheel, and refuses every other.
    [Fact]
    public void Every_side_kind_has_its_own_glyph_and_no_other_kind_has_one()
    {
        var glyphs = new List<UiIconId>();
        foreach (var element in Enum.GetValues<CreatureElementKind>())
        {
            if (element == CreatureElementKind.Wheel || PartKinds.All.Any(info => info.Element == element && info.IsLink))
            {
                glyphs.Add(BuildScreen.SideIcon(element));
            }
            else
            {
                Should.Throw<ArgumentOutOfRangeException>(() => BuildScreen.SideIcon(element));
            }
        }

        glyphs.ShouldAllBe(icon => UiIcons.IsPartGlyph(icon));
        glyphs.ShouldBeUnique();
    }

    [Fact]
    public void Only_available_part_rows_can_be_dragged_out()
    {
        var draggable = PartTray.Groups()
            .SelectMany(group => group.Rows)
            .Where(row => BuildScreen.DraggablePart(row) is not null)
            .Select(row => row.Part);

        draggable.ShouldBe([BuildPart.Servo, BuildPart.Stepper, BuildPart.VelocityMotor, BuildPart.Brake, BuildPart.Wheel, BuildPart.Rubber, BuildPart.Accelerometer, BuildPart.Camera, BuildPart.TouchSensor, BuildPart.AngleSensor]);
    }
}
