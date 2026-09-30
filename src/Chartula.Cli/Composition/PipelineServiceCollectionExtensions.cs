using Chartula.Cli.Commands;
using Chartula.Cli.Configuration;
using Chartula.Core.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for the release pipeline.
/// <see cref="ReleasePipeline"/> depends only on the ports the other compositions register;
/// the commands depend only on <see cref="IReleasePipeline"/>.
/// The range gate asks on the console: stderr, so a prompt stays out of a changelog
/// redirected from stdout, and only when stdin is a terminal a person can answer on.
/// Register this after all the step services.
/// </summary>
internal static class PipelineServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaPipeline(this IServiceCollection services, IConfiguration configuration)
    {
        LargeRangeRule rule = new(ReadConfirmAboveCommits(configuration));
        services.AddSingleton(rule);
        services.AddSingleton<IReleaseRangeGate>(
            new ConsoleRangeGate(Console.In, Console.Error, interactive: !Console.IsInputRedirected, rule));
        services.AddSingleton<IReleasePipeline, ReleasePipeline>();
        return services;
    }

    // Reject an unparsable or negative value loudly. Falling back to the default would
    // let a range the operator meant to confirm run without asking.
    private static int ReadConfirmAboveCommits(IConfiguration configuration)
    {
        string? raw = configuration[$"{RangeOptions.SectionName}:{nameof(RangeOptions.ConfirmAboveCommits)}"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return LargeRangeRule.DefaultCommitThreshold;
        }

        if (!int.TryParse(raw, out int value) || value < 0)
        {
            throw new InvalidOperationException(
                $"Invalid range.confirmAboveCommits '{raw}'. Expected a whole number, 0 or more.");
        }

        return value;
    }
}
