using Anthropic;
using Chartula.Cli.Configuration;
using Chartula.Core.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for the LLM seam. This is the only place that knows which
/// concrete provider backs <see cref="IChangelogModel"/>; swapping providers is
/// a change here and nowhere else.
/// </summary>
internal static class LlmServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaLlm(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        LlmOptions options = ReadOptions(configuration);

        services.AddSingleton(options);
        services.AddSingleton(sp => CreateChatClient(options, configuration));
        services.AddSingleton<IChangelogModel, ChatModel>();
        return services;
    }

    private static LlmOptions ReadOptions(IConfiguration configuration) => new()
    {
        Provider = configuration[$"{LlmOptions.SectionName}:Provider"] ?? "anthropic",
        Model = configuration[$"{LlmOptions.SectionName}:Model"] ?? "claude-opus-4-8",
        ApiKeyEnvironmentVariable =
            configuration[$"{LlmOptions.SectionName}:ApiKeyEnvironmentVariable"] ?? "ANTHROPIC_API_KEY",
    };

    private static IChatClient CreateChatClient(LlmOptions options, IConfiguration configuration)
    {
        if (!string.Equals(options.Provider, "anthropic", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"LLM provider '{options.Provider}' is not supported yet. Only 'anthropic' is implemented.");
        }

        // Read the key by name; never hardcode it. Absence is tolerated here so the
        // CLI still starts - the provider surfaces a clear auth error on first use.
        string? apiKey = configuration[options.ApiKeyEnvironmentVariable];
        AnthropicClient client = new() { ApiKey = apiKey };
        return client.AsIChatClient(options.Model);
    }
}
