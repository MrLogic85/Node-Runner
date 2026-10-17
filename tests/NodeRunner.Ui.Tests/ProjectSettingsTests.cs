using System.Globalization;
using System.Reflection;
using Godot;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NodeRunner.App.Navigation;
using NodeRunner.App.ViewModels;
using NodeRunner.Managers;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// The settings in <c>project.godot</c> that code relies on, the creature physics setup (why:
/// .claude/rules/creature.md rule 3) and the routes that open scenes.
/// </summary>
public sealed class ProjectSettingsTests
{
    private const double _earthGravity = 9.8;
    private const int _minimumSolverIterations = 24;

    [Fact]
    public void Physics_has_earth_gravity_at_the_metre_scale_and_enough_solver_iterations()
    {
        var gravity = ProjectGodot.Setting(ProjectGodot.Lines, "physics", "2d/default_gravity");
        var iterations = ProjectGodot.Setting(ProjectGodot.Lines, "physics", "2d/solver/solver_iterations");

        double.Parse(gravity ?? "980", CultureInfo.InvariantCulture)
            .ShouldBe(_earthGravity * Metres.WorldUnitsPerMetre, tolerance: 1e-9);
        int.Parse(iterations ?? "16", CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(
            _minimumSolverIterations,
            "Fewer solver iterations let beams stretch and rigid triangles flip (#1245).");
    }

    [Fact]
    public void Settings_are_read_from_their_own_section_only()
    {
        string[] lines = ["[rendering]", "2d/default_gravity=1", "[physics]", "", "2d/default_gravity=500", "[input]"];

        ProjectGodot.Setting(lines, "physics", "2d/default_gravity").ShouldBe("500");
        ProjectGodot.Setting(lines, "physics", "2d/solver/solver_iterations").ShouldBeNull();
    }

    [Fact]
    public void Creature_bodies_leave_ccd_off_so_fast_rollers_cannot_launch()
    {
        var violations = CSharpSources.Project
            .Where(source => source.Path.StartsWith("creature/", StringComparison.Ordinal))
            .SelectMany(source => source.Find(SetsCcd))
            .ToList();

        violations.ShouldBeEmpty("Leave ContinuousCd unset on creature bodies (#1171).");
        CSharpSources.Snippet(
                "RigidBody2D Make() => new RigidBody2D { ContinuousCd = RigidBody2D.CcdMode.CastRay }; " +
                "void Set(RigidBody2D body) { body.ContinuousCd = RigidBody2D.CcdMode.CastShape; }")
            .Find(SetsCcd).Count().ShouldBe(2);
    }

    // UiScale.BaseStretch assumes the reference canvas as base size, a fractional stretch and no extra scale.
    [Fact]
    public void Window_stretches_canvas_items_from_the_reference_canvas_and_registers_the_ui_autoloads()
    {
        var settings = File.ReadAllText(Path.Combine(ThemeFile.ProjectRoot, "project.godot")).ReplaceLineEndings("\n");

        settings.ShouldContain($"window/size/viewport_width={UiLayout.CanvasWidth}\n");
        settings.ShouldContain($"window/size/viewport_height={UiLayout.CanvasHeight}\n");
        settings.ShouldContain("window/stretch/mode=\"canvas_items\"");
        settings.ShouldContain("window/stretch/aspect=\"expand\"");
        settings.ShouldNotContain("window/stretch/scale=");
        settings.ShouldNotContain("window/stretch/scale_mode=");
        settings.ShouldContain($"{UiScale.AutoloadName}=\"*res://src/ui/lib/UiScale.cs\"");
        settings.ShouldContain($"{UiSafeArea.AutoloadName}=\"*res://src/ui/lib/UiSafeArea.cs\"");
    }

    // A scene is rebuilt from its route, so a route's arguments must reach it.
    [Fact]
    public void Every_route_opens_an_existing_scene_that_takes_its_arguments()
    {
        var routes = typeof(SceneRoute).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(SceneRoute)) && !type.IsAbstract)
            .ToList();

        routes.ShouldNotBeEmpty();
        routes.Select(RouteProblem).OfType<string>().ShouldBeEmpty();
    }

    private static string? RouteProblem(Type route)
    {
        if (!SceneRouter.ScenePaths.TryGetValue(route, out var path) || !path.StartsWith("res://", StringComparison.Ordinal))
        {
            return $"{route.Name} has no res:// scene in SceneRouter.ScenePaths";
        }

        if (!File.Exists(Path.Combine(ThemeFile.ProjectRoot, path["res://".Length..])))
        {
            return $"{route.Name} opens missing scene {path}";
        }

        if (route.GetProperties().Length == 0)
        {
            return null;
        }

        var scene = path["res://scenes/".Length..];
        var script = SceneNodes.InScene(scene).Single(node => node.IsRoot).Script;
        var rootType = typeof(SceneRouter).Assembly.GetTypes()
            .SingleOrDefault(type => script is not null && type.GetCustomAttribute<ScriptPathAttribute>(inherit: false)?.Path == script);
        return rootType?.IsAssignableTo(typeof(IRoutedScene)) == true
            ? null
            : $"{route.Name} carries arguments but its scene's root is not an IRoutedScene";
    }

    private static bool SetsCcd(SyntaxNode node) => node is AssignmentExpressionSyntax
    {
        Left: IdentifierNameSyntax { Identifier.Text: "ContinuousCd" }
            or MemberAccessExpressionSyntax { Name.Identifier.Text: "ContinuousCd" },
    };
}
