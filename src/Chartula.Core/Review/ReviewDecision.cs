namespace Chartula.Core.Review;

/// <summary>What the reviewer decided about a presented rendering.</summary>
public enum ReviewOutcome
{
    /// <summary>The text is approved as-is.</summary>
    Approved,

    /// <summary>The text was edited; the edited version is used.</summary>
    Edited,
}

/// <summary>
/// The reviewer's decision: the text to write, and whether it was approved as-is
/// or edited.
/// </summary>
/// <param name="Outcome">Whether the text was approved or edited.</param>
/// <param name="Text">The text to write (original when approved, edited otherwise).</param>
public sealed record ReviewDecision(ReviewOutcome Outcome, string Text)
{
    public static ReviewDecision Approve(string text) => new(ReviewOutcome.Approved, text);

    public static ReviewDecision Edit(string text) => new(ReviewOutcome.Edited, text);
}
