using System.Globalization;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartNamesTests
{
    [Fact]
    public void Default_EveryKind_FitsThePartLimit_UpToNinetyNineOfAKind()
    {
        const int count = 99;
        var nodes = Enumerable.Range(1, count + 1).Select(id => new NodeDef(id, new Vector2D(id, 0))).ToArray();
        var beams = Enumerable.Range(0, count).Select(index => new BeamDef(1000 + index, index + 1, index + 2)).ToArray();
        var pistons = Enumerable.Range(0, count).Select(index => new PistonDef(2000 + index, index + 1, index + 2)).ToArray();
        var springs = Enumerable.Range(0, count).Select(index => new SpringDef(3000 + index, index + 1, index + 2)).ToArray();
        var sensors = Enum.GetValues<SensorKind>().Select((kind, index) => new SensorDef(4000 + index, 1000 + index, kind)).ToArray();
        int[] partIds = [.. nodes.Select(node => node.Id), .. beams.Select(beam => beam.Id), .. pistons.Select(piston => piston.Id), .. springs.Select(spring => spring.Id), .. sensors.Select(sensor => sensor.Id)];

        foreach (var partId in partIds)
        {
            var name = PartNames.Default(nodes, beams, sensors, pistons, springs, partId);
            var shown = string.Format(CultureInfo.InvariantCulture, name.Message, [.. name.Args]);
            shown.Length.ShouldBeLessThanOrEqualTo(NameLimits.Part, shown);
        }
    }
}
