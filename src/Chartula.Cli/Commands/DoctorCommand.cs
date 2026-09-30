using System.Collections;
using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Chartula.Core.Categorization;
using Chartula.Core.PullRequests;
using Chartula.Infrastructure.History;
using Chartula.Infrastructure.PullRequests;
using Chartula.Infrastructure.Releases;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Commands;

/// <summary>
/// <c>chartula doctor</c>: checks what a run needs before a run spends anything (#254).
/// A first run fails on the setup more often than on the release, and each failure showed
/// up on its own, often after the run had started.
/// Each check asks what a run asks, through the code a run uses, so it fails where a run
/// would and in the run's words.
/// It writes nothing and publishes nothing. The endpoint check is the one that costs: a
/// model call of a few tokens, because only an answer proves the key, the model id and
/// the endpoint together.
/// It names variables, never their values.
/// </summary>
internal static class DoctorCommand
{
    public const string Name = "doctor";

    /// <summary>The merged pull requests the title check reads: enough to show a habit, in one request.</summary>
    private const int RecentPullRequests = 30;

    /// <summary>The ceiling on the answer to an endpoint check. The answer only has to arrive.</summary>
    private const int EndpointCheckOutputTokens = 16;

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        string directory,
        IDictionary environment,
        TextWriter output,
        DoctorTransports? transports = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(output);

        Report report = new(output);
        output.WriteLine($"Checking the setup for a run in {directory}");
        output.WriteLine();

        // git and the checkout.
        GitExecutable? git = CheckGit(report, environment);
        GitCliRepositoryReader? checkout = await CheckCheckoutAsync(report, git, directory, cancellationToken);
        (string? tag, string? tagCommit) = await CheckTagAsync(report, checkout, args, directory, cancellationToken);
        RepositoryCoordinates? repository = await CheckRepositoryAsync(report, checkout, args, directory, cancellationToken);

        // The configuration and the model.
        IConfiguration? configuration = CheckConfiguration(report, git, directory, environment);
        LlmOptions? llm = CheckModel(report, configuration);
        await CheckEndpointAsync(report, configuration, llm, transports?.Model, cancellationToken);

        // GitHub.
        await CheckGitHubAsync(report, configuration, repository, tag, tagCommit, transports?.GitHub, cancellationToken);

