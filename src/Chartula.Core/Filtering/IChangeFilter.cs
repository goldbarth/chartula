using Chartula.Core.Curation;

namespace Chartula.Core.Filtering;

/// <summary>
/// Decides whether a change belongs in the changelog, only from its category and the
/// label rules. Internal changes such as chores are dropped by default.
/// </summary>
public interface IChangeFilter
{
    /// <summary>
    /// Why <paramref name="change"/> is dropped, naming the setting that decided it, or
    /// <c>null</c> when it stays. A preview shows the reason, so a configuration can be
    /// checked before anything is paid for.
    /// </summary>
    string? DropReason(ReleaseChange change);

    bool ShouldInclude(ReleaseChange change) => DropReason(change) is null;
}
