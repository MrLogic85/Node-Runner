using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// Where every selected joint was when a selection drag started, and the
/// <see cref="Pivot"/> that Rotate and Scale turn about (see
/// <see cref="BuildViewModel.SnapshotSelection"/>). Every drag frame is computed from it, so nothing drifts.
/// </summary>
public sealed record SelectionSnapshot(IReadOnlyDictionary<int, Vector2D> Positions, Vector2D Pivot);
