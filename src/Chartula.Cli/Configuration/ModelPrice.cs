namespace Chartula.Cli.Configuration;

/// <summary>What a model's tokens cost, in US dollars per million.</summary>
/// <param name="InputPerMillionTokens">The price of a million input tokens.</param>
/// <param name="OutputPerMillionTokens">The price of a million output tokens, thinking included.</param>
internal sealed record ModelPrice(decimal InputPerMillionTokens, decimal OutputPerMillionTokens)
{
    // Anthropic's first-party list prices for the models docs/configuration.md names.
    // Only those: an unknown model gets no price rather than a guessed one, and the
    // estimate stays in tokens. Prices change - cost.inputPerMillionTokens and
    // cost.outputPerMillionTokens override this table.
    private static readonly Dictionary<string, ModelPrice> Anthropic = new(StringComparer.Ordinal)
    {
        ["claude-opus-5"] = new(5m, 25m),
        ["claude-opus-4-8"] = new(5m, 25m),
        ["claude-sonnet-5"] = new(2m, 10m),
        ["claude-haiku-4-5"] = new(1m, 5m),
    };

    /// <summary>The list price of an Anthropic model, or <c>null</c> when it is not known.</summary>
    public static ModelPrice? For(LlmOptions llm)
        => llm.Provider == LlmProviderParser.ToConfigurationValue(LlmProvider.Anthropic)
           && string.IsNullOrWhiteSpace(llm.BaseUrl)
           && Anthropic.TryGetValue(llm.Model, out ModelPrice? price)
            ? price
            : null;

    /// <summary>The cost of <paramref name="inputTokens"/> in and <paramref name="outputTokens"/> out.</summary>
    public decimal Cost(long inputTokens, long outputTokens)
        => ((inputTokens * InputPerMillionTokens) + (outputTokens * OutputPerMillionTokens)) / 1_000_000m;
}
