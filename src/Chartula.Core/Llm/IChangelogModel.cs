namespace Chartula.Core.Llm;

/// <summary>
/// The LLM operations the Chartula pipeline needs, expressed in Chartula's own
/// terms rather than raw chat completions. The pipeline depends only on this
/// interface, never on a concrete provider.
/// </summary>
/// <remarks>
/// The single shipped implementation is backed by a provider-agnostic
/// <c>Microsoft.Extensions.AI.IChatClient</c>, so swapping the model provider is
/// a composition-root change and never touches the pipeline. The prompts these
/// operations use are refined in the prompt-design issue; this interface only
/// fixes the seam.
/// </remarks>
public interface IChangelogModel
{
    /// <summary>
    /// Rephrases each grounded fact into the text of one entry for the requested
    /// audience. The model rephrases only; it never introduces facts, and it never
    /// decides where an entry stands - that is put around its text in code.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The answer could not be read as entries. There is no partial rendering to
    /// return in that case, so it is not reported as one.
    /// </exception>
    Task<RenderedEntries> RephraseAsync(
        RephraseRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the generated output is fully backed by the facts and
    /// reports any claims that are not.
    /// </summary>
    Task<FaithfulnessReport> CheckFaithfulnessAsync(
        FaithfulnessRequest request,
        CancellationToken cancellationToken = default);
}
