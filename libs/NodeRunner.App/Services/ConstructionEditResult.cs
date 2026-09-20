using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public sealed record ConstructionEditResult(CreationDef? UpdatedCreation, string StatusMessage)
{
    public bool ShouldApplyLive => UpdatedCreation is not null;
}
