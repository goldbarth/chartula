namespace Chartula.Core.Budget;

/// <summary>
/// The most a run can consume, per call and in total, known before the first call.
/// A call that turns out not to be needed - an audience that failed, so there is
/// nothing to check - is still counted: an upper bound may overstate, never under.
/// </summary>
public sealed record RunEstimate(IReadOnlyList<CallEstimate> Calls)
{
    public long MaxInputTokens => Calls.Sum(call => call.MaxInputTokens);

    public long MaxOutputTokens => Calls.Sum(call => call.MaxOutputTokens);

    public bool Equals(RunEstimate? other) => other is not null && Calls.SequenceEqual(other.Calls);

    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (CallEstimate call in Calls)
        {
            hash.Add(call);
        }

        return hash.ToHashCode();
    }
}