        output.WriteLine();
        output.WriteLine(report.Failed
            ? "A run would stop. Fix what failed above, then run chartula doctor again."
            : report.Warned
                ? "A run would start. The warnings above do not stop it, but read them before generate."
                : "A run would start.");
        return report.Failed ? 1 : 0;
    }

    private static GitExecutable? CheckGit(Report report, IDictionary environment)
    {
        try
        {
            GitExecutable git = GitExecutable.Resolve(Variable(environment, "PATH"));
            report.Ok("git", git.Path);
            return git;
        }
        catch (InvalidOperationException ex)
        {
            report.Fail("git", ex.Message);
            return null;
        }
    }

    private static async Task<GitCliRepositoryReader?> CheckCheckoutAsync(
        Report report, GitExecutable? git, string directory, CancellationToken cancellationToken)
    {
        if (git is null)
        {
            report.Skip("checkout", "needs git");
            return null;
        }

        GitCliRepositoryReader checkout = new(git, directory);
        if (!await checkout.IsCheckoutAsync(cancellationToken))
        {
            report.Fail("checkout",
                $"'{directory}' is not a git checkout. Run chartula in a checkout of the repository the release belongs to.");
            return null;
        }

        // A shallow clone still answers the other checks, so the checkout is passed on.
        if (await checkout.IsShallowAsync(cancellationToken))
        {
            report.Fail("checkout", $"""
                '{directory}' is a shallow clone: its history ends at the fetch depth, so a run cannot read where a release starts.
                {GitCliCommitReader.FetchFullHistory.TrimEnd()}
                """);
        }
        else
        {
            report.Ok("checkout", $"{directory}, with its full history");
        }

        return checkout;
    }

    private static async Task<(string? Tag, string? Commit)> CheckTagAsync(
        Report report,
        GitCliRepositoryReader? checkout,
        IReadOnlyList<string> args,
        string directory,
        CancellationToken cancellationToken)
    {
        if (checkout is null)
        {
            report.Skip("tag", "needs a checkout");
            return (null, null);
        }

        Resolved<string> tag = await ReleaseTarget.ResolveTagAsync(
            args, directory, () => checkout.ReadNearestTagAsync(cancellationToken));
        if (tag.Value is null)
        {
            report.Fail("tag", tag.Message!);
            return (null, null);
        }

        if (await checkout.ReadCommitAsync(tag.Value, cancellationToken) is not { } commit)
        {
            report.Fail("tag",
                $"'{tag.Value}' is not a tag in '{directory}'. Fetch the tags with git fetch --tags, or pass another --tag.");
            return (null, null);
        }

        report.Ok("tag", tag.Message is null
            ? $"{tag.Value}, from --tag"
            : $"{tag.Value}, the nearest tag reachable from HEAD. Pass --tag to choose another.");
        return (tag.Value, commit);
    }

    private static async Task<RepositoryCoordinates?> CheckRepositoryAsync(
        Report report,
        GitCliRepositoryReader? checkout,
        IReadOnlyList<string> args,
        string directory,
        CancellationToken cancellationToken)
    {
        if (checkout is null && CommandLineArguments.GetOption(args, "--repo") is null)
        {
            report.Skip("repository", "needs a checkout, or --repo");
            return null;
        }

        Resolved<RepositoryCoordinates> repository = await ReleaseTarget.ResolveRepositoryAsync(
            args,
            directory,
            () => checkout?.ReadRemoteUrlAsync(ReleaseTarget.Remote, cancellationToken) ?? Task.FromResult<string?>(null));
        if (repository.Value is null)
        {
            report.Fail("repository", repository.Message!);
            return null;
        }

        string name = $"{repository.Value.Owner}/{repository.Value.Name}";
        report.Ok("repository", repository.Message is null
            ? $"{name}, from --repo"
            : $"{name}, from the '{ReleaseTarget.Remote}' remote. Pass --repo to choose another.");
        return repository.Value;
    }

    private static IConfiguration? CheckConfiguration(
        Report report, GitExecutable? git, string directory, IDictionary environment)
    {
        try
        {
            string? file = ChartulaYamlConfiguration.FindConfigFile(directory);
            IConfiguration configuration = ChartulaConfiguration.Build(
                new ConfigurationBuilder().AddChartulaYaml(directory), environment);

            // Building a run's services checks every setting the way a run does. They
            // include the git readers, so without git there is nothing more to learn here.
            if (git is not null)
            {
                using (Program.BuildServices(configuration, requireApiKey: false))
                {
                }
            }

            report.Ok("config", file ?? $"no chartula.yaml in {directory}, so the defaults apply");
            return configuration;
        }
        catch (InvalidOperationException ex)
        {
            report.Fail("config", ex.Message);
            return null;
        }
    }

    private static LlmOptions? CheckModel(Report report, IConfiguration? configuration)
    {
        if (configuration is null)
        {
            report.Skip("model", "needs a configuration that reads");
            return null;
        }

        LlmOptions llm = LlmServiceCollectionExtensions.ReadOptions(configuration);
        string model = $"{llm.Provider} at {llm.BaseUrl ?? "its default endpoint"}, model {llm.Model}";
        string variable = llm.ApiKeyEnvironmentVariable;

        if (LlmServiceCollectionExtensions.MissingApiKey(configuration) is { } missing)
        {
            report.Fail("model", $"{model}\n{missing}");
            return null;
        }

        report.Ok("model", string.IsNullOrWhiteSpace(configuration[variable])
            ? $"{model}, no key in {variable}: a local server needs none"
            : $"{model}, key from {variable}");
        return llm;
    }

    private static async Task CheckEndpointAsync(
        Report report,
        IConfiguration? configuration,
        LlmOptions? llm,
        HttpMessageHandler? transport,
        CancellationToken cancellationToken)
    {
        if (configuration is null || llm is null)
        {
            report.Skip("endpoint", "needs the model settings and a key");
            return;
        }

        // The thorough check can run on a model of its own, and a run calls both.
        FaithfulnessOptions faithfulness = ThoroughCheckModel.Read(configuration);
        ThoroughCheckModel check = ThoroughCheckModel.Resolve(llm, faithfulness);
        List<string> models = [llm.Model];
        if (faithfulness.Thorough && check.DiffersFrom(llm))
        {
            models.Add(check.Model);
        }

        using IChatClient client = LlmServiceCollectionExtensions.CreateChatClient(configuration, transport);
        foreach (string model in models)
        {
            try
            {
                ChatResponse response = await client.GetResponseAsync(
                    [new ChatMessage(ChatRole.User, "Reply with OK.")],
                    new ChatOptions { ModelId = model, MaxOutputTokens = EndpointCheckOutputTokens },
                    cancellationToken);
                string tokens = response.Usage?.TotalTokenCount is { } total ? $", {total} tokens" : string.Empty;
                report.Ok("endpoint", $"{model} answered{tokens}");
            }
            catch (InvalidOperationException ex)
            {
                report.Fail("endpoint", ex.Message);
            }
        }
    }

    private static async Task CheckGitHubAsync(
        Report report,
        IConfiguration? configuration,
        RepositoryCoordinates? repository,
        string? tag,
        string? tagCommit,
        HttpMessageHandler? transport,
        CancellationToken cancellationToken)
    {
        if (configuration is null || repository is null || tag is null || tagCommit is null)
        {
            const string Needs = "needs the configuration, the repository and the tag";
            report.Skip("GitHub read", Needs);
            report.Skip("GitHub write", Needs);
            report.Skip("PR titles", Needs);
            return;
        }

        GitHubOptions options = GitHubHttpClientFactory.ReadOptions(configuration);
        using HttpClient http = GitHubHttpClientFactory.Create(options, configuration, transport);
        GitHubPullRequestReader reader = new(http, options.TokenEnvironmentVariable);
        string repo = $"{repository.Owner}/{repository.Name}";

        // The request a run starts with: the pull requests of a commit, here the tag's.
        try
        {
            await reader.EnsureCanReadAsync(repository, tagCommit, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            report.Fail("GitHub read", ex.Message);
            report.Skip("GitHub write", "needs read access");
            report.Skip("PR titles", "needs read access");
            return;
        }

        if (string.IsNullOrWhiteSpace(configuration[options.TokenEnvironmentVariable]))
        {
            report.Warn("GitHub read", $"""
                {repo} at {options.ApiBaseUrl}, without a token: {options.TokenEnvironmentVariable} is not set.
                GitHub allows 60 requests an hour without one, and a run spends one per commit of the release.
                Create a fine-grained token at {GitHubTokenNotice.NewTokenUrl}, then: export {options.TokenEnvironmentVariable}=<token>
                """);
        }
        else
        {
            report.Ok("GitHub read", $"{repo} at {options.ApiBaseUrl}, token from {options.TokenEnvironmentVariable}");
        }

        // Only generate publishes, so a token that may not is a warning, not a failure:
        // preview and generate --no-publish still run.
        try
        {
            await new GitHubReleaseNotesWriter(http, options.TokenEnvironmentVariable)
                .EnsureCanWriteAsync(repository, tag, cancellationToken);
            report.Ok("GitHub write", $"the token can publish release notes to {repo}, which generate needs");
        }
        catch (InvalidOperationException ex)
        {
            report.Warn("GitHub write", $"""
                {ex.Message.TrimEnd()}
                generate stops before its first model call. preview and generate --no-publish publish nothing and still run.
                """);
        }

        await CheckTitlesAsync(report, reader, repository, cancellationToken);
    }

    /// <summary>
    /// How many recent pull request titles carry a Conventional Commits prefix.
    /// The category of a change comes from that prefix, deterministically. Without one a
    /// change is Other, and internal work is not filtered out, which a user otherwise
    /// finds out from a poor changelog, after paying for it.
    /// </summary>
    private static async Task CheckTitlesAsync(
        Report report,
        GitHubPullRequestReader reader,
        RepositoryCoordinates repository,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> titles;
        try
        {
            titles = await reader.ReadRecentMergedTitlesAsync(repository, RecentPullRequests, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            report.Warn("PR titles", ex.Message);
            return;
        }

        List<string> without = [.. titles.Where(title => !ConventionalCommitCategorizer.HasKnownPrefix(title))];
        if (titles.Count == 0)
        {
            report.Ok("PR titles", "no merged pull requests yet");
            return;
        }

        if (without.Count == 0)
        {
            report.Ok("PR titles", $"all of the last {titles.Count} merged pull requests carry a Conventional Commits prefix");
            return;
        }

        string examples = string.Join(", ", without.Take(3).Select(title => $"'{title}'"));
        report.Warn("PR titles", $"""
            {titles.Count - without.Count} of the last {titles.Count} merged pull requests carry a Conventional Commits prefix, such as feat: or fix:.
            Without one, a change is Other, and internal work is not filtered out. For example: {examples}
            docs/writing-pull-requests.md shows what a prefix changes in the output.
            """);
    }

    // Windows variable names are case-insensitive, everywhere else they are not.
    private static string? Variable(IDictionary environment, string name)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (DictionaryEntry variable in environment)
        {
            if (string.Equals((string)variable.Key, name, comparison))
            {
                return variable.Value as string;
            }
        }

        return null;
    }

    /// <summary>
    /// Writes each check as it finishes, so a slow endpoint shows which check it is on.
    /// A check that cannot run for want of an earlier one is listed as not checked,
    /// rather than left out, so the list is the same length on every machine.
    /// </summary>
    private sealed class Report(TextWriter output)
    {
        private const int CheckWidth = 13;

        public bool Failed { get; private set; }

        public bool Warned { get; private set; }

        public void Ok(string check, string detail) => Write("ok", check, detail);

        public void Warn(string check, string detail)
        {
            Warned = true;
            Write("warn", check, detail);
        }

        public void Fail(string check, string detail)
        {
            Failed = true;
            Write("fail", check, detail);
        }

        public void Skip(string check, string reason) => Write("skip", check, $"not checked: {reason}");

        private void Write(string status, string check, string detail)
        {
            string indent = new(' ', 2 + 4 + 2 + CheckWidth + 1);
            string[] lines = detail.ReplaceLineEndings("\n").Split('\n');
            output.WriteLine($"  {status,-4}  {check,-CheckWidth} {lines[0].TrimEnd()}");
            foreach (string line in lines.Skip(1).Where(line => line.Trim().Length > 0))
            {
                output.WriteLine(indent + line.Trim());
            }
        }
    }
}

/// <summary>Handlers in place of the network, for tests. A run passes none.</summary>
/// <param name="GitHub">Stands in for the GitHub API.</param>
/// <param name="Model">Stands in for the model endpoint.</param>
internal sealed record DoctorTransports(HttpMessageHandler? GitHub = null, HttpMessageHandler? Model = null);
