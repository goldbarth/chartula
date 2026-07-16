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
    /// Rephrases the grounded facts into prose tailored for the requested
    /// audience. The model rephrases only; it never introduces facts.
    /// </summary>
    Task<string> RephraseAsync(
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
