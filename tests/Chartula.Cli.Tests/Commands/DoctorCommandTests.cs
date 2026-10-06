using System.Collections;
using System.Net;
using System.Text;
using Chartula.Cli.Commands;
using Chartula.Cli.Composition;
using Chartula.Cli.Terminal;

namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// #254: <c>chartula doctor</c> checks what a run needs before a run spends anything.
/// git runs for real, in a checkout each test creates. GitHub and the model endpoint are
/// handlers, so no test reaches the network or spends a token.
/// </summary>
public sealed class DoctorCommandTests : IDisposable
{
    private const string Tag = "v1.0.0";

    private readonly string _checkout = Path.Combine(Path.GetTempPath(), "chartula-doctor-" + Guid.NewGuid().ToString("N"));

    private readonly List<string> _gitHubRequests = [];

    private readonly List<string> _modelRequests = [];

    public void Dispose() => TestDirectory.Delete(_checkout);

    private async Task GitAsync(string directory, params string[] arguments)
    {
        (int exitCode, _, string error) = await CliProcess.RunAsync(
            "git", directory, ["-c", "user.email=t@example.com", "-c", "user.name=T", .. arguments]);
        Assert.True(exitCode == 0, error);
    }

    private async Task CreateCheckoutAsync(bool tagged = true)
    {
        Directory.CreateDirectory(_checkout);
        await GitAsync(_checkout, "init", "-b", "main");
        await GitAsync(_checkout, "remote", "add", "origin", "https://github.com/octo/repo.git");
        await GitAsync(_checkout, "commit", "--allow-empty", "-m", "feat: A");
        if (tagged)
        {
            await GitAsync(_checkout, "tag", Tag);
        }
    }

    /// <summary>A setup that passes: openai-compatible at a stubbed endpoint, a token, and git on the PATH.</summary>
    private static Hashtable Environment(params (string Name, string? Value)[] overrides)
    {
        Hashtable environment = new()
        {
            ["PATH"] = System.Environment.GetEnvironmentVariable("PATH"),
            ["Chartula__Llm__Provider"] = "openai-compatible",
            ["Chartula__Llm__Model"] = "some-model",
            ["Chartula__Llm__BaseUrl"] = "http://localhost:8799/v1",
            ["GITHUB_TOKEN"] = "ghp_secret_value",
        };
        foreach ((string name, string? value) in overrides)
        {
            if (value is null)
            {
                environment.Remove(name);
            }
            else
            {
                environment[name] = value;
            }
        }

        return environment;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private const string Answer =
        """{"id":"c","object":"chat.completion","created":0,"model":"some-model","choices":[{"index":0,"message":{"role":"assistant","content":"OK"},"finish_reason":"stop"}],"usage":{"prompt_tokens":10,"completion_tokens":1,"total_tokens":11}}""";

    private static string PullList(params string[] titles)
        => "[" + string.Join(",", titles.Select((title, i) =>
            $$"""{"number":{{i + 1}},"title":"{{title}}","merged_at":"2026-09-{{10 + i}}T00:00:00Z"}""")) + "]";

    /// <summary>GitHub as a run meets it: the commit is readable, the token may publish, and every title has a prefix.</summary>
    private Func<HttpRequestMessage, HttpResponseMessage> GitHub(
        HttpStatusCode read = HttpStatusCode.OK,
        HttpStatusCode write = HttpStatusCode.OK,
        string? pulls = null)
        => request =>
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/pulls", StringComparison.Ordinal) && path.Contains("/commits/", StringComparison.Ordinal))
            {
                return read == HttpStatusCode.OK ? Json(read, "[]") : Json(read, """{"message":"Not Found"}""");
            }

            if (path.EndsWith("/releases/generate-notes", StringComparison.Ordinal))
            {
                return write == HttpStatusCode.OK
                    ? Json(write, """{"name":"v1.0.0","body":"notes"}""")
                    : Json(write, """{"message":"Resource not accessible by personal access token"}""");
            }

            return path.EndsWith("/repos/octo/repo/pulls", StringComparison.Ordinal)
                ? Json(HttpStatusCode.OK, pulls ?? PullList("feat: A", "fix: B"))
                : Json(HttpStatusCode.NotFound, """{"message":"Not Found"}""");
        };

