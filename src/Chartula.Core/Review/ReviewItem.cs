using Chartula.Core.Llm;

namespace Chartula.Core.Review;

/// <summary>
/// One generated rendering presented for review: the audience, the text, and any
/// passages the faithfulness checks flagged for a human to look at.
/// </summary>
/// <param name="Audience">The audience this rendering is for.</param>
/// <param name="Text">The generated changelog text.</param>
/// <param name="Flags">Findings from the faithfulness checks, empty if clean.</param>
public sealed record ReviewItem(Audience Audience, string Text, IReadOnlyList<string> Flags);
