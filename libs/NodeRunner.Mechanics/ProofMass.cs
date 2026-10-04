using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>The accelerometer's proof mass state in its sensor frame (X along, Y up).</summary>
public readonly record struct ProofMass(Vector2D Displacement, Vector2D Velocity);
