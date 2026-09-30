namespace Chartula.Core.Facts;

/// <summary>
/// How much of each change the model reads.
/// Teams pick the depth that fits their PR style.
/// The deeper mode carries more detail, but also gives the LLM more material to rephrase.
/// <para>
/// A third value, <c>title-description-and-issues</c>, read no issue: it only filled
/// <see cref="ChangeFact.LinkedIssues"/>, which every depth now does from the text it
/// reads (#258). It is still accepted, as <see cref="TitleAndDescription"/>.
/// </para>
/// </summary>
public enum FactBaseDepth
{
    /// <summary>Only the title.</summary>
    TitleOnly,

    /// <summary>Title and description. The default.</summary>
    TitleAndDescription,
}
