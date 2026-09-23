namespace Chartula.Core.Generation;

/// <summary>
/// The parts of one rendering entry that are decided without a model.
/// </summary>
/// <param name="Id">The id the fact is sent with. The model returns the entry's text under the same id.</param>
/// <param name="Group">The heading the entry stands under.</param>
/// <param name="IsBreaking">Whether the entry carries the breaking marker.</param>
/// <param name="Reference">The pull request reference at the end of a technical entry, or <c>null</c> when it has none.</param>
public sealed record PlannedEntry(int Id, string Group, bool IsBreaking, string? Reference);
