using NodeRunner.Domain;

namespace NodeRunner.ML.Brains;

/// <summary>
/// Explores the ports a trained brain gained (#1180): every child of a generation that opens with an
/// elite redraws the new ports' genes as <see cref="GenerationZero"/> draws them, while the elites
/// keep the trained gait. Which ports are new is read from the best elite's values, so nothing is
/// stored; docs/TRAINING_LOOP.md → "New ports are explored" owns the rules. Stateless and
/// deterministic for a seeded <see cref="Random"/>.
/// </summary>
public static class NewPortExploration
{
    /// <summary>
    /// <paramref name="generation"/> with every child's new-port genes redrawn. The first
    /// <paramref name="eliteCount"/> genomes are the elites, best first, and stay unchanged; the
    /// first one decides which ports are new. Draws nothing when there is no elite or no port is new.
    /// </summary>
    /// <param name="generation">The generation's genomes, elites first, as the genetic algorithm built them.</param>
    /// <param name="eliteCount">How many genomes open the generation unchanged; 0 explores nothing.</param>
    /// <param name="ports">The creature's brain ports.</param>
    /// <param name="disabledGenes">The genome positions training must leave at 0.</param>
    /// <param name="random">The run's random source.</param>
    /// <param name="drawnPositions">By part id, a Piston's drawn length, as for <see cref="GenerationZero.BaseGenome"/>.</param>
    public static double[][] Explore(
        double[][] generation,
        int eliteCount,
        BrainPortLayout ports,
        IReadOnlyCollection<int> disabledGenes,
        Random random,
        IReadOnlyDictionary<int, double>? drawnPositions = null)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(disabledGenes);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegative(eliteCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(eliteCount, generation.Length);
        if (eliteCount == 0)
        {
            return generation;
        }

        if (generation[0].Length != (ports.Inputs.Count + 1) * ports.Outputs.Count)
        {
            throw new ArgumentException("The genomes do not match the ports.", nameof(generation));
        }

        var genes = NewGenes(generation[0], ports, disabledGenes.ToHashSet());
        if (genes.Length == 0)
        {
            return generation;
        }

        var baseGenome = GenerationZero.BaseGenome(ports, drawnPositions);
        var explored = new double[generation.Length][];
        for (var slot = 0; slot < generation.Length; slot++)
        {
            explored[slot] = generation[slot];
            if (slot < eliteCount)
            {
                continue;
            }

            explored[slot] = generation[slot].ToArray();
            foreach (var gene in genes)
            {
                explored[slot][gene] = baseGenome[gene] + GenerationZero.Noise(gene, ports, random);
            }
        }

        return explored;
    }

    // The new ports' genome positions, ascending so the draws come in a fixed order.
    private static int[] NewGenes(double[] elite, BrainPortLayout ports, HashSet<int> disabledGenes)
    {
        var inputs = ports.Inputs.Count;
        var outputs = ports.Outputs.Count;
        var biasStart = inputs * outputs;

        var newParts = Enumerable.Range(0, outputs)
            .Where(o => ports.Outputs[o].Signal == PortSignal.Strength && elite[biasStart + o] == PortSignals.PassiveStrengthBias)
            .Select(o => ports.Outputs[o].PartId)
            .ToHashSet();
        var newOutputs = Enumerable.Range(0, outputs).Where(o => newParts.Contains(ports.Outputs[o].PartId)).ToHashSet();
        var newInputs = Enumerable.Range(0, inputs)
            .Where(i => Enumerable.Range(0, outputs).All(o => elite[(o * inputs) + i] == 0))
            .ToHashSet();

        var genes = new List<int>();
        for (var o = 0; o < outputs; o++)
        {
            for (var i = 0; i < inputs; i++)
            {
                var gene = (o * inputs) + i;
                if ((newOutputs.Contains(o) || newInputs.Contains(i)) && !disabledGenes.Contains(gene))
                {
                    genes.Add(gene);
                }
            }
        }

        genes.AddRange(newOutputs.Order().Select(o => biasStart + o));
        return genes.ToArray();
    }
}