    private async Task<(int ExitCode, string Output)> DoctorAsync(
        Hashtable environment,
        Func<HttpRequestMessage, HttpResponseMessage>? gitHub = null,
        Func<HttpRequestMessage, HttpResponseMessage>? model = null,
        TerminalProfile? profile = null,
        params string[] args)
    {
        Directory.CreateDirectory(_checkout);
        StringWriter output = new();
        int exitCode = await DoctorCommand.RunAsync(
            ["doctor", .. args],
            _checkout,
            environment,
            output,
            new DoctorTransports(
                new Recording(_gitHubRequests, gitHub ?? GitHub()),
                new Recording(_modelRequests, model ?? (_ => Json(HttpStatusCode.OK, Answer)))),
            profile);
        return (exitCode, output.ToString());
    }

    private static void AssertLine(string output, string status, string check, string detail)
        => Assert.Contains(
            output.Split('\n'),
            line => line.StartsWith($"  {status,-4}  {check,-13} ", StringComparison.Ordinal) && line.Contains(detail, StringComparison.Ordinal));

    [Fact]
    public async Task A_ready_setup_passes_every_check_and_says_a_run_would_start()
    {
        await CreateCheckoutAsync();

        (int exitCode, string output) = await DoctorAsync(Environment());

        Assert.Equal(0, exitCode);
        AssertLine(output, "ok", "git", "git");
        AssertLine(output, "ok", "checkout", "with its full history");
        AssertLine(output, "ok", "tag", "v1.0.0, the nearest tag reachable from HEAD");
        AssertLine(output, "ok", "repository", "octo/repo, from the 'origin' remote");
        AssertLine(output, "ok", "config", "no chartula.yaml");
        AssertLine(output, "ok", "model", "openai-compatible at http://localhost:8799/v1, model some-model");
        AssertLine(output, "ok", "endpoint", "some-model answered, 11 tokens");
        AssertLine(output, "ok", "GitHub read", "octo/repo at https://api.github.com/, token from GITHUB_TOKEN");
        AssertLine(output, "ok", "GitHub write", "the token can publish release notes to octo/repo");
        AssertLine(output, "ok", "PR titles", "all of the last 2 merged pull requests");
        Assert.EndsWith("A run would start.\n", output.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task It_names_variables_never_their_values_and_writes_nothing()
    {
        await CreateCheckoutAsync();

        (_, string output) = await DoctorAsync(Environment());

        Assert.DoesNotContain("ghp_secret_value", output);

        // Reading and asking only: no release is created or changed, no file is written.
        Assert.DoesNotContain(_gitHubRequests, request => request.StartsWith("POST /repos/octo/repo/releases ", StringComparison.Ordinal));
        Assert.DoesNotContain(_gitHubRequests, request => request.StartsWith("PATCH", StringComparison.Ordinal));
        (_, string status, _) = await CliProcess.RunAsync("git", _checkout, ["status", "--porcelain", "--ignored"]);
        Assert.Empty(status.Trim());
    }

    [Fact]
    public async Task The_endpoint_check_is_one_small_call_per_model()
    {
        await CreateCheckoutAsync();

        await DoctorAsync(Environment());

        string request = Assert.Single(_modelRequests);
        Assert.StartsWith("POST /v1/chat/completions", request);
        Assert.Contains("\"model\":\"some-model\"", request);
        Assert.Matches("\"max_(completion_)?tokens\":16", request); // the output ceiling
    }

    [Fact]
    public async Task A_thorough_check_on_a_model_of_its_own_is_asked_as_well()
    {
        await CreateCheckoutAsync();

        (_, string output) = await DoctorAsync(Environment(("Chartula__Faithfulness__Model", "check-model")));

        AssertLine(output, "ok", "endpoint", "some-model answered");
        AssertLine(output, "ok", "endpoint", "check-model answered");
        Assert.Equal(2, _modelRequests.Count);
    }

    [Fact]
    public async Task Without_git_the_checks_that_need_it_are_not_run()
    {
        (int exitCode, string output) = await DoctorAsync(Environment(("PATH", null)));

        Assert.Equal(1, exitCode);
        AssertLine(output, "fail", "git", "Could not find 'git' on the PATH");
        AssertLine(output, "skip", "checkout", "not checked: needs git");
        AssertLine(output, "skip", "tag", "not checked: needs a checkout");
        Assert.Contains("A run would stop.", output);
    }

    [Fact]
    public async Task A_directory_that_is_not_a_checkout_fails()
    {
        (int exitCode, string output) = await DoctorAsync(Environment());

        Assert.Equal(1, exitCode);
        AssertLine(output, "fail", "checkout", "is not a git checkout");
        AssertLine(output, "skip", "repository", "not checked: needs a checkout, or --repo");
        AssertLine(output, "skip", "GitHub read", "not checked");
    }

    [Fact]
    public async Task A_shallow_clone_fails_with_how_to_fetch_the_history()
    {
        await CreateCheckoutAsync();
        await GitAsync(_checkout, "commit", "--allow-empty", "-m", "feat: B");
        string shallow = _checkout + "-shallow";
        try
        {
            await GitAsync(Path.GetTempPath(), "clone", "--quiet", "--depth", "1", "--no-local", "file://" + _checkout, shallow);

            StringWriter output = new();
            int exitCode = await DoctorCommand.RunAsync(
                ["doctor", "--tag", "main", "--repo", "octo/repo"], shallow, Environment(), output,
                new DoctorTransports(new Recording([], GitHub()), new Recording([], _ => Json(HttpStatusCode.OK, Answer))));

            Assert.Equal(1, exitCode);
            AssertLine(output.ToString(), "fail", "checkout", "is a shallow clone");
            Assert.Contains("git fetch --unshallow --tags", output.ToString());
        }
        finally
        {
            TestDirectory.Delete(shallow);
        }
    }

    [Fact]
    public async Task A_checkout_without_a_tag_fails_naming_how_to_fetch_the_tags()
    {
        await CreateCheckoutAsync(tagged: false);

        (int exitCode, string output) = await DoctorAsync(Environment());

        Assert.Equal(1, exitCode);
        AssertLine(output, "fail", "tag", "no tag is reachable from HEAD");
        AssertLine(output, "fail", "tag", "git fetch --tags");
    }

    [Fact]
    public async Task A_tag_passed_that_the_checkout_does_not_have_fails()
    {
        await CreateCheckoutAsync();

        (int exitCode, string output) = await DoctorAsync(Environment(), args: ["--tag", "v9.9.9"]);

        Assert.Equal(1, exitCode);
        AssertLine(output, "fail", "tag", "'v9.9.9' is not a tag");
    }

    [Fact]
    public async Task A_configuration_error_fails_and_the_checks_that_need_it_are_not_run()
    {
        await CreateCheckoutAsync();
        File.WriteAllText(Path.Combine(_checkout, "chartula.yaml"), "llm:\n  model: a\n");
        File.WriteAllText(Path.Combine(_checkout, "chartula.yml"), "llm:\n  model: b\n");

        (int exitCode, string output) = await DoctorAsync(Environment());

        Assert.Equal(1, exitCode);
        AssertLine(output, "fail", "config", "a run reads one configuration file");
        AssertLine(output, "skip", "model", "not checked");
        AssertLine(output, "skip", "endpoint", "not checked");
        Assert.Empty(_modelRequests);
        Assert.Empty(_gitHubRequests);
    }

    [Fact]
    public async Task The_file_that_is_read_is_named()
    {
        await CreateCheckoutAsync();
        File.WriteAllText(Path.Combine(_checkout, "chartula.yaml"), "factBase:\n  depth: title-only\n");

        (_, string output) = await DoctorAsync(Environment());

        AssertLine(output, "ok", "config", Path.Combine(_checkout, "chartula.yaml"));
        Assert.DoesNotContain("chartula.example.yaml", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_file_the_template_of_this_version_is_named()
    {
        await CreateCheckoutAsync();

        (_, string output) = await DoctorAsync(Environment());

        Assert.Contains(
            $"https://github.com/goldbarth/chartula/blob/v{ToolVersion.Release}/chartula.example.yaml",
            output,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_anthropic_setup_without_a_key_fails_naming_the_variable_and_asks_no_endpoint()
    {
        await CreateCheckoutAsync();

        (int exitCode, string output) = await DoctorAsync(Environment(
            ("Chartula__Llm__Provider", null), ("Chartula__Llm__Model", null), ("Chartula__Llm__BaseUrl", null)));

        Assert.Equal(1, exitCode);
        AssertLine(output, "fail", "model", "anthropic at its default endpoint, model claude-sonnet-5");
        Assert.Contains("No Anthropic API key found in ANTHROPIC_API_KEY.", output);
        AssertLine(output, "skip", "endpoint", "not checked: needs the model settings and a key");
        Assert.Empty(_modelRequests);
    }

    [Fact]
    public async Task An_endpoint_that_does_not_serve_the_model_fails_in_the_words_of_a_run()
    {
        await CreateCheckoutAsync();

        (int exitCode, string output) = await DoctorAsync(
            Environment(),
            model: _ => Json(HttpStatusCode.NotFound, """{"error":{"message":"model 'some-model' not found"}}"""));

        Assert.Equal(1, exitCode);
        AssertLine(output, "fail", "endpoint", "answered 404 Not Found for model 'some-model'.");
        Assert.Contains("The endpoint said: model 'some-model' not found", output);
    }

    [Fact]
    public async Task A_repository_GitHub_does_not_show_fails_and_the_later_GitHub_checks_are_not_run()
    {
        await CreateCheckoutAsync();

        (int exitCode, string output) = await DoctorAsync(Environment(), GitHub(read: HttpStatusCode.NotFound));

        Assert.Equal(1, exitCode);
        AssertLine(output, "fail", "GitHub read", "GitHub cannot read octo/repo (404 Not Found).");
        AssertLine(output, "skip", "GitHub write", "not checked: needs read access");
        AssertLine(output, "skip", "PR titles", "not checked: needs read access");
    }

    // Only generate publishes, so preview and --no-publish still run.
    [Fact]
    public async Task A_token_that_may_not_publish_is_a_warning_that_names_the_permission()
    {
        await CreateCheckoutAsync();

        (int exitCode, string output) = await DoctorAsync(Environment(), GitHub(write: HttpStatusCode.Forbidden));

        Assert.Equal(0, exitCode);
        AssertLine(output, "warn", "GitHub write", "GitHub does not let this run publish the release notes for v1.0.0 to octo/repo (403 Forbidden).");
        Assert.Contains("Publishing needs a token with Contents read and write on octo/repo.", output);
        Assert.Contains("preview and generate --no-publish publish nothing and still run.", output);
        Assert.EndsWith(
            "Setup checks completed with 1 warning. preview and generate --no-publish would start; generate would stop at GitHub write.\n",
            output.ReplaceLineEndings("\n"));
    }

    // #339: the checks in three groups under a header, in the order a run meets them.
    [Fact]
    public async Task The_checks_stand_in_three_groups_under_a_header()
    {
        await CreateCheckoutAsync();

        (_, string output) = await DoctorAsync(Environment());

        string[] lines = output.ReplaceLineEndings("\n").Split('\n');
        Assert.Equal("chartula doctor", lines[0]);
        Assert.Equal($"Checkout  {_checkout}", lines[1]);
        int repository = Array.IndexOf(lines, "Repository");
        int model = Array.IndexOf(lines, "Model");
        int gitHub = Array.IndexOf(lines, "GitHub");
        Assert.True(repository > 1 && repository < model && model < gitHub);
        int config = Array.FindIndex(lines, line => line.StartsWith("  ok    config", StringComparison.Ordinal));
        Assert.True(repository < config && config < model);
        Assert.StartsWith("  ok    model", lines[model + 1]);
        Assert.StartsWith("  ok    GitHub read", lines[gitHub + 1]);
    }

    // On a terminal a status is a symbol and its word; the word alone carries the meaning.
    [Fact]
    public async Task On_a_terminal_each_status_is_a_symbol_and_its_word()
    {
        await CreateCheckoutAsync();

        (_, string output) = await DoctorAsync(
            Environment(), GitHub(write: HttpStatusCode.Forbidden),
            profile: new TerminalProfile(Live: true, ColorDepth.None, Unicode: true));

        Assert.StartsWith("chartula · doctor\n", output.ReplaceLineEndings("\n"));
        Assert.Contains("\n  ✓ ok    git           ", output.ReplaceLineEndings("\n"));
        Assert.Contains("\n  ! warn  GitHub write  GitHub does not let this run publish", output.ReplaceLineEndings("\n"));
        Assert.Contains("\n                        Publishing needs a token", output.ReplaceLineEndings("\n"));
        Assert.DoesNotContain('\u001b', output);
    }

    [Fact]
    public async Task A_warning_that_does_not_stop_a_run_says_a_run_would_start()
    {
        await CreateCheckoutAsync();

        (int exitCode, string output) = await DoctorAsync(Environment(), GitHub(pulls: PullList("Update readme", "feat: A")));

        Assert.Equal(0, exitCode);
        AssertLine(output, "warn", "PR titles", "1 of the last 2 merged pull requests");
        Assert.EndsWith("Setup checks completed with 1 warning. A run would start; read them before generate.\n", output.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task No_token_is_a_warning_on_reading_and_on_publishing()
    {
        await CreateCheckoutAsync();

        (int exitCode, string output) = await DoctorAsync(
            Environment(("GITHUB_TOKEN", null)), GitHub(write: HttpStatusCode.Unauthorized));

        Assert.Equal(0, exitCode);
        AssertLine(output, "warn", "GitHub read", "without a token: GITHUB_TOKEN is not set.");
        Assert.Contains("export GITHUB_TOKEN=<token>", output);
        AssertLine(output, "warn", "GitHub write", "(401 Unauthorized)");
        Assert.Contains("The request carried no token: GITHUB_TOKEN is not set.", output);
    }

    [Fact]
    public async Task Titles_without_a_prefix_are_counted_with_what_that_does_to_the_output()
    {
        await CreateCheckoutAsync();

        (int exitCode, string output) = await DoctorAsync(
            Environment(),
            GitHub(pulls: PullList("feat: A", "Update readme", "wip: try", "fix(cli): B")));

        Assert.Equal(0, exitCode);
        AssertLine(output, "warn", "PR titles", "2 of the last 4 merged pull requests carry a Conventional Commits prefix");
        Assert.Contains("Without one, a change is Other, and internal work is not filtered out.", output);
        Assert.Contains("For example: 'wip: try', 'Update readme'", output); // newest first
    }

    [Fact]
    public async Task Pull_requests_closed_without_a_merge_are_not_counted()
    {
        await CreateCheckoutAsync();
        string pulls = """[{"number":1,"title":"feat: A","merged_at":"2026-09-10T00:00:00Z"},{"number":2,"title":"Closed unmerged","merged_at":null}]""";

        (_, string output) = await DoctorAsync(Environment(), GitHub(pulls: pulls));

        AssertLine(output, "ok", "PR titles", "all of the last 1 merged pull requests");
    }

    /// <summary>Records each request as <c>METHOD path body</c> and answers with <paramref name="route"/>.</summary>
    private sealed class Recording(List<string> requests, Func<HttpRequestMessage, HttpResponseMessage> route)
        : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath} {body}");
            HttpResponseMessage response = route(request);
            response.RequestMessage = request;
            return response;
        }
    }
}
