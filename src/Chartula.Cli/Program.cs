using Chartula.Cli.Commands;
using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli;

/// <summary>
/// Entry point for the Chartula CLI. Dispatches the <c>generate</c> and
/// <c>preview</c> commands; both run the same pipeline, but preview writes nothing.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return 0;
        }

        PipelineMode? mode = args[0] switch
        {
            "generate" => PipelineMode.Generate,
            "preview" => PipelineMode.Preview,
            _ => null,
        };

        if (mode is null)
        {
            Console.Error.WriteLine($"Unknown command '{args[0]}'.");
            PrintUsage();
            return 1;
        }

        string? tag = CommandLineArguments.GetOption(args, "--tag");
        if (string.IsNullOrWhiteSpace(tag))
        {
            Console.Error.WriteLine("Missing required option --tag <release-tag>.");
            return 1;
        }

        if (!ReleaseCommand.TryParseRepository(
                CommandLineArguments.GetOption(args, "--repo"), out RepositoryCoordinates repository))
        {
            Console.Error.WriteLine("Missing or invalid option --repo <owner/name>.");
            return 1;
        }

        ServiceProvider services;
        try
        {
            services = BuildServices();
        }
        catch (InvalidOperationException ex)
        {
            // A configuration error (e.g. an invalid value in chartula.yaml).
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            return 1;
        }

        using (services)
        {
            IReleasePipeline pipeline = services.GetRequiredService<IReleasePipeline>();

            return await ReleaseCommand.RunAsync(
                pipeline, mode.Value, new ReleaseRequest(tag, repository), Console.Out, CancellationToken.None);
        }
    }

    private static ServiceProvider BuildServices()
    {
        // chartula.yaml refines behavior; environment variables override it. The
        // tool runs with sensible defaults when neither is present.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddChartulaYaml(Directory.GetCurrentDirectory())
            .AddEnvironmentVariables()
            .Build();

        return new ServiceCollection()
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
            .AddChartulaReleaseNotes(configuration)
            .AddChartulaPipeline()
            .BuildServiceProvider();
    }

    private static bool IsHelp(string arg)
        => arg is "-h" or "--help" or "help";

    private static void PrintUsage()
    {
        Console.WriteLine("Chartula - multi-audience, grounded changelog generator.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  chartula preview  --tag <release-tag> --repo <owner/name>   Show what would be produced (dry run).");
        Console.WriteLine("  chartula generate --tag <release-tag> --repo <owner/name>   Produce and write the outputs.");
    }
}
