using Chartula.Cli.Configuration;

namespace Chartula.Cli.Composition;

/// <summary>
/// The model and thinking mode the thorough check runs with, each taken from
/// <c>faithfulness</c> when set there and from <c>llm</c> otherwise. Resolved in one
/// place so the request, the run header and the provenance cannot disagree about it.
/// </summary>
/// <param name="Model">The model id the check asks.</param>
/// <param name="Thinking">The thinking mode the check asks for.</param>
/// <param name="ModelKey">The setting the model came from, for messages.</param>
/// <param name="ThinkingKey">The setting the thinking mode came from, for messages.</param>
internal sealed record ThoroughCheckModel(string Model, ThinkingMode Thinking, string ModelKey, string ThinkingKey)
{
    public static ThoroughCheckModel Resolve(LlmOptions llm, FaithfulnessOptions faithfulness)
    {
        bool ownModel = !string.IsNullOrWhiteSpace(faithfulness.Model);
        bool ownThinking = !string.IsNullOrWhiteSpace(faithfulness.Thinking);
        return new(
            ownModel ? faithfulness.Model!.Trim() : llm.Model,
            ThinkingModeParser.Parse(ownThinking ? faithfulness.Thinking : llm.Thinking),
            ownModel ? "faithfulness.model" : "llm.model",
            ownThinking ? "faithfulness.thinking" : "llm.thinking");
    }

    public static FaithfulnessOptions Read(Microsoft.Extensions.Configuration.IConfiguration configuration)
        => Microsoft.Extensions.Configuration.ConfigurationBinder.Get<FaithfulnessOptions>(
               configuration.GetSection(FaithfulnessOptions.SectionName))
           ?? new FaithfulnessOptions();

    /// <summary>Whether the check runs with anything other than what renders.</summary>
    public bool DiffersFrom(LlmOptions llm)
        => Model != llm.Model || Thinking != ThinkingModeParser.Parse(llm.Thinking);
}
