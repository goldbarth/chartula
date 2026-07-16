using Chartula.Core.Curation;

namespace Chartula.Core.Filtering;

/// <summary>
/// Decides whether a change belongs in the changelog, from its category and label
/// rules - never from guesswork. Internal/chore changes are dropped by default.
/// </summary>
public interface IChangeFilter
{
    bool ShouldInclude(ReleaseChange change);
}
