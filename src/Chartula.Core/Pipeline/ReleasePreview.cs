using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Pipeline;

/// <summary>
/// What a preview shows: everything <c>generate</c> would render from, decided without
/// a model call, and what <c>generate</c> would send to the model.
/// Every fact decision - range, labels, filters, categories, visibility - is made before
/// the model, so this is exactly what a paid run starts from, at the cost of the GitHub
/// requests only (#259).
/// </summary>
/// <param name="Facts">Each fact of the release, with the requested audiences it reaches.</param>
/// <param name="Dropped">The changes the filter left out, each with the reason.</param>
/// <param name="Audiences">What each requested audience's rendering would send, in render order.</param>
/// <param name="ThoroughCheck">Whether <c>generate</c> would also run the thorough check on each rendering.</param>
public sealed record ReleasePreview(
    IReadOnlyList<PreviewFact> Facts,
    IReadOnlyList<DroppedChange> Dropped,
    IReadOnlyList<AudiencePreview> Audiences,
    bool ThoroughCheck)
{
    /// <summary>
    /// The model calls <c>generate</c> would make: one rephrasing per audience with
    /// anything to render, and one thorough check for each of those when it is on.
    /// Retries are not counted; they are not known before a call fails.
    /// </summary>
    public int ModelCalls
    {
        get
        {
            int renderings = Audiences.Count(static audience => audience.Entries > 0);
            return ThoroughCheck ? renderings * 2 : renderings;
        }
    }

    public bool Equals(ReleasePreview? other)
        => other is not null
           && ThoroughCheck == other.ThoroughCheck
           && Facts.SequenceEqual(other.Facts)
           && Dropped.SequenceEqual(other.Dropped)
           && Audiences.SequenceEqual(other.Audiences);

    public override int GetHashCode() => HashCode.Combine(ThoroughCheck, Facts.Count, Dropped.Count, Audiences.Count);
}

/// <summary>A fact of the release, and the requested audiences whose rendering carries it.</summary>
/// <param name="Fact">The fact.</param>
/// <param name="Audiences">The requested audiences it reaches, in render order; empty when it reaches none.</param>
public sealed record PreviewFact(ChangeFact Fact, IReadOnlyList<Audience> Audiences)
{
    public bool Equals(PreviewFact? other)
        => other is not null && Fact.Equals(other.Fact) && Audiences.SequenceEqual(other.Audiences);

    public override int GetHashCode() => HashCode.Combine(Fact, Audiences.Count);
}

/// <summary>What one audience's rendering would send to the model.</summary>
/// <param name="Audience">The audience.</param>
/// <param name="Entries">The entries its rendering would carry. With none, it makes no call.</param>
/// <param name="PromptCharacters">
/// The characters of the rephrasing prompt, instructions and facts together, exactly as
/// it would be sent. 0 when there is nothing to render. Characters, not tokens: how many
/// tokens a text is depends on the model's tokenizer, and a wrong estimate reads worse than none.
/// </param>
public sealed record AudiencePreview(Audience Audience, int Entries, int PromptCharacters);
