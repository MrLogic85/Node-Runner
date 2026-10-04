using NodeRunner.Creature;

namespace NodeRunner.Ui.Tests;

public sealed class ShadowDrawingTests
{
    [Fact]
    public void EveryCreatureVisual_DeclaresItsShadowDrawing()
    {
        var visuals = typeof(Creature.Creature).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(Creature.Creature).Namespace
                && type.IsSubclassOf(typeof(Godot.CanvasItem))
                && type != typeof(Creature.Creature))
            .ToArray();

        visuals.ShouldNotBeEmpty();
        visuals.Where(type => !typeof(IShadowVisual).IsAssignableFrom(type)).ShouldBeEmpty();
    }

    [Fact]
    public void ShadowDrawing_FollowsTheCreatureModel()
    {
        AsShadow<NodeVisual>().ShouldBe(ShadowDrawing.Simplified);
        AsShadow<BeamVisual>().ShouldBe(ShadowDrawing.Simplified);
        AsShadow<SensorVisual>().ShouldBe(ShadowDrawing.Hidden);
        AsShadow<RigidHatchVisual>().ShouldBe(ShadowDrawing.Simplified);
        AsShadow<CameraRaysVisual>().ShouldBe(ShadowDrawing.Hidden);
    }

    private static ShadowDrawing AsShadow<T>()
        where T : IShadowVisual => T.AsShadow;
}
