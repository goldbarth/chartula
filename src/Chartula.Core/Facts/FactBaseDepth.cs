namespace Chartula.Core.Facts;

/// <summary>
/// How much source material feeds each <see cref="ChangeFact"/>, so the fact base
/// fits a team's PR style. Deeper modes carry more, at the cost of more material
/// for the LLM to rephrase.
/// </summary>
public enum FactBaseDepth
{
    /// <summary>Only the title; no description, no linked issues.</summary>
    TitleOnly,

    /// <summary>Title and description; no linked issues. The default.</summary>
    TitleAndDescription,

    /// <summary>Title, description, and linked issues.</summary>
    TitleDescriptionAndIssues,
}
