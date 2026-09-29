using System.Reflection;
using Godot;
using NodeRunner.App.Navigation;
using NodeRunner.Managers;

namespace NodeRunner.Ui.Tests;

public sealed class SceneRouterTests
{
    public static TheoryData<Type> Routes() => [.. typeof(SceneRoute).Assembly.GetTypes()
        .Where(type => type.IsSubclassOf(typeof(SceneRoute)) && !type.IsAbstract)];

    [Fact]
    public void Routes_exist() => Routes().Count.ShouldBeGreaterThan(0);

    [Theory]
    [MemberData(nameof(Routes))]
    public void Route_opens_an_existing_scene(Type route)
    {
        SceneRouter.ScenePaths.TryGetValue(route, out var path)
            .ShouldBeTrue($"{route.Name} has no scene in SceneRouter.ScenePaths");
        path!.ShouldStartWith("res://");
        File.Exists(Path.Combine(ThemeFile.ProjectRoot, path["res://".Length..]))
            .ShouldBeTrue($"{route.Name} opens missing scene {path}");
    }

    // A scene is rebuilt from its route, so a route's arguments must reach it.
    [Theory]
    [MemberData(nameof(Routes))]
    public void Route_with_arguments_opens_a_routed_scene(Type route)
    {
        if (route.GetProperties().Length == 0)
        {
            return;
        }

        var scene = SceneRouter.ScenePaths[route]["res://scenes/".Length..];
        var script = SceneNodes.InScene(scene).Single(node => node.IsRoot).Script;
        script.ShouldNotBeNull($"{scene} has no root script");
        var rootType = typeof(SceneRouter).Assembly.GetTypes()
            .Single(type => type.GetCustomAttribute<ScriptPathAttribute>(inherit: false)?.Path == script);
        rootType.IsAssignableTo(typeof(IRoutedScene))
            .ShouldBeTrue($"{route.Name} carries arguments but {rootType.Name} is not an IRoutedScene");
    }
}
