namespace Chartula.Core.Llm;

/// <summary>
/// The LLM operations the Chartula pipeline needs, in Chartula's own terms instead
/// of raw chat completions.
/// The pipeline depends only on this interface, never on a concrete provider.
/// </summary>
/// <remarks>
/// The single shipped implementation is backed by a provider-agnostic
/// <c>Microsoft.Extensions.AI.IChatClient</c>, so swapping the model provider is
/// a composition-root change and never touches the pipeline.
/// </remarks>
public interface IChangelogModel
{
    /// <summary>
    /// Rephrases each grounded fact into the text of one entry for the requested audience.
    /// The model only rephrases. It never introduces facts and never decides where an
    /// entry stands: code places the text afterwards.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The answer could not be read as entries. A partial rendering does not exist in
    /// that case, so none is returned.
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
