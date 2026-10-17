using System.Text.Json.Serialization;

namespace NodeRunner.Domain;

/// <summary>Player progress across Creations. A save that leaves out the flag is rejected, not read as fresh.</summary>
public sealed record ProgressionDef([property: JsonRequired] bool DefaultCreationsSeeded = false);
