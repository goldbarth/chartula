namespace Chartula.Cli.Configuration;

/// <summary>
/// The hosts known to serve one provider's API, checked against the configured provider.
/// A base URL for <c>anthropic</c> exists for proxies and gateways, so an unknown host
/// cannot be refused.
/// But a host that belongs to another provider is never a gateway for this one. A key
/// sent there has reached a third party and must be rotated.
/// The run header showed the mismatch, but nothing stopped the run before this check.
/// That is why it refuses instead of warning.
/// </summary>
/// <remarks>
/// The list holds only hosts with a certain owner that serve no other provider's dialect.
/// Several hosted endpoints serve both dialects under one host, so a longer list would
/// refuse working setups.
/// </remarks>
internal static class ProviderHost
{
    private static readonly Dictionary<string, (LlmProvider Provider, string Owner)> KnownHosts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["api.anthropic.com"] = (LlmProvider.Anthropic, "Anthropic"),
            ["api.openai.com"] = (LlmProvider.OpenAiCompatible, "OpenAI"),
        };

    /// <summary>
    /// Throws when <paramref name="baseUrl"/> is a host known to belong to a provider
    /// other than <paramref name="provider"/>.
    /// <paramref name="providerConfigured"/> says whether <c>llm.provider</c> was set at all.
    /// The mismatch typically happens when an endpoint is set without it, because the
    /// provider silently keeps its default.
    /// </summary>
    public static void RequireOwnedBy(
        LlmProvider provider,
        bool providerConfigured,
        string? baseUrl,
        string apiKeyEnvironmentVariable)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? endpoint)
            // A trailing dot names the same host and would otherwise slip past the lookup.
            || !KnownHosts.TryGetValue(endpoint.Host.TrimEnd('.'), out (LlmProvider Provider, string Owner) known)
            || known.Provider == provider)
        {
            return;
        }

        string configured = LlmProviderParser.ToConfigurationValue(provider);
        string expected = LlmProviderParser.ToConfigurationValue(known.Provider);
        string providerSetting = providerConfigured
            ? $"llm.provider is '{configured}'"
            : $"llm.provider is not set and defaults to '{configured}'";

        throw new InvalidOperationException(
            $"{LlmOptions.BaseUrlVariable} '{baseUrl}' is {known.Owner}'s API, but {providerSetting}, " +
            $"so the key in {apiKeyEnvironmentVariable} would be sent to {known.Owner}. " +
            $"For {known.Owner}'s API, set llm.provider to {expected} " +
            $"(in chartula.yaml, or as Chartula__Llm__Provider); otherwise unset {LlmOptions.BaseUrlVariable}.");
    }
}
