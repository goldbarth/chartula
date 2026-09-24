using Chartula.Cli.Configuration;
using Chartula.Core.Facts;
using Chartula.Core.Observability;
using Chartula.Core.Prompting;
using Chartula.Core.Serialization;
using Chartula.Infrastructure.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for release outputs. The concrete writers (files in the
/// working directory) are chosen here; the pipeline depends only on the ports.
/// </summary>
internal static class OutputServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaOutputs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        FaithfulnessOptions faithfulness = ThoroughCheckModel.Read(configuration);
        FactBaseOptions factBase = configuration.GetSection(FactBaseOptions.SectionName).Get<FactBaseOptions>()
                                   ?? new FactBaseOptions();

        RunProvenance ProvenanceOf(IServiceProvider sp)
            => Provenance(
                sp.GetRequiredService<LlmOptions>(),
                faithfulness,
                FactBaseDepthParser.Parse(factBase.Depth));

        services.AddSingleton<IChangelogJsonWriter>(
            sp => new FileChangelogJsonWriter(Directory.GetCurrentDirectory(), ProvenanceOf(sp)));
        // The run record uses the same provenance as changelog.json, so the two files
        // can be matched without a second source.
        services.AddSingleton<IRunRecordWriter>(
            sp => new FileRunRecordWriter(Directory.GetCurrentDirectory(), ProvenanceOf(sp)));
        services.AddSingleton<IChangelogMarkdownWriter>(
            _ => new FileChangelogMarkdownWriter(Directory.GetCurrentDirectory()));
        services.AddSingleton<ICustomerPageWriter>(
            _ => new FileCustomerPageWriter(Directory.GetCurrentDirectory()));
        return services;
    }

    // Everything here is known before the run starts: the tool version, the prompt
    // hash and model used for rendering, and the settings that affect a run's cost and
    // output most. So two files can be compared without relying on anyone's memory.
    // The check's model and thinking mode are recorded as resolved, even when they
    // repeat the rendering's, so reading a file never requires the configuration.
    internal static RunProvenance Provenance(LlmOptions llm, FaithfulnessOptions faithfulness, FactBaseDepth depth)
    {
        ThoroughCheckModel? check = faithfulness.Thorough ? ThoroughCheckModel.Resolve(llm, faithfulness) : null;
        return new(
            ToolVersion.Informational,
            llm.Provider,
            llm.Model,
            ChangelogPromptBuilder.PromptHash,
            ThinkingModeParser.Name(ThinkingModeParser.Parse(llm.Thinking)),
            faithfulness.Thorough,
            FactBaseDepthParser.Name(depth),
            check?.Model,
            check is null ? null : ThinkingModeParser.Name(check.Thinking));
    }
}
