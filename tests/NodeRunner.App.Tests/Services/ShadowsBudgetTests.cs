using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class ShadowsBudgetTests
{
    [Fact]
    public void ReferenceCreature_GetsTheReferenceLimits()
    {
        var budget = ShadowsBudget.For(Creature(ShadowsBudget.ReferenceParts));

        budget.SmoothLimit.ShouldBe(ShadowsBudget.ReferenceSmoothLimit);
        budget.SevereLimit.ShouldBe(ShadowsBudget.ReferenceSevereLimit);
    }

    [Theory]
    [InlineData(60, 16, 32)]
    [InlineData(47, 20, 40)]
    [InlineData(20, 48, 96)]
    public void LimitsScaleInverselyWithParts(int parts, int smooth, int severe)
    {
        var budget = ShadowsBudget.For(Creature(parts));

        budget.SmoothLimit.ShouldBe(smooth);
        budget.SevereLimit.ShouldBe(severe);
    }

    [Fact]
    public void LimitsStayWithinTheShadowsRange()
    {
        var small = ShadowsBudget.For(Creature(5));
        var huge = ShadowsBudget.For(Creature(2000));
        var empty = ShadowsBudget.For(new CreatureDef([], [], []));

        small.SmoothLimit.ShouldBe(TrainSettingsDef.MaxShadows);
        small.SevereLimit.ShouldBe(TrainSettingsDef.MaxShadows);
        huge.SmoothLimit.ShouldBe(TrainSettingsDef.MinShadows);
        huge.SevereLimit.ShouldBe(TrainSettingsDef.MinShadows);
        empty.SevereLimit.ShouldBe(TrainSettingsDef.MaxShadows);
    }

    [Theory]
    [InlineData(2, ShadowsLoad.Smooth)]
    [InlineData(32, ShadowsLoad.Smooth)]
    [InlineData(33, ShadowsLoad.Caution)]
    [InlineData(64, ShadowsLoad.Caution)]
    [InlineData(65, ShadowsLoad.TooMany)]
    [InlineData(100, ShadowsLoad.TooMany)]
    public void LoadOf_ComparesWithTheLimits(int shadows, ShadowsLoad expected) =>
        ShadowsBudget.For(Creature(ShadowsBudget.ReferenceParts)).LoadOf(shadows).ShouldBe(expected);

    [Fact]
    public void ASmallCreature_IsNeverTooMany()
    {
        var budget = ShadowsBudget.For(Creature(5));

        budget.LoadOf(TrainSettingsDef.MaxShadows).ShouldBe(ShadowsLoad.Smooth);
    }

    // Loose nodes are a valid drawing, so a creature of any part count is easy to make.
    internal static CreatureDef Creature(int parts) =>
        new([.. Enumerable.Range(1, parts).Select(id => new NodeDef(id, new Vector2D(id, 0)))], [], []);
}
