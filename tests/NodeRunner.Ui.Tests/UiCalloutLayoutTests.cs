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

    private static UiCalloutLayout.Placement Placement(Vector2 anchor, Vector2 direction, string text = "Note", float clearance = 18) =>
        new(anchor, direction, clearance, UiCallout.CalloutKind.Danger, UiIconId.Warn, text);
}
