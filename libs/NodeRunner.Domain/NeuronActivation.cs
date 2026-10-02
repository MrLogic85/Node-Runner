namespace NodeRunner.Domain;

/// <summary>How a <see cref="NeuronDef"/> turns its summed input into its value. An input neuron passes its port's value through.</summary>
public enum NeuronActivation
{
    Identity,
    Tanh,
    Sigmoid,
    Relu,
}
