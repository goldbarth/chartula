using Chartula.Cli.Configuration;
using Chartula.Core.Faithfulness;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for faithfulness checks. The rule-based check is pure domain
/// logic that always runs; the thorough check is the second LLM pass, on by
/// default and toggled via the <c>Chartula:Faithfulness</c> section. The pipeline
/// depends only on the interfaces. Requires the LLM services first.
/// </summary>
internal static class FaithfulnessServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaFaithfulness(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        FaithfulnessOptions options = configuration.GetSection(FaithfulnessOptions.SectionName).Get<FaithfulnessOptions>()
                                      ?? new FaithfulnessOptions();

        services.AddSingleton<IRuleBasedFaithfulnessChecker, RuleBasedFaithfulnessChecker>();
        services.AddSingleton(new ThoroughFaithfulnessOptions(options.Thorough));
        services.AddSingleton<IThoroughFaithfulnessChecker, ThoroughFaithfulnessChecker>();
        return services;
    }
}
