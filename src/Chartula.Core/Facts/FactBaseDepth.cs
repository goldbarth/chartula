namespace Chartula.Core.Facts;

/// <summary>
/// How much source material feeds each <see cref="ChangeFact"/>.
/// Teams pick the depth that fits their PR style.
/// Deeper modes carry more detail, but also give the LLM more material to rephrase.
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
