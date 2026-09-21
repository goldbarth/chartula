using Chartula.Cli.Commands;
using Chartula.Cli.Configuration;
using Chartula.Core.Budget;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for what a run may spend. The estimator is the domain's; the
/// prices, the ceiling and where the estimate is printed are decided here.
/// </summary>
internal static class BudgetServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaBudget(this IServiceCollection services, IConfiguration configuration)
    {
        CostOptions cost = CostOptions.Read(configuration);

        services.AddSingleton<RunEstimator>();
        services.AddSingleton<IRunBudget>(sp => new ConsoleRunBudget(sp.GetRequiredService<LlmOptions>(), cost, Console.Error));
        return services;
    }
}
