namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// #300: runs the built CLI with a command line a person gets wrong.
/// It runs from an empty directory, so a run that started would stop at once, on the
/// missing checkout, and say so. Neither test may get that far.
/// </summary>
public sealed class ArgumentCheckTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("chartula-arguments-").FullName;

    private static readonly Dictionary<string, string?> NoCredentials = new()
    {
        ["ANTHROPIC_API_KEY"] = null,
        ["GITHUB_TOKEN"] = null,
    };

    [Theory]
    [InlineData("generate", "--help")]
    [InlineData("generate", "--tag", "v1.0.0", "-h")]
    [InlineData("doctor", "--help")]
    public async Task Help_after_a_command_prints_the_help_and_starts_nothing(params string[] args)
    {
        (int exitCode, string output, string error) = await CliProcess.RunChartulaAsync(_directory, NoCredentials, args);

        Assert.Equal(0, exitCode);
        Assert.StartsWith("chartula - multi-audience, grounded changelog generator.", output);
        Assert.Empty(error);
    }

    [Fact]
    public async Task A_misspelled_no_publish_stops_before_anything_starts()
    {
        (int exitCode, string output, string error) = await CliProcess.RunChartulaAsync(
            _directory, NoCredentials, "generate", "--nopublish");

        Assert.Equal(1, exitCode);
        Assert.StartsWith("Unknown option '--nopublish' for generate. Did you mean --no-publish?", error);
        Assert.Contains("chartula --help lists every command and its options.", error);
        Assert.DoesNotContain("--repo", error); // the checkout was never read
        Assert.Empty(output);
    }

    // #353: a refused argument prints its error and the pointer to the help, whichever
    // argument it is, and never the header of a run that did not start.
    [Theory]
    [InlineData("Invalid option --repo 'name-only'. Expected <owner/name>.", "preview", "--repo", "name-only")]
    [InlineData("Unknown audience 'developers'. There are three: technical, customer, product.", "generate", "--audience", "developers")]
    [InlineData("--audience has a comma with no audience next to it.", "generate", "--audience", "product,")]
    [InlineData("Unknown audience '1'. There are three: technical, customer, product.", "preview", "--audience", "1")]
    public async Task A_refused_value_prints_its_error_and_no_header(string expected, params string[] args)
    {
        (int exitCode, string output, string error) = await CliProcess.RunChartulaAsync(_directory, NoCredentials, args);

        Assert.Equal(1, exitCode);
        string[] lines = error.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.StartsWith(expected, lines[0]);
        Assert.Equal("chartula --help lists every command and its options.", lines[1]);
        Assert.Empty(output);
    }

    [Fact]
    public async Task The_word_version_is_refused_and_told_the_flag()
    {
        (int exitCode, _, string error) = await CliProcess.RunChartulaAsync(_directory, NoCredentials, "version");

        Assert.Equal(1, exitCode);
        Assert.StartsWith("Unknown command 'version'. For the installed version, run chartula --version.", error);
    }

    public void Dispose() => TestDirectory.Delete(_directory);
}
