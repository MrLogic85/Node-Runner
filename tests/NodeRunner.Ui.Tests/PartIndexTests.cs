using NodeRunner.Creature;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NodeRunner.Domain;
using NodeRunner.Domain.Tests;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Training finds each part's visual and anchor in a <see cref="PartIndex{TValue}"/> by the part's id: every
/// selectable kind resolves without a kind switch, a repeated id is refused, and a missing or mistyped one is
/// not found. <c>Creature.IndexParts</c>, which fills it, reads every part list of the creature.
/// </summary>
public sealed class PartIndexTests
{
    private static readonly CreatureDef _everyKind = EveryPartKind.Creature;

    [Fact]
    public void Every_selectable_kind_resolves_by_its_id()
    {
        var index = IndexOf(_everyKind.Parts);

        EveryPartKind.HoldsEveryKind.ShouldBeTrue();
        Enum.GetValues<CreatureElementKind>().ShouldBeSubsetOf(_everyKind.Parts.Select(part => CreatureElementSelection.Of(part).Kind));
        index.Missing(_everyKind.Parts).ShouldBeEmpty();
        foreach (var part in _everyKind.Parts)
        {
            index.TryOf(CreatureElementSelection.Of(part), out var entry).ShouldBeTrue();
            entry.ShouldBe((part, $"{part.PartKind} {part.Id}"));
        }
    }

    [Fact]
    public void A_part_id_is_indexed_once()
    {
        var index = IndexOf(_everyKind.Parts);

        Should.Throw<ArgumentException>(() => index.Add(new BrakeDef(9, 1), "again")).Message.ShouldContain("Part id 9");
    }

    [Fact]
    public void A_missing_or_mistyped_id_is_not_found()
    {
        var index = IndexOf(_everyKind.Parts.Where(part => part.Id != 21));

        index.TryOf(new CreatureElementSelection(CreatureElementKind.Stepper, 21), out _).ShouldBeFalse();
        index.TryOf(new CreatureElementSelection(CreatureElementKind.Servo, 15), out _).ShouldBeFalse();
        index.TryOf(new CreatureElementSelection(CreatureElementKind.VelocityMotor, 15), out _).ShouldBeTrue();
        index.Missing(_everyKind.Parts).ShouldBe(["Stepper 21"]);
    }

    // A part kind IndexParts leaves out would show unselectable, so each typed part list of CreatureDef is read there.
    [Fact]
    public void Training_indexes_every_part_list()
    {
        var partLists = typeof(CreatureDef).GetProperties()
            .Where(property => property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                && property.PropertyType.GetGenericArguments()[0] is { IsClass: true } part
                && part.IsAssignableTo(typeof(IPartDef)))
            .Select(property => property.Name)
            .ToList();
        var indexParts = CSharpSources.Project.Single(source => source.Path == "creature/Creature.Selection.cs").Tree.GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.Text == "IndexParts");
        var read = indexParts.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .Where(access => access.Expression is IdentifierNameSyntax { Identifier.Text: "definition" })
            .Select(access => access.Name.Identifier.Text)
            .ToHashSet();

        partLists.ShouldContain(nameof(CreatureDef.Steppers));
        partLists.Where(list => !read.Contains(list)).ShouldBeEmpty("Index every part of the kind in Creature.IndexParts.");
    }

    private static PartIndex<string> IndexOf(IEnumerable<IPartDef> parts)
    {
        var index = new PartIndex<string>();
        foreach (var part in parts)
        {
            index.Add(part, $"{part.PartKind} {part.Id}");
        }

        return index;
    }
}
