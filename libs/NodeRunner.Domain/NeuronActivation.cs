namespace NodeRunner.Domain;

/// <summary>How a <see cref="NeuronDef"/> turns its summed input into its value. An input neuron passes its port's value through.</summary>
public enum NeuronActivation
{
    Identity,
    Tanh,
    Sigmoid,
    Relu,

    /// <summary><c>tanh(z / k)</c>, with k <see cref="PortSignals.TanhDamping"/>: a Memory cell's target.</summary>
    DampedTanh,

    /// <summary><c>sigmoid(z / k)</c>, with k <see cref="PortSignals.SigmoidDamping"/>: a Memory cell's write.</summary>
    DampedSigmoid,
}
