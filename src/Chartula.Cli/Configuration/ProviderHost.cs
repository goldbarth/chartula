namespace Chartula.Cli.Configuration;

/// <summary>
/// The hosts known to serve one provider's API, checked against the provider a run
/// is configured for. A base URL for <c>anthropic</c> exists for proxies and gateways,
/// so an unknown host cannot be refused; but a host that belongs to another provider
/// is never a gateway for this one, and the key sent to it has reached a third party
/// and has to be rotated. The run header names the mismatch, and nothing stopped a
/// run on it until this check - which is why it refuses rather than warns.
/// </summary>
/// <remarks>
/// Only hosts whose owner is certain and that serve no other provider's dialect are
/// listed. Several hosted endpoints serve both dialects under one host, so a longer
/// list would refuse working setups.
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
    /// other than <paramref name="provider"/>. <paramref name="providerConfigured"/>
    /// says whether <c>llm.provider</c> was set at all: an endpoint set without it is
    /// how the mismatch happens, because the provider silently stays on its default.
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
