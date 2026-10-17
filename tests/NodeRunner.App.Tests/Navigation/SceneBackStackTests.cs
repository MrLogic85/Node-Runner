using NodeRunner.App.Navigation;

namespace NodeRunner.App.Tests.Navigation;

public sealed class SceneBackStackTests
{
    private static readonly Guid _wormId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid _antId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private sealed record CreationsRoute : SceneRoute;

    private sealed record ExamplesRoute : SceneRoute;

    private sealed record BuildRoute(Guid CreationId) : SceneRoute;

    private sealed record AchievementRoute(string AchievementId) : SceneRoute;

    private sealed record ListRoute(List<Guid> CreationIds) : SceneRoute;

    private abstract record OpenRoute : SceneRoute;

    [Fact]
    public void AStack_StartsOnItsRoot_AndBackReturnsEachRouteWithItsArguments()
    {
        var stack = new SceneBackStack(new CreationsRoute());
        stack.Current.ShouldBe(new CreationsRoute());
        stack.CanGoBack.ShouldBeFalse();
        stack.Back().ShouldBeNull();

        stack.Navigate(new SceneNavigation(new BuildRoute(_wormId))).ShouldBe(new BuildRoute(_wormId));
        stack.Navigate(new SceneNavigation(new AchievementRoute("first-steps")));

        stack.CanGoBack.ShouldBeTrue();
        stack.Back().ShouldBe(new BuildRoute(_wormId));
        stack.Current.ShouldBe(new BuildRoute(_wormId));
        stack.Navigate(new SceneNavigation(new ExamplesRoute()));
        stack.ReturnToRoot().ShouldBe(new CreationsRoute());
        stack.Entries.ShouldBe([new CreationsRoute()]);
        new BuildRoute(_wormId).IsSameScene(new BuildRoute(_antId)).ShouldBeTrue();
        new BuildRoute(_wormId).IsSameScene(new ExamplesRoute()).ShouldBeFalse();
    }

    // From Creations → Build(worm) → Achievement: Unique drops earlier entries of the scene whatever their
    // arguments, no keep-current drops the scene navigated from, and the root scene returns to the root.
    [Theory]
    [InlineData(false, true, SceneLaunchMode.Stacked, "Creations,Build worm,Achievement,Build ant")]
    [InlineData(false, true, SceneLaunchMode.Unique, "Creations,Achievement,Build ant")]
    [InlineData(false, false, SceneLaunchMode.Stacked, "Creations,Build worm,Build ant")]
    [InlineData(false, false, SceneLaunchMode.Unique, "Creations,Build ant")]
    [InlineData(true, true, SceneLaunchMode.Stacked, "Creations")]
    [InlineData(true, false, SceneLaunchMode.Unique, "Creations")]
    public void Navigate_KeepsOrDropsEntriesByItsOptions(bool toRoot, bool keepCurrent, SceneLaunchMode launchMode, string expected)
    {
        var stack = new SceneBackStack(new CreationsRoute());
        stack.Navigate(new SceneNavigation(new BuildRoute(_wormId)));
        stack.Navigate(new SceneNavigation(new AchievementRoute("first-steps")));
        SceneRoute target = toRoot ? new CreationsRoute() : new BuildRoute(_antId);

        stack.Navigate(new SceneNavigation(target, keepCurrent, launchMode)).ShouldBe(target);

        stack.Entries.Select(Describe).ShouldBe(expected.Split(','));
    }

    [Fact]
    public void Navigate_WithoutKeepCurrent_FromTheRoot_KeepsTheRoot()
    {
        var stack = new SceneBackStack(new CreationsRoute());

        stack.Navigate(new SceneNavigation(new ExamplesRoute(), KeepCurrent: false));

        stack.Entries.ShouldBe([new CreationsRoute(), new ExamplesRoute()]);
    }

    [Fact]
    public void ReplaceCurrent_GivesTheSceneNewArgumentsThatBackRebuilds_AndRefusesAnotherScene()
    {
        var stack = new SceneBackStack(new CreationsRoute());
        stack.Navigate(new SceneNavigation(new BuildRoute(_wormId)));
        Should.Throw<ArgumentException>(() => stack.ReplaceCurrent(new ExamplesRoute()));
        stack.Entries.ShouldBe([new CreationsRoute(), new BuildRoute(_wormId)]);

        stack.ReplaceCurrent(new BuildRoute(_antId));
        stack.Navigate(new SceneNavigation(new AchievementRoute("first-steps")));

        stack.Back().ShouldBe(new BuildRoute(_antId));
        stack.Entries.ShouldBe([new CreationsRoute(), new BuildRoute(_antId)]);
    }

    // Routes in the app carry only plain values, so a closed scene can be rebuilt from its route.
    [Fact]
    public void AppRoutes_CarryOnlyPlainValues()
    {
        var violations = typeof(AssemblyMarker).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(SceneRoute)))
            .SelectMany(RouteViolations)
            .ToList();

        violations.ShouldBeEmpty();
        RouteViolations(typeof(ListRoute)).ShouldNotBeEmpty();
        RouteViolations(typeof(OpenRoute)).ShouldNotBeEmpty();
    }

    private static string Describe(SceneRoute route) => route switch
    {
        BuildRoute build => build.CreationId == _wormId ? "Build worm" : "Build ant",
        _ => route.GetType().Name.Replace("Route", string.Empty, StringComparison.Ordinal),
    };

    private static IEnumerable<string> RouteViolations(Type route)
    {
        if (!route.IsSealed)
        {
            yield return $"{route.Name} is not sealed; each scene is one sealed record.";
        }

        foreach (var property in route.GetProperties().Where(property => property.DeclaringType != typeof(SceneRoute)))
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (!(type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid) || type == typeof(decimal)))
            {
                yield return $"{route.Name}.{property.Name} is {property.PropertyType.Name}, not a plain value.";
            }
        }
    }
}
