using System.Reflection;
using System.Text.Json.Serialization;

namespace NodeRunner.Domain.Tests;

public sealed class PartKindsTests
{
    [Fact]
    public void All_HasOneEntryPerPartKind_InPartKindOrder()
    {
        PartKinds.All.Select(info => info.Kind).ShouldBe(Enum.GetValues<PartKind>());
    }

    [Fact]
    public void All_SelectsEveryElementKind_AndNamesEverySensorKindOnce()
    {
        PartKinds.All.Select(info => info.Element).Distinct().ShouldBe(Enum.GetValues<CreatureElementKind>(), ignoreOrder: true);
        PartKinds.All.Where(info => info.Sensor is not null).Select(info => info.Sensor!.Value).ShouldBe(Enum.GetValues<SensorKind>(), ignoreOrder: true);
        foreach (var sensor in Enum.GetValues<SensorKind>())
        {
            PartKinds.Of(sensor).Element.ShouldBe(CreatureElementKind.Sensor);
        }
    }

    // A new part list on CreatureDef must hold parts the common contract reaches (#1221).
    [Fact]
    public void EverySavedPartList_HoldsPartDefs_AndThoseOnAJoint_AreJointParts()
    {
        var partLists = typeof(CreatureDef).GetProperties()
            .Where(property => property.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .Where(property => property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            .Select(property => property.PropertyType.GetGenericArguments()[0])
            .ToArray();

        partLists.Length.ShouldBe(12);
        foreach (var type in partLists)
        {
            typeof(IPartDef).IsAssignableFrom(type).ShouldBeTrue($"{type.Name} is not an IPartDef.");
            var onAJoint = type != typeof(NodeDef) && type.GetProperty(nameof(IJointPartDef.NodeId)) is not null;
            typeof(IJointPartDef).IsAssignableFrom(type).ShouldBe(onAJoint, $"{type.Name} sits on a joint exactly when it is an IJointPartDef.");
        }
    }
}
