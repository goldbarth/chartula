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
/// <c>preview</c> commands; both run the same pipeline, but preview writes nothing
/// and <c>--no-publish</c> keeps generate to the local files.
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

        PipelineMode? mode = ParseMode(args[0], args);

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
            .AddChartulaObservability()
            .AddChartulaLlm(configuration)
            .AddChartulaHistory()
            .AddChartulaPullRequests(configuration)
            .AddChartulaCuration()
            .AddChartulaLabelRules(configuration)
            .AddChartulaFilter(configuration)
            .AddChartulaFactBase(configuration)
            .AddChartulaCategories(configuration)
            .AddChartulaGeneration()
            .AddChartulaFaithfulness(configuration)
            .AddChartulaReview(configuration)
            .AddChartulaOutputs()
            .AddChartulaReleaseNotes(configuration)
            .AddChartulaPipeline()
            .BuildServiceProvider();
    }

    /// <summary>
    /// Maps the command word and the flags onto a pipeline mode, or <c>null</c> for
    /// an unknown command. Producing the record and announcing the release are
    /// separable acts: <c>--no-publish</c> drops the second one and leaves
    /// everything else as it was. Preview publishes nothing either way.
    /// </summary>
    internal static PipelineMode? ParseMode(string command, IReadOnlyList<string> args)
        => command switch
        {
            "generate" => CommandLineArguments.HasFlag(args, "--no-publish")
                ? PipelineMode.GenerateWithoutPublishing
                : PipelineMode.Generate,
            "preview" => PipelineMode.Preview,
            _ => null,
        };

    private static bool IsHelp(string arg)
        => arg is "-h" or "--help" or "help";

    /// <summary>
    /// The help text. It is a string rather than a series of writes so a test can
    /// hold it to what the CLI actually accepts.
    /// </summary>
    internal static string Usage =>
        """
        Chartula - multi-audience, grounded changelog generator.

        Usage:
          chartula preview  --tag <release-tag> --repo <owner/name>   Show what would be produced (dry run).
          chartula generate --tag <release-tag> --repo <owner/name>   Produce and write the outputs.

        Options:
          --no-publish   Write changelog.json and CHANGELOG.md, but publish no release notes.

        """;

    private static void PrintUsage() => Console.Out.Write(Usage);
}
