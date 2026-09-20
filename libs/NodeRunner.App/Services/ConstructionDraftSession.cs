using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>State handed back to the host when construction enters an unsaved draft.</summary>
public sealed record ConstructionDraftSession(CreatureDef Creature, Guid? ActiveCreationId, string StatusMessage);
