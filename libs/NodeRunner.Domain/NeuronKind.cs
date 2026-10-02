namespace NodeRunner.Domain;

/// <summary>What a <see cref="NeuronDef"/> stands for: a part's input port, a part's output port, or a hidden neuron.</summary>
public enum NeuronKind
{
    Input,
    Output,
    Hidden,
}
