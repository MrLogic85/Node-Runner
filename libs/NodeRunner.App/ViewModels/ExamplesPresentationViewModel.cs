using NodeRunner.App.Services;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The Examples screen: one Creations card per shipped example, with the line on what is new in it
/// under the name and Copy as its only action.
/// </summary>
public sealed class ExamplesPresentationViewModel
{
    public ExamplesPresentationViewModel(IReadOnlyList<CreationExample>? examples = null)
    {
        Cards = (examples ?? CreationExamples.All)
            .Select(example => new CreationCardPresentation(
                example.Id,
                example.Name,
                example.Creature,
                example.WhatIsNew,
                CreationCardPresentation.ThumbnailTextFor(example.Creature),
                Training: null,
                CanOpen: false,
                CanDuplicate: true,
                CanDelete: false))
            .ToArray();
    }

    public IReadOnlyList<CreationCardPresentation> Cards { get; }

    /// <summary>The example to copy, or null when <paramref name="id"/> is not on the screen.</summary>
    public Guid? RequestCopy(Guid id) => Cards.Any(card => card.Id == id) ? id : null;
}
