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
        FaithfulnessOptions faithfulness = configuration.GetSection(FaithfulnessOptions.SectionName).Get<FaithfulnessOptions>()
                                           ?? new FaithfulnessOptions();
        FactBaseOptions factBase = configuration.GetSection(FactBaseOptions.SectionName).Get<FactBaseOptions>()
                                   ?? new FactBaseOptions();

        RunProvenance ProvenanceOf(IServiceProvider sp)
            => Provenance(
                sp.GetRequiredService<LlmOptions>(),
                faithfulness.Thorough,
                FactBaseDepthParser.Parse(factBase.Depth));

        services.AddSingleton<IChangelogJsonWriter>(
            sp => new FileChangelogJsonWriter(Directory.GetCurrentDirectory(), ProvenanceOf(sp)));
        // The run record says what the run was made with in the same terms as the
        // file it wrote, so the two can be matched without a second source.
        services.AddSingleton<IRunRecordWriter>(
            sp => new FileRunRecordWriter(Directory.GetCurrentDirectory(), ProvenanceOf(sp)));
        services.AddSingleton<IChangelogMarkdownWriter>(
            _ => new FileChangelogMarkdownWriter(Directory.GetCurrentDirectory()));
        services.AddSingleton<ICustomerPageWriter>(
            _ => new FileCustomerPageWriter(Directory.GetCurrentDirectory()));
        return services;
    }

    // Everything here is known before the run starts: what wrote the file, which
    // instructions and model it rendered with, and the settings that move a run's
    // cost and output most, so two files can be compared without anyone's memory.
    internal static RunProvenance Provenance(LlmOptions llm, bool thoroughCheck, FactBaseDepth depth)
        => new(
            ToolVersion.Informational,
            llm.Provider,
            llm.Model,
            ChangelogPromptBuilder.PromptHash,
            ThinkingModeParser.Name(ThinkingModeParser.Parse(llm.Thinking)),
            thoroughCheck,
            FactBaseDepthParser.Name(depth));
}
