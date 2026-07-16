namespace Chartula.Core.Facts;

/// <summary>
/// The complete, deterministic fact base for a release: the tag plus one
/// <see cref="ChangeFact"/> per included change. This is the grounded source the
/// audience renderings and the faithfulness checks all build on.
/// </summary>
/// <param name="Tag">The release tag the facts belong to.</param>
/// <param name="Changes">The included changes, as index-card facts.</param>
public sealed record FactBase(string Tag, IReadOnlyList<ChangeFact> Changes)
{
    // As with ChangeFact: the changes are compared by content, not by list identity, so
    // a fact base written and read back equals the one it came from.
    public bool Equals(FactBase? other)
        => other is not null && Tag == other.Tag && Changes.SequenceEqual(other.Changes);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Tag);
        foreach (ChangeFact change in Changes)
        {
            hash.Add(change);
        }

        return hash.ToHashCode();
    }
}
