using Chartula.Cli.Commands;
using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Chartula.Cli.Terminal;
using Chartula.Core.Llm;
using Chartula.Core.Pipeline;
using Chartula.Infrastructure.History;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli;

/// <summary>
/// Entry point for the Chartula CLI. Dispatches the <c>generate</c> and <c>preview</c>
/// commands, and <c>doctor</c>, which checks the setup. The first two run the same
/// pipeline. Preview stops before the model and writes nothing, and
/// <c>--no-publish</c> limits generate to the local files.
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

        if (args[0] is VersionFlag)
        {
            Console.Out.WriteLine(VersionLine);
            return 0;
        }

        if (!CommandLineArguments.IsCommand(args[0]))
        {
            Console.Error.WriteLine($"Unknown command '{args[0]}'.");
            PrintUsage();
            return 1;
        }

        // Help wins wherever it stands, before anything is checked or started.
        if (args.Skip(1).Any(CommandLineArguments.IsHelp))
        {
            PrintUsage();
            return 0;
        }

        if (CommandLineArguments.Check(args) is { } argumentError)
        {
            Console.Error.WriteLine(argumentError);
            Console.Error.WriteLine("chartula --help lists every command and its options.");
            return 1;
        }

        if (args[0] is DoctorCommand.Name)
        {
            // The report goes to stdout, so stdout decides how it is marked.
            TerminalProfile doctorReport = TerminalProfile.Detect(
                Console.IsOutputRedirected,
                Environment.GetEnvironmentVariables(),
                CommandLineArguments.HasFlag(args, TerminalProfile.PlainFlag),
                Console.OutputEncoding);
            return await DoctorCommand.RunAsync(
                args, Directory.GetCurrentDirectory(), Environment.GetEnvironmentVariables(), Console.Out, profile: doctorReport);
        }

        PipelineMode mode = ParseMode(args[0], args)
                            ?? throw new InvalidOperationException($"'{args[0]}' is a command without a pipeline mode.");

        if (!AudienceSelection.TryParse(args, out IReadOnlyCollection<Audience>? audiences, out string? audienceError))
        {
            Console.Error.WriteLine(audienceError);
            return 1;
        }

        if (!ReleaseStart.TryParse(args, out ReleaseStart start, out string? startError))
        {
            Console.Error.WriteLine(startError);
            return 1;
        }

        // Progress and notices go to stderr and the report to stdout, so each stream
        // decides how its own lines look.
        bool plain = CommandLineArguments.HasFlag(args, TerminalProfile.PlainFlag);
        TerminalProfile progress = TerminalProfile.Detect(
            Console.IsErrorRedirected, Environment.GetEnvironmentVariables(), plain, Console.OutputEncoding);
        TerminalProfile report = TerminalProfile.Detect(
            Console.IsOutputRedirected, Environment.GetEnvironmentVariables(), plain, Console.OutputEncoding);

        IConfiguration configuration;
        ServiceProvider services;
        try
        {
            configuration = BuildConfiguration();
            services = BuildServices(configuration, requireApiKey: mode != PipelineMode.Preview, progress);
        }
        catch (InvalidOperationException ex)
        {
            // A configuration error (e.g. an invalid value in chartula.yaml).
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            return 1;
        }

        using (services)
        {
            Console.Error.WriteLine(StatusMarks.Heading(
                progress.Unicode ? $"chartula · {args[0]}" : $"chartula {args[0]}", progress));
            string directory = Directory.GetCurrentDirectory();
            GitCliRepositoryReader checkout = services.GetRequiredService<GitCliRepositoryReader>();
            ReleaseTarget? target = await ReleaseTarget.ResolveAsync(
                args,
                directory,
                () => checkout.ReadNearestTagAsync(),
                () => checkout.ReadRemoteUrlAsync(ReleaseTarget.Remote),
                Console.Error);
            if (target is null)
            {
                return 1;
            }

            // Write notices to stderr, so they stay out of the changelog when stdout is
            // redirected to a file.
            Console.Error.WriteLine(EndpointNotice.For(configuration));
            if (GitHubTokenNotice.For(configuration) is { } notice)
            {
                Console.Error.WriteLine(notice);
            }

            IReleasePipeline pipeline = services.GetRequiredService<IReleasePipeline>();

            return await ReleaseCommand.RunAsync(
                pipeline,
                mode,
                new ReleaseRequest(target.Tag, target.Repository)
                {
                    Audiences = audiences,
                    Since = start.Since,
                    RangeConfirmed = CommandLineArguments.HasFlag(args, ConsoleRangeGate.YesFlag),
                    ReplacePublished = CommandLineArguments.HasFlag(args, CommandLineArguments.ReplacePublished),
                },
                Console.Out,
                CancellationToken.None,
                report);
        }
    }

    /// <summary>
    /// chartula.yaml refines the behaviour, and environment variables override it.
    /// Without either, the tool runs with sensible defaults.
    /// </summary>
    private static IConfiguration BuildConfiguration()
        => ChartulaConfiguration.Build(Directory.GetCurrentDirectory());

    /// <summary>
    /// The services a run is built from. Building them checks every setting, so
    /// <c>chartula doctor</c> builds them too. A preview makes no model call and needs no
    /// key, and doctor reports the key on a line of its own, so both leave
    /// <paramref name="requireApiKey"/> off.
    /// </summary>
    internal static ServiceProvider BuildServices(
        IConfiguration configuration, bool requireApiKey, TerminalProfile? progress = null)
    {
        return new ServiceCollection()
            .AddChartulaObservability(progress ?? TerminalProfile.Plain)
            .AddChartulaLlm(configuration, requireApiKey)
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
            .AddChartulaOutputs(configuration)
            .AddChartulaReleaseNotes(configuration)
            .AddChartulaPipeline(configuration)
            .BuildServiceProvider();
    }

    /// <summary>
    /// Maps the command and the flags to a pipeline mode, or <c>null</c> for an unknown command.
    /// <c>--no-publish</c> skips only publishing the release notes and writes everything
    /// else as usual. Preview publishes nothing either way.
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
        => CommandLineArguments.IsHelp(arg) || arg is "help";

    private const string VersionFlag = "--version";

    /// <summary>
    /// The version with the commit it was built from, in the form <c>changelog.json</c>
    /// records as <c>toolVersion</c>, so a bug report names the same build a run file does.
    /// </summary>
    internal static string VersionLine => $"chartula {ToolVersion.Informational ?? ToolVersion.Release}";

    /// <summary>
    /// The help text. It is one string instead of a series of writes, so a test can
    /// check it against what the CLI actually accepts.
    /// </summary>
    internal static string Usage =>
        """
        chartula - multi-audience, grounded changelog generator.

        Commands:
          chartula preview  [options]   Show the facts and what generate would send. Free.
          chartula generate [options]   Produce and write the outputs.
          chartula doctor   [options]   Check the setup a run needs, before a run spends anything.
          chartula --version            Print the version and its commit.

        Run it from a checkout of the repository the release belongs to.

        Which release:
          --tag <tag>    The release tag. Default: the nearest tag reachable from HEAD.
          --repo <o/n>   The GitHub repository, as owner/name. Default: read from
                         the 'origin' remote.
          --since <ref>  Render the commits after this tag or commit, up to the
                         release tag. Default: after the previous tag, or from the
                         first commit for a first tag.
          --yes          Confirm a first tag or a large range up front, for a run
                         without a terminal to ask on.

        What a run writes:
          --audience <a> Render only this audience: technical, customer or product.
                         Repeat it, or separate them with commas. Default:
                         technical and customer; product renders only when named.
                         An output whose audience was not rendered is not written.
          --no-publish   Write every file, but publish no GitHub release notes.
          --replace-published
                         Replace the notes of a release that is already
                         published. Without it, generate stops before its first
                         model call when the release is published.

        How it looks:
          --plain        Plain lines only: no spinner, no colour, no symbols beyond
                         ASCII. Also the default without a terminal, in CI, with
                         TERM=dumb, and without colour with NO_COLOR set.

        doctor takes --tag, --repo and --plain; --no-publish and --replace-published are for
        generate only.
        An option a command does not take stops before anything starts.

        """;

    private static void PrintUsage() => Console.Out.Write(Usage);
}
