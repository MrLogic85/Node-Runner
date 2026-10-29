using System.Reflection;
using System.Text.Json.Serialization;

namespace NodeRunner.Domain.Tests;

public sealed class PartKindsTests
{
    [Fact]
    public void All_HasOneEntryPerPartKind_SelectsEveryElement_AndNamesEverySensorKindOnce()
    {
        PartKinds.All.Select(info => info.Kind).ShouldBe(Enum.GetValues<PartKind>());
        EveryPartKind.Creature.Parts.Select(part => part.PartKind).Distinct().ShouldBe(Enum.GetValues<PartKind>(), ignoreOrder: true);
        PartKinds.All.Select(info => info.Element).Distinct().ShouldBe(Enum.GetValues<CreatureElementKind>(), ignoreOrder: true);
        PartKinds.All.Where(info => info.Sensor is not null).Select(info => info.Sensor!.Value).ShouldBe(Enum.GetValues<SensorKind>(), ignoreOrder: true);
        Enum.GetValues<SensorKind>().ShouldAllBe(sensor => PartKinds.Of(sensor).Element == CreatureElementKind.Sensor);
        foreach (var part in EveryPartKind.Creature.Parts)
        {
            (PartKinds.Of(part.PartKind).Slot is not null).ShouldBe(part is IJointPartDef, $"{part.PartKind}");
        }

        EveryPartKind.Creature.HingeParts.ShouldAllBe(part => PartKinds.SlotOf(part) == JointSlot.HingePart);
        var creature = EveryPartKind.Creature;
        var links = LinkRef.All(creature.Beams, creature.Pistons, creature.Springs).Select(link => link.Id).ToHashSet();
        creature.Parts.ShouldAllBe(part => PartKinds.IsLink(part) == links.Contains(part.Id));
    }

    // A new part list on CreatureDef must hold parts the common contract reaches.
    [Fact]
    public void EverySavedPartList_HoldsPartDefs_AndThoseOnAJoint_AreJointParts()
    {
        var partLists = typeof(CreatureDef).GetProperties()
            .Where(property => property.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .Where(property => property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            .Select(property => property.PropertyType.GetGenericArguments()[0])
            .ToArray();

        partLists.Length.ShouldBe(13);
        foreach (var type in partLists)
        {
            typeof(IPartDef).IsAssignableFrom(type).ShouldBeTrue($"{type.Name} is not an IPartDef.");
            var onAJoint = type != typeof(NodeDef) && type.GetProperty(nameof(IJointPartDef.NodeId)) is not null;
            typeof(IJointPartDef).IsAssignableFrom(type).ShouldBe(onAJoint, $"{type.Name} sits on a joint exactly when it is an IJointPartDef.");
        }
    }
}
