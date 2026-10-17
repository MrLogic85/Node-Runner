using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class ShadowsBudgetTests
{
    // The limits scale inversely with the parts from the reference creature's, within the Shadows range.
    [Theory]
    [InlineData(ShadowsBudget.ReferenceParts, ShadowsBudget.ReferenceSmoothLimit, ShadowsBudget.ReferenceSevereLimit)]
    [InlineData(60, 16, 32)]
    [InlineData(5, TrainSettingsDef.MaxShadows, TrainSettingsDef.MaxShadows)]
    [InlineData(2000, TrainSettingsDef.MinShadows, TrainSettingsDef.MinShadows)]
    [InlineData(0, TrainSettingsDef.MaxShadows, TrainSettingsDef.MaxShadows)]
    public void For_ScalesTheLimitsWithTheParts(int parts, int smooth, int severe)
    {
        var budget = ShadowsBudget.For(Creature(parts));

        budget.SmoothLimit.ShouldBe(smooth);
        budget.SevereLimit.ShouldBe(severe);
        budget.LoadOf(smooth).ShouldBe(ShadowsLoad.Smooth);
        if (severe > smooth)
        {
            budget.LoadOf(smooth + 1).ShouldBe(ShadowsLoad.Caution);
            budget.LoadOf(severe).ShouldBe(ShadowsLoad.Caution);
            budget.LoadOf(severe + 1).ShouldBe(ShadowsLoad.TooMany);
        }
    }

    // Loose nodes are a valid drawing, so a creature of any part count is easy to make.
    internal static CreatureDef Creature(int parts) =>
        new([.. Enumerable.Range(1, parts).Select(id => new NodeDef(id, new Vector2D(id, 0)))], [], []);
}
