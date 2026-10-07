using Chartula.Cli;
using Chartula.Cli.Commands;
using Chartula.Core.Pipeline;

namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// The parser is tiny, but a flag read as a value, or a value read as a flag, would
/// silently change what a run does.
/// </summary>
public sealed class CommandLineArgumentsTests
{
    private static readonly string[] Generate =
        ["generate", "--tag", "v1.0.0", "--repo", "octo/repo", "--no-publish"];

    [Fact]
    public void A_flag_is_found_wherever_it_stands()
    {
        Assert.True(CommandLineArguments.HasFlag(Generate, "--no-publish"));
        Assert.True(CommandLineArguments.HasFlag(["generate", "--no-publish", "--tag", "v1.0.0"], "--no-publish"));
    }

    [Fact]
    public void An_absent_flag_is_absent()
        => Assert.False(
            CommandLineArguments.HasFlag(["generate", "--tag", "v1.0.0", "--repo", "octo/repo"], "--no-publish"));

    [Fact]
    public void A_trailing_flag_does_not_swallow_the_option_before_it()
    {
        // --no-publish takes no value, so the options around it must still be read correctly.
        Assert.Equal("v1.0.0", CommandLineArguments.GetOption(Generate, "--tag"));
        Assert.Equal("octo/repo", CommandLineArguments.GetOption(Generate, "--repo"));
    }

    [Fact]
    public void The_help_text_names_the_flag()
        => Assert.Contains("--no-publish", Program.Usage);

    // Two pull requests each added an option to the help, and merging them left one option
    // listed twice and another under the wrong heading. Each option stands once, in its group.
    [Theory]
    [InlineData("Which release:", "--tag", "--repo", "--since", "--yes")]
    [InlineData("What a run writes:", "--audience", "--no-publish", "--replace-published")]
    [InlineData("How it looks:", "--plain")]
    public void The_help_lists_each_option_once_under_its_group(string group, params string[] options)
    {
        string[] lines = Program.Usage.ReplaceLineEndings("\n").Split('\n');
        int start = Array.IndexOf(lines, group);
        Assert.True(start >= 0, $"The help has no group '{group}'.");
        string[] listed = [.. lines.Skip(start + 1).TakeWhile(static line => line.Length > 0)
            .Where(static line => line.StartsWith("  --", StringComparison.Ordinal))
            .Select(static line => line.Trim().Split(' ')[0])];

        Assert.Equal(options, listed);
        foreach (string option in options)
        {
            Assert.Single(lines, line => line.TrimStart().StartsWith(option + " ", StringComparison.Ordinal) || line.Trim() == option);
        }
    }

    [Fact]
    public void The_help_text_says_the_flag_leaves_out_only_the_release_notes()
    {
        // It once named two of the files, which read as if the others were not written.
        string line = Program.Usage.Split('\n').Single(l => l.TrimStart().StartsWith("--no-publish", StringComparison.Ordinal));
        Assert.Contains("every file", line);
        Assert.Contains("no GitHub release notes", line);
    }

    [Fact]
    public void Generate_publishes_unless_it_is_told_not_to()
    {
        Assert.Equal(
            PipelineMode.Generate,
            Program.ParseMode("generate", ["generate", "--tag", "v1.0.0", "--repo", "octo/repo"]));
        Assert.Equal(PipelineMode.GenerateWithoutPublishing, Program.ParseMode("generate", Generate));
    }

    [Fact]
    public void Preview_is_a_preview()
        => Assert.Equal(PipelineMode.Preview, Program.ParseMode("preview", ["preview", "--tag", "v1.0.0"]));

    // #300: the readers look options up by name and pass over the rest, so the command
    // line is checked first, and refused when any part of it cannot be read as meant.
    [Theory]
    [InlineData("generate", "--tag", "v1.0.0", "--repo", "octo/repo", "--since", "v0.9.0", "--audience", "technical,customer", "--yes", "--no-publish")]
    [InlineData("generate", "--replace-published")]
    [InlineData("preview", "--tag", "v1.0.0", "--audience", "technical", "--audience", "customer", "--yes")]
    [InlineData("doctor", "--tag", "v1.0.0", "--repo", "octo/repo", "--plain")]
    [InlineData("preview", "--plain")]
    [InlineData("generate", "--plain", "--no-publish")]
    [InlineData("generate")]
    public void Every_option_a_command_takes_passes(params string[] args)
        => Assert.Null(CommandLineArguments.Check(args));

    [Theory]
    [InlineData("Unknown option '--tga' for preview. Did you mean --tag?", "preview", "--tga", "v1.0.0")]
    [InlineData("Unknown option '--nopublish' for generate. Did you mean --no-publish?", "generate", "--nopublish")]
    [InlineData("Unknown option '--verbose' for generate.", "generate", "--verbose")]
    [InlineData("--tag needs a value: --tag <release-tag>.", "preview", "--tag")]
    [InlineData("--tag needs a value: --tag <release-tag>.", "preview", "--tag", "--audience", "technical")]
    [InlineData("--since needs a value: --since <ref>.", "generate", "--since", "--no-publish")]
    [InlineData("--tag is given twice. Pass it once.", "generate", "--tag", "v1", "--tag", "v2")]
    [InlineData("--no-publish is an option of generate, not of preview.", "preview", "--no-publish")]
    [InlineData("--replace-published is an option of generate, not of doctor.", "doctor", "--replace-published")]
    [InlineData("--since is an option of preview and generate, not of doctor.", "doctor", "--since", "v1")]
    [InlineData("Write --tag v1.0.0, with a space, not --tag=v1.0.0.", "generate", "--tag=v1.0.0")]
    [InlineData("Unexpected argument 'v1.0.0'. generate takes options only, such as --tag <release-tag>.", "generate", "v1.0.0")]
    public void An_argument_that_cannot_be_read_as_meant_is_refused_with_the_fix(string expected, params string[] args)
        => Assert.Equal(expected, CommandLineArguments.Check(args));

    // #282 removed it; ignoring it would run a range the caller did not ask for.
    [Fact]
    public void The_removed_whole_history_flag_names_what_replaced_it()
        => Assert.StartsWith(
            "--whole-history is gone: a first tag renders every commit up to it without a flag.",
            CommandLineArguments.Check(["generate", "--whole-history"]));

    [Theory]
    [InlineData("-h", true)]
    [InlineData("--help", true)]
    [InlineData("help", false)]
    public void Help_is_asked_for_with_a_flag(string arg, bool help)
        => Assert.Equal(help, CommandLineArguments.IsHelp(arg));

    [Fact]
    public void An_unknown_command_has_no_mode()
        => Assert.Null(Program.ParseMode("publish", ["publish"]));
}
