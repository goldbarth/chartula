namespace Chartula.Core.Llm;

/// <summary>
/// One passage a faithfulness check flagged, and the fact it is about.
/// </summary>
/// <remarks>
/// The pull request number is what makes a flag usable. The reader goes straight to the
/// fact instead of searching the release for it, and flags from several runs group by the
/// fact they concern instead of by how alike their wording is.
/// The thorough check names the number. <see cref="Faithfulness.ThoroughFaithfulnessChecker"/>
/// keeps it only when the fact base has that pull request, so past the checker a number
/// here is always a fact of the release.
/// </remarks>
/// <param name="Text">What was flagged, and why.</param>
/// <param name="PullRequest">
/// The pull request whose fact the flagged passage rephrases.
/// <c>null</c> when the flag concerns no single fact: a number or name no fact contains,
/// a claim about the release as a whole, a fact without a pull request, or a check that
/// could not be evaluated.
/// </param>
public sealed record FaithfulnessFlag(string Text, int? PullRequest = null)
{
    /// <summary>The flag as a reviewer reads it: the fact first, when there is one.</summary>
    public override string ToString() => PullRequest is { } number ? $"#{number}: {Text}" : Text;
}
