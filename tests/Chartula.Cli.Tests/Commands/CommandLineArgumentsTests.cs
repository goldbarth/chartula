using Chartula.Cli;
using Chartula.Cli.Commands;
using Chartula.Core.Pipeline;

namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// The parser is tiny, but a flag that is read as a value - or a value that is read
/// as a flag - would silently change what a run does.
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
        // --no-publish takes no value, so the options around it must still read.
        Assert.Equal("v1.0.0", CommandLineArguments.GetOption(Generate, "--tag"));
        Assert.Equal("octo/repo", CommandLineArguments.GetOption(Generate, "--repo"));
    }

    [Fact]
    public void The_help_text_names_the_flag()
        => Assert.Contains("--no-publish", Program.Usage);

    [Fact]
    public void Generate_publishes_unless_it_is_told_not_to()
    {
        Assert.Equal(
            PipelineMode.Generate,
            Program.ParseMode("generate", ["generate", "--tag", "v1.0.0", "--repo", "octo/repo"]));
        Assert.Equal(PipelineMode.GenerateWithoutPublishing, Program.ParseMode("generate", Generate));
    }

    [Fact]
    public void Preview_stays_a_preview_with_or_without_the_flag()
    {
        // Preview already publishes nothing, so the flag has nothing left to drop.
        Assert.Equal(PipelineMode.Preview, Program.ParseMode("preview", ["preview", "--tag", "v1.0.0"]));
        Assert.Equal(PipelineMode.Preview, Program.ParseMode("preview", ["preview", "--no-publish"]));
    }

    [Fact]
    public void An_unknown_command_has_no_mode()
        => Assert.Null(Program.ParseMode("publish", ["publish"]));
}
