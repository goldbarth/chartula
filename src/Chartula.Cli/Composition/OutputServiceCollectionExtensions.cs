using System.Reflection;
using Chartula.Cli.Configuration;
using Chartula.Core.Prompting;
using Chartula.Core.Serialization;
using Chartula.Infrastructure.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for release outputs. The concrete writers (files in the
/// working directory) are chosen here; the pipeline depends only on the ports.
/// </summary>
internal static class OutputServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaOutputs(this IServiceCollection services)
    {
        services.AddSingleton<IChangelogJsonWriter>(sp => new FileChangelogJsonWriter(
            Directory.GetCurrentDirectory(), Provenance(sp.GetRequiredService<LlmOptions>())));
        services.AddSingleton<IChangelogMarkdownWriter>(
            _ => new FileChangelogMarkdownWriter(Directory.GetCurrentDirectory()));
        services.AddSingleton<ICustomerPageWriter>(
            _ => new FileCustomerPageWriter(Directory.GetCurrentDirectory()));
        return services;
    }

    // Everything here is known before the run starts: what wrote the file and which
    // instructions and model it rendered with.
    internal static RunProvenance Provenance(LlmOptions llm)
        => new(
            typeof(OutputServiceCollectionExtensions).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            llm.Provider,
            llm.Model,
            ChangelogPromptBuilder.PromptHash);
}
