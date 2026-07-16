using Chartula.Cli.Configuration;
using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Facts;
using Chartula.Core.Filtering;
using Chartula.Core.Labeling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for the fact base. Reads the depth from the
/// <c>Chartula:FactBase</c> section; the builder is pure domain logic and the
/// pipeline depends only on <see cref="IFactBaseBuilder"/>. Requires the curation,
/// label, and filter services to be registered first.
/// </summary>
internal static class FactBaseServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaFactBase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        FactBaseOptions options = configuration.GetSection(FactBaseOptions.SectionName).Get<FactBaseOptions>()
                                  ?? new FactBaseOptions();

        // Depth is a config value, not a service - pass it into the builder.
        FactBaseDepth depth = FactBaseDepthParser.Parse(options.Depth);
        services.AddSingleton<IFactBaseBuilder>(sp => new FactBaseBuilder(
            sp.GetRequiredService<IReleaseChangeResolver>(),
            sp.GetRequiredService<IChangeFilter>(),
            sp.GetRequiredService<IChangeCategorizer>(),
            sp.GetRequiredService<ILabelRulePolicy>(),
            depth));
        return services;
    }
}
