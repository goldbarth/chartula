using Chartula.Cli.Composition;
using Chartula.Core.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli;

/// <summary>
/// Entry point for the Chartula CLI.
/// This is a scaffold - the real command surface (generate, preview, ...)
/// arrives with Phase 1 of the roadmap.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        using ServiceProvider services = new ServiceCollection()
            .AddChartulaLlm(configuration)
            .AddChartulaHistory()
            .AddChartulaPullRequests(configuration)
            .AddChartulaCuration()
            .AddChartulaLabelRules(configuration)
            .AddChartulaFilter(configuration)
            .AddChartulaFactBase(configuration)
            .AddChartulaGeneration()
            .AddChartulaFaithfulness(configuration)
            .AddChartulaReview(configuration)
            .AddChartulaOutputs()
            .BuildServiceProvider();

        // The rest of the code depends only on the interface, never on a provider.
        IChangelogModel model = services.GetRequiredService<IChangelogModel>();

        Console.WriteLine("Chartula - multi-audience, grounded changelog generator.");
        Console.WriteLine("Early development. Nothing to generate yet - see the roadmap.");
        Console.WriteLine(
            $"LLM seam ready: {nameof(IChangelogModel)} -> {model.GetType().Name} over a swappable provider.");
        return 0;
    }
}
