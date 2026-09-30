using Chartula.Core.Curation;

namespace Chartula.Core.Facts;

/// <summary>
/// The fact base of a release together with the changes the filter dropped from it.
/// The dropped changes are no facts and never reach an output file; only a preview
/// shows them, with the reason, so a filter setting can be checked before a run is paid for.
/// </summary>
/// <param name="Facts">The fact base.</param>
/// <param name="Dropped">The changes left out, in release order, each with the reason.</param>
public sealed record CuratedRelease(FactBase Facts, IReadOnlyList<DroppedChange> Dropped)
{
    public bool Equals(CuratedRelease? other)
        => other is not null && Facts.Equals(other.Facts) && Dropped.SequenceEqual(other.Dropped);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Facts);
        foreach (DroppedChange dropped in Dropped)
        {
            hash.Add(dropped);
        }

        return hash.ToHashCode();
    }
}

/// <summary>A change the filter left out of the fact base.</summary>
/// <param name="Change">The change as it was resolved.</param>
/// <param name="Reason">Why it was left out, naming the setting that decided it.</param>
public sealed record DroppedChange(ReleaseChange Change, string Reason);
