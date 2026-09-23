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
    // Compare Changes by content, as in ChangeFact, not by list reference.
    // A fact base that is written and read back then equals the original.
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
