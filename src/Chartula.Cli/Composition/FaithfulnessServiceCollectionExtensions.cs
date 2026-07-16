using Chartula.Core.Faithfulness;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for faithfulness checks. The rule-based check is pure domain
/// logic with no LLM dependency and always runs; the pipeline depends only on
/// <see cref="IRuleBasedFaithfulnessChecker"/>.
/// </summary>
internal static class FaithfulnessServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaFaithfulness(this IServiceCollection services)
    {
        services.AddSingleton<IRuleBasedFaithfulnessChecker, RuleBasedFaithfulnessChecker>();
        return services;
    }
}
