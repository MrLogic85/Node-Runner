using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiCalloutLayoutTests
{
    private static readonly Vector2 _size = new(100, 20);
    private static readonly Rect2 _bounds = new(-1000, -1000, 2000, 2000);
    private static readonly float _above = -18 - UiCalloutLayout.LeaderLength - 20;

    [Fact]
    public void Arrange_PutsTheNearSideOfTheCalloutPastTheClearanceAndLeader()
    {
        var arranged = UiCalloutLayout.Arrange([Placement(new Vector2(0, 0), Vector2.Up)], [_size], _bounds);

        arranged[0].ShouldBe(new UiCalloutLayout.Arranged(
            new Rect2(-50, _above, 100, 20),
            new Vector2(0, -18 - UiCalloutLayout.LeaderLength)));
    }

    [Fact]
    public void Arrange_OnADiagonal_RunsTheLeaderStraightOutWithoutCoveringItsSpot()
    {
        var direction = new Vector2(-1, -1).Normalized();

        var arranged = UiCalloutLayout.Arrange([Placement(Vector2.Zero, direction)], [_size], _bounds)[0];

        arranged.Rect.Grow(UiCalloutLayout.LeaderLength + 18 - 1).HasPoint(Vector2.Zero).ShouldBeFalse();
        arranged.LeaderEnd.Normalized().IsEqualApprox(direction).ShouldBeTrue();
    }

    [Fact]
    public void Arrange_JoinsACalloutWithTheSameTextToTheOneItWouldOverlap()
    {
        var arranged = UiCalloutLayout.Arrange(
            [Placement(new Vector2(0, 0), Vector2.Up), Placement(new Vector2(30, 0), Vector2.Up), Placement(new Vector2(300, 0), Vector2.Up)],
            [_size, _size, _size],
            _bounds);

        arranged[0].Joined.ShouldBeFalse();
        arranged[1].Joined.ShouldBeTrue();
        arranged[1].Rect.ShouldBe(arranged[0].Rect);
        arranged[1].LeaderEnd.Y.ShouldBe(-18 - UiCalloutLayout.LeaderLength, 0.01f);
        arranged[2].Rect.ShouldBe(new Rect2(250, _above, 100, 20));
    }

    [Fact]
    public void Arrange_NeverJoinsACalloutWithLinesUnderItsCaption()
    {
        IReadOnlyList<UiCalloutLine> lines = [UiCalloutLine.OfMeters(UiTokens.Color.Accent, [new UiCalloutMeter("speed", 0.5, Centred: true)])];
        var withLines = Placement(new Vector2(0, 0), Vector2.Up) with { Lines = lines };

        var arranged = UiCalloutLayout.Arrange(
            [withLines, withLines with { Anchor = new Vector2(30, 0) }, Placement(new Vector2(0, 0), Vector2.Up)],
            [_size, _size, _size],
            _bounds);

        arranged.ShouldAllBe(callout => !callout.Joined);
    }

    [Fact]
    public void Arrange_NeverJoinsACalloutWithSectionsUnderItsCaption()
    {
        var withSections = Placement(new Vector2(0, 0), Vector2.Up) with { Sections = [new UiCalloutSection(UiIconId.None, "Wheel")] };

        var arranged = UiCalloutLayout.Arrange(
            [withSections, withSections with { Anchor = new Vector2(30, 0) }, Placement(new Vector2(0, 0), Vector2.Up)],
            [_size, _size, _size],
            _bounds);

        arranged.ShouldAllBe(callout => !callout.Joined);
    }

    [Fact]
    public void Arrange_StacksACalloutWithOtherTextAwayFromThePart_FirstListedNearest()
    {
        var arranged = UiCalloutLayout.Arrange(
            [Placement(new Vector2(0, 0), Vector2.Up), Placement(new Vector2(30, 0), Vector2.Up, "Other")],
            [_size, _size],
            _bounds);

        arranged[0].Rect.ShouldBe(new Rect2(-50, _above, 100, 20));
        arranged[1].ShouldBe(new UiCalloutLayout.Arranged(
            new Rect2(-50, _above - UiSize.Space.S1 - 20, 100, 20),
            arranged[1].LeaderEnd));
        arranged[1].Joined.ShouldBeFalse();
    }

    [Fact]
    public void Arrange_StacksDownwardForAPartWhoseClearSideIsBelow()
    {
        var arranged = UiCalloutLayout.Arrange(
            [Placement(new Vector2(0, 0), Vector2.Down), Placement(new Vector2(30, 0), Vector2.Down, "Other")],
            [_size, _size],
            _bounds);

        arranged[1].Rect.Position.Y.ShouldBe(arranged[0].Rect.End.Y + UiSize.Space.S1);
    }

    [Fact]
    public void Arrange_TakesInAStackItGrowsInto_SoNoTwoOverlap()
    {
        var arranged = UiCalloutLayout.Arrange(
            [
                Placement(new Vector2(0, 0), Vector2.Up),
                Placement(new Vector2(0, 0), Vector2.Up, "Second"),
                Placement(new Vector2(0, -200), Vector2.Up, "Third", clearance: -148),
            ],
            [_size, _size, _size],
            _bounds);

        var rects = arranged.Select(at => at.Rect).ToArray();
        for (var a = 0; a < rects.Length; a++)
        {
            for (var b = a + 1; b < rects.Length; b++)
            {
                rects[a].Intersects(rects[b]).ShouldBeFalse();
            }
        }

        rects.Select(rect => rect.Position.X).Distinct().ShouldHaveSingleItem();
    }

    [Fact]
    public void Arrange_KeepsListOrderInAStackThatTookInAnother()
    {
        var arranged = UiCalloutLayout.Arrange(
            [
                Placement(new Vector2(0, 0), Vector2.Up, "First"),
                Placement(new Vector2(0, -52), Vector2.Up, "Second", clearance: 0),
                Placement(new Vector2(10, 0), Vector2.Up, "Third"),
            ],
            [_size, _size, _size],
            _bounds);

        arranged[0].Rect.Position.Y.ShouldBeGreaterThan(arranged[1].Rect.Position.Y);
        arranged[1].Rect.Position.Y.ShouldBeGreaterThan(arranged[2].Rect.Position.Y);
    }

    [Fact]
    public void Arrange_PushesItAlongTheEdgeInsteadOfToTheOtherSide()
    {
        var bounds = new Rect2(-30, -20, 400, 400);

        var rect = UiCalloutLayout.Arrange([Placement(Vector2.Zero, Vector2.Up)], [_size], bounds)[0].Rect;

        rect.Position.ShouldBe(new Vector2(-30 + UiSize.Space.S1, -20 + UiSize.Space.S1));
    }

    [Theory]
    [InlineData(0, 300)]
    [InlineData(-80, 20)]
    public void Dock_PutsTheTopAtTheBoundsAndCentresWhereverThePartIs(float anchorX, float anchorY)
    {
        var arena = new Rect2(0, 0, 600, 240);

        var docked = UiCalloutLayout.Dock(new Vector2(anchorX, anchorY), _size, 258, arena);

        docked.Rect.ShouldBe(new Rect2(208, UiSize.Space.S1, 100, 20));
        docked.Joined.ShouldBeFalse();
    }

    [Fact]
    public void Dock_RunsTheLeaderFromThePartToTheBox()
    {
        var docked = UiCalloutLayout.Dock(new Vector2(258, 200), _size, 258, new Rect2(0, 0, 600, 240));

        docked.LeaderEnd.ShouldBe(new Vector2(258, UiSize.Space.S1 + 20));
    }

    [Theory]
    [InlineData(10, 120)]
    [InlineData(590, 500 - UiSize.Space.S1)]
    public void Dock_StaysInsideTheBoundsSideways(float centreX, float expectedLeft)
    {
        // Bounds that start at 120, as when the slow-motion chip takes the arena's left corner.
        var bounds = new Rect2(120 - UiSize.Space.S1, 0, 480 + UiSize.Space.S1, 240);

        var docked = UiCalloutLayout.Dock(Vector2.Zero, _size, centreX, bounds);

        docked.Rect.Position.X.ShouldBe(expectedLeft);
    }

    [Fact]
    public void Dock_GrowsATallerCalloutDownward()
    {
        var arena = new Rect2(0, 0, 600, 240);

        var small = UiCalloutLayout.Dock(Vector2.Zero, _size, 258, arena);
        var tall = UiCalloutLayout.Dock(Vector2.Zero, new Vector2(100, 90), 258, arena);

        tall.Rect.Position.ShouldBe(small.Rect.Position);
    }

    [Fact]
    public void Dock_PutsACalloutWiderThanTheBoundsAgainstTheirLeft()
    {
        var bounds = new Rect2(120, 0, 80, 240);

        var docked = UiCalloutLayout.Dock(Vector2.Zero, _size, 160, bounds);

        docked.Rect.Position.X.ShouldBe(120 + UiSize.Space.S1);
    }

    [Fact]
    public void DockedShare_CoversTheBoxItsLeaderAndAMargin()
    {
        var docked = new Rect2(208, UiSize.Space.S1, 100, 20);

        var share = UiCalloutLayout.DockedShare(docked, 240);

        share.ShouldBe((UiSize.Space.S1 + 20 + UiCalloutLayout.LeaderLength + UiSize.Space.S1) / 240f, 1e-6f);
    }

    [Fact]
    public void DockedShare_IsNoneForAnEmptyView()
    {
        UiCalloutLayout.DockedShare(new Rect2(0, 0, 100, 20), 0).ShouldBe(0);
    }

    [Theory]
    [InlineData(120, 120, 480)]
    [InlineData(-10, 0, 600)]
    [InlineData(700, 600, 0)]
    public void RightOf_KeepsWhatLiesRightOfTheEdgeInsideTheBounds(float x, float expectedLeft, float expectedWidth)
    {
        var right = UiCalloutLayout.RightOf(new Rect2(0, 0, 600, 240), x);

        right.ShouldBe(new Rect2(expectedLeft, 0, expectedWidth, 240));
    }

    private static UiCalloutLayout.Placement Placement(Vector2 anchor, Vector2 direction, string text = "Note", float clearance = 18) =>
        new(anchor, direction, clearance, UiCallout.CalloutKind.Danger, UiIconId.Warn, text);
}
