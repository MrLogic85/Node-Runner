using System.Reflection;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Godot saves an exported enum in a <c>.tscn</c> as its number, so a member inserted mid-enum
/// silently changes every scene that saved a later one. <c>SceneSavedEnums.txt</c> lists each
/// exported enum member with its number; members may be added, never renumbered. Append new ones
/// with <c>NODE_RUNNER_UPDATE_ENUMS=1 dotnet test tests/NodeRunner.Ui.Tests --filter SceneSavedEnumsTests</c>.
/// </summary>
public sealed class SceneSavedEnumsTests
{
    private const string _updateVariable = "NODE_RUNNER_UPDATE_ENUMS";

    private static readonly string _path =
        Path.Combine(SceneNodes.FindRepositoryRoot(), "tests", "NodeRunner.Ui.Tests", "SceneSavedEnums.txt");

    [Fact]
    public void ExportedEnums_KeepTheNumberOfEveryMember()
    {
        var current = ExportedEnumMembers();
        var saved = Recorded();

        saved.Except(current).ShouldBeEmpty(
            "A saved enum member was renumbered, renamed or removed, which changes what scenes saved with it mean. " +
            "Add new members at the end instead, or update every scene and SceneSavedEnums.txt by hand.");
        if (Environment.GetEnvironmentVariable(_updateVariable) == "1")
        {
            File.WriteAllLines(_path, current);
        }

        current.Except(Recorded()).ShouldBeEmpty($"Record new enum members: run the UI tests with {_updateVariable}=1.");
    }

    private static List<string> Recorded() => File.Exists(_path) ? [.. File.ReadAllLines(_path).Where(line => line.Length > 0)] : [];

    private static List<string> ExportedEnumMembers() =>
    [
        .. typeof(UiButton).Assembly.GetTypes()
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(property => property.IsDefined(typeof(Godot.ExportAttribute)))
            .SelectMany(property => SavedTypes(property.PropertyType))
            .Where(type => type.IsEnum && type.Assembly == typeof(UiButton).Assembly)
            .Distinct()
            .SelectMany(type => Enum.GetValues(type).Cast<Enum>()
                .Select(value => $"{type.FullName}.{value}={Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture)}"))
            .Distinct()
            .Order(StringComparer.Ordinal),
    ];

    // An enum also saves as numbers inside an exported array or collection.
    private static IEnumerable<Type> SavedTypes(Type type) =>
        type.IsArray ? SavedTypes(type.GetElementType()!)
        : type.IsGenericType ? type.GetGenericArguments().SelectMany(SavedTypes)
        : [type];
}
