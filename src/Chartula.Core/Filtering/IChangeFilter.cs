using Chartula.Core.Curation;

namespace Chartula.Core.Filtering;

/// <summary>
/// Decides whether a change belongs in the changelog, only from its category and the
/// label rules. Internal changes such as chores are dropped by default.
/// </summary>
public interface IChangeFilter
{
    bool ShouldInclude(ReleaseChange change);
}
