namespace Chartula.Core.Generation;

/// <summary>
/// One entry of a rendering as far as it is decided without a model.
/// </summary>
/// <param name="Id">The id the fact is sent with, and the one its text comes back under.</param>
/// <param name="Group">The heading the entry stands under.</param>
/// <param name="IsBreaking">Whether the entry carries the breaking marker.</param>
/// <param name="Reference">What closes a technical entry, or <c>null</c> when it has none.</param>
public sealed record PlannedEntry(int Id, string Group, bool IsBreaking, string? Reference);
