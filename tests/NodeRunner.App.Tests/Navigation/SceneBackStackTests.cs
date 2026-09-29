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

    [Fact]
    public void New_stack_is_on_its_root_and_cannot_go_back()
    {
        var stack = new SceneBackStack(new CreationsRoute());

        stack.Current.ShouldBe(new CreationsRoute());
        stack.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void Navigate_pushes_the_route_and_returns_it_to_build_from()
    {
        var stack = new SceneBackStack(new CreationsRoute());

        var opened = stack.Navigate(new SceneNavigation(new BuildRoute(_wormId)));

        opened.ShouldBe(new BuildRoute(_wormId));
        stack.Entries.ShouldBe([new CreationsRoute(), new BuildRoute(_wormId)]);
    }

    [Fact]
    public void Back_returns_the_previous_route_with_its_arguments()
    {
        var stack = new SceneBackStack(new CreationsRoute());
        stack.Navigate(new SceneNavigation(new BuildRoute(_wormId)));
        stack.Navigate(new SceneNavigation(new AchievementRoute("first-steps")));

        stack.Back().ShouldBe(new BuildRoute(_wormId));
        stack.Current.ShouldBe(new BuildRoute(_wormId));
    }

    [Fact]
    public void Back_on_the_root_returns_null_and_keeps_the_root()
    {
        var stack = new SceneBackStack(new CreationsRoute());

        stack.Back().ShouldBeNull();
        stack.Entries.ShouldBe([new CreationsRoute()]);
    }

    [Fact]
    public void Without_keep_current_Back_skips_the_scene_navigated_from()
    {
        var stack = new SceneBackStack(new CreationsRoute());
        stack.Navigate(new SceneNavigation(new ExamplesRoute()));

        stack.Navigate(new SceneNavigation(new BuildRoute(_wormId), KeepCurrent: false));

        stack.Entries.ShouldBe([new CreationsRoute(), new BuildRoute(_wormId)]);
        stack.Back().ShouldBe(new CreationsRoute());
    }

    [Fact]
    public void Without_keep_current_the_root_still_stays()
    {
        var stack = new SceneBackStack(new CreationsRoute());

        stack.Navigate(new SceneNavigation(new ExamplesRoute(), KeepCurrent: false));

        stack.Entries.ShouldBe([new CreationsRoute(), new ExamplesRoute()]);
    }

    [Fact]
    public void Stacked_keeps_earlier_entries_of_the_same_scene()
    {
        var stack = new SceneBackStack(new CreationsRoute());
        stack.Navigate(new SceneNavigation(new BuildRoute(_wormId)));
        stack.Navigate(new SceneNavigation(new AchievementRoute("first-steps")));

        stack.Navigate(new SceneNavigation(new BuildRoute(_antId)));

        stack.Entries.ShouldBe(
        [
            new CreationsRoute(),
            new BuildRoute(_wormId),
            new AchievementRoute("first-steps"),
            new BuildRoute(_antId),
        ]);
    }

    [Fact]
    public void Unique_removes_earlier_entries_of_the_same_scene_whatever_their_arguments()
    {
        var stack = new SceneBackStack(new CreationsRoute());
        stack.Navigate(new SceneNavigation(new BuildRoute(_wormId)));
        stack.Navigate(new SceneNavigation(new AchievementRoute("first-steps")));

        stack.Navigate(new SceneNavigation(new BuildRoute(_antId), LaunchMode: SceneLaunchMode.Unique));

        stack.Entries.ShouldBe([new CreationsRoute(), new AchievementRoute("first-steps"), new BuildRoute(_antId)]);
    }

    [Fact]
    public void Unique_without_keep_current_drops_both_the_current_and_earlier_entries()
    {
        var stack = new SceneBackStack(new CreationsRoute());
        stack.Navigate(new SceneNavigation(new BuildRoute(_wormId)));
        stack.Navigate(new SceneNavigation(new ExamplesRoute()));

        stack.Navigate(new SceneNavigation(new BuildRoute(_antId), KeepCurrent: false, LaunchMode: SceneLaunchMode.Unique));

        stack.Entries.ShouldBe([new CreationsRoute(), new BuildRoute(_antId)]);
    }

    [Theory]
    [InlineData(true, SceneLaunchMode.Stacked)]
    [InlineData(false, SceneLaunchMode.Unique)]
    public void Navigating_to_the_root_scene_returns_to_the_root(bool keepCurrent, SceneLaunchMode launchMode)
    {
        var stack = new SceneBackStack(new CreationsRoute());
        stack.Navigate(new SceneNavigation(new ExamplesRoute()));
        stack.Navigate(new SceneNavigation(new BuildRoute(_wormId)));

        stack.Navigate(new SceneNavigation(new CreationsRoute(), keepCurrent, launchMode))
            .ShouldBe(new CreationsRoute());

        stack.Entries.ShouldBe([new CreationsRoute()]);
        stack.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void Return_to_root_clears_every_scene_above_it()
    {
        var stack = new SceneBackStack(new CreationsRoute());
        stack.Navigate(new SceneNavigation(new BuildRoute(_wormId)));
        stack.Navigate(new SceneNavigation(new AchievementRoute("first-steps")));

        stack.ReturnToRoot().ShouldBe(new CreationsRoute());
        stack.Entries.ShouldBe([new CreationsRoute()]);
    }

    [Fact]
    public void Entries_cannot_be_changed_from_outside()
    {
        var stack = new SceneBackStack(new CreationsRoute());

        stack.Entries.ShouldBeAssignableTo<System.Collections.ObjectModel.ReadOnlyCollection<SceneRoute>>();
    }

    [Fact]
    public void Same_scene_ignores_arguments()
    {
        new BuildRoute(_wormId).IsSameScene(new BuildRoute(_antId)).ShouldBeTrue();
        new BuildRoute(_wormId).IsSameScene(new ExamplesRoute()).ShouldBeFalse();
    }

    // Routes in the app carry only plain values, so a closed scene can be rebuilt from its route.
    [Fact]
    public void App_routes_carry_only_plain_values()
    {
        var violations = typeof(AssemblyMarker).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(SceneRoute)))
            .SelectMany(RouteViolations)
            .ToList();

        violations.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(typeof(BuildRoute))]
    [InlineData(typeof(AchievementRoute))]
    [InlineData(typeof(CreationsRoute))]
    public void Plain_value_route_passes(Type route) => RouteViolations(route).ShouldBeEmpty();

    private sealed record ListRoute(List<Guid> CreationIds) : SceneRoute;

    private abstract record OpenRoute : SceneRoute;

    [Theory]
    [InlineData(typeof(ListRoute))]
    [InlineData(typeof(OpenRoute))]
    public void Live_or_open_route_is_flagged(Type route) => RouteViolations(route).ShouldNotBeEmpty();

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
