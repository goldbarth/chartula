using Chartula.Cli.Configuration;
using Chartula.Core.Labeling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for label rules. Reads the <c>Chartula:Labels</c> section and
/// builds the domain <see cref="LabelRules"/>; the pipeline depends only on
/// <see cref="ILabelRulePolicy"/>. With no configuration the rules do nothing.
/// </summary>
internal static class LabelServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaLabelRules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        LabelOptions options = configuration.GetSection(LabelOptions.SectionName).Get<LabelOptions>()
                               ?? new LabelOptions();

        LabelRules rules = LabelRules.From(options.Exclude, options.Category, options.OnlyIncludeLabeled);

        services.AddSingleton(rules);
        services.AddSingleton<ILabelRulePolicy, LabelRulePolicy>();
        return services;
    }
}
