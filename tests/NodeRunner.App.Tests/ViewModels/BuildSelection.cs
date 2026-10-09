using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>Test setup for a selection of exactly one part, as a tap on it with nothing selected leaves.</summary>
internal static class BuildSelection
{
    public static void SelectOnly(this BuildViewModel build, CreatureElementKind kind, int id) =>
        build.ReplaceSelection(PartSet.Of(new CreatureElementSelection(kind, id)));
}
