using Chartula.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Tests.Configuration;

/// <summary>
/// #237: a malformed <c>chartula.yaml</c> ended the run with an unhandled YamlDotNet
/// exception. A well-formed file with a key in the wrong place was read as if the key
/// were missing. Now every case is refused with the file, the line and the column.
/// </summary>
public sealed class MalformedYamlTests
{
    private static string Refusal(string yaml, string fileName = "chartula.yaml")
        => Assert.Throws<InvalidOperationException>(() => ChartulaYamlConfiguration.Flatten(yaml, fileName)).Message;

    // The file from #237: two keys indented 4 spaces under a key that has a value, and
    // faithfulness nested under llm.
    [Fact]
    public void A_key_indented_under_a_key_with_a_value_is_named_by_line_and_column()
    {
        string message = Refusal(
            """
            llm:
              provider: anthropic
                model: claude-sonnet-5
                thinking: disabled
              faithfulness:
                thorough: true
            """);

        Assert.StartsWith("chartula.yaml, line 3, column 10: this is not valid YAML", message);
        Assert.Contains("line 3 is indented further than line 2 (4 spaces against 2), but line 2 already has a value", message);
        Assert.DoesNotContain("Idx:", message);
    }

    [Fact]
    public void A_key_that_lines_up_with_no_key_above_is_named()
    {
        string message = Refusal(
            """
            llm:
                model: claude-sonnet-5
                thinking: disabled
              faithfulness:
                thorough: true
            """);

        Assert.StartsWith("chartula.yaml, line 4, column 3:", message);
        Assert.Contains("line 4 is indented 2 spaces, which lines up with no key above it (they are at 0 or 4)", message);
    }

    // Once the indentation parses, faithfulness under llm is valid YAML. It used to be
    // ignored, so thorough: false had no effect.
    [Fact]
    public void A_section_nested_under_another_is_refused_pointing_at_the_indentation()
    {
        string message = Refusal(
            """
            llm:
              model: claude-sonnet-5
              faithfulness:
                thorough: false
            """);

        Assert.Equal(
            "chartula.yaml, line 3, column 3: 'faithfulness' is not a key of 'llm' but a section of its own. " +
            "Check the indentation: 'faithfulness:' starts at the beginning of its line, like 'llm:'.",
            message);
    }

    // YamlDotNet reports the start of the mapping, line 1, not the line with the tab.
    [Fact]
    public void A_tab_is_named_on_its_own_line()
    {
        string message = Refusal("llm:\n  model: claude-sonnet-5\n\tthinking: disabled\n");

        Assert.Equal("chartula.yaml, line 3, column 1: this line is indented with a tab. YAML indents with spaces only.", message);
    }

    [Fact]
    public void An_unclosed_bracket_is_named()
    {
        string message = Refusal("filter:\n  excludeCategories: [Internal\n");

        Assert.StartsWith("chartula.yaml, line 3, column 1: this is not valid YAML", message);
        Assert.Contains("Check that every '[' and '{' on it is closed.", message);
    }

    [Fact]
    public void A_key_set_twice_is_refused_rather_than_the_last_one_winning()
    {
        string message = Refusal("llm:\n  model: a\nllm:\n  model: b\n");

        Assert.Equal("chartula.yaml, line 3, column 1: 'llm' is set a second time here; the second would replace the first. Set it once.", message);
    }

    [Theory]
    [InlineData("llm:\n  modle: x\n", "line 2, column 3: unknown key 'modle' in 'llm'. Did you mean 'model'?")]
    [InlineData("faithfullness:\n  thorough: false\n", "line 1, column 1: unknown key 'faithfullness' at the top level. Did you mean 'faithfulness'?")]
    [InlineData("model: x\n", "line 1, column 1: unknown key 'model' at the top level: it is a key of 'llm' and 'faithfulness'. Check the indentation")]
    [InlineData("llm:\n  depth: title-only\n", "line 2, column 3: unknown key 'depth' in 'llm': it is a key of 'factBase'.")]
    [InlineData("review:\n  on: true\n", "line 2, column 3: unknown key 'on' in 'review'. Valid keys: enabled.")]
    [InlineData("github:\n  token: x\n", "unknown key 'token' in 'github'. 'github' has no keys this file may set")]
    public void An_unknown_key_is_named_with_what_it_most_likely_is(string yaml, string expected)
    {
        Assert.Contains(expected, Refusal(yaml));
    }

    // Environment-only keys are never suggested as the intended key.
    [Fact]
    public void An_environment_only_key_is_not_suggested()
    {
        Assert.DoesNotContain("baseUrl", Refusal("llm:\n  baseUri: x\n"));
    }

    [Theory]
    [InlineData("faithfulness:\n  thorough: maybe\n", "line 2, column 13: 'faithfulness.thorough' is 'maybe'; expected true or false.")]
    [InlineData("review:\n  enabled: yes\n", "line 2, column 12: 'review.enabled' is 'yes'; expected true or false.")]
    [InlineData("llm: claude-sonnet-5\n", "line 1, column 6: 'llm' is a section: its keys go on the lines below it, indented, not the value 'claude-sonnet-5'.")]
    [InlineData("llm:\n  model: [a, b]\n", "line 2, column 10: 'llm.model' takes a single value, not a list.")]
    [InlineData("llm:\n  model:\n    thinking: high\n", "line 3, column 5: 'llm.model' takes a single value, but has keys nested under it.")]
    [InlineData("filter:\n  excludeCategories: Internal\n", "line 2, column 22: 'filter.excludeCategories' takes a list, such as [a, b], not the value 'Internal'.")]
    [InlineData("filter:\n  excludeCategories: [[Internal]]\n", "line 2, column 23: the items of 'filter.excludeCategories' are single values, not a list.")]
    [InlineData("labels:\n  category:\n    perf: [Performance]\n", "line 3, column 11: the values under 'labels.category' are single values, not a list.")]
    [InlineData("labels:\n  category: Fix\n", "line 2, column 13: 'labels.category' takes one 'name: value' per line below it")]
    [InlineData("- llm\n", "line 1, column 1: the file has to hold sections such as 'llm:' and 'labels:', not a list.")]
    [InlineData("llm:\n  model: a\n---\nllm:\n  model: b\n", "line 4, column 1: a second YAML document starts here.")]
    public void A_value_of_the_wrong_shape_or_type_is_named(string yaml, string expected)
    {
        Assert.Contains(expected, Refusal(yaml));
    }

    [Fact]
    public void Every_problem_is_named_at_once_one_per_line()
    {
        string message = Refusal("llm:\n  modle: x\nreview:\n  enabled: maybe\n");

        Assert.Equal(
            [
                "chartula.yaml, line 2, column 3: unknown key 'modle' in 'llm'. Did you mean 'model'?",
                "chartula.yaml, line 4, column 12: 'review.enabled' is 'maybe'; expected true or false.",
            ],
            message.Split('\n'));
    }

    // The refusal of environment-only keys, which decide where credentials go, still comes first.
    [Fact]
    public void An_environment_only_key_is_refused_before_the_other_problems()
    {
        string message = Refusal("llm:\n  baseUrl: https://example.test/v1\n  modle: x\n");

        Assert.StartsWith("chartula.yaml sets llm.baseUrl (set Chartula__Llm__BaseUrl instead).", message);
    }

    [Fact]
    public void The_file_is_named_as_it_was_found()
    {
        string directory = Path.Combine(Path.GetTempPath(), "chartula-yml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "chartula.yml"), "llm:\n  modle: x\n");

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => new ConfigurationBuilder().AddChartulaYaml(directory).Build());

            Assert.StartsWith("chartula.yml, line 2, column 3:", error.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Everything the file could express before keeps its meaning.
    [Theory]
    [InlineData("")]
    [InlineData("# only a comment\n")]
    [InlineData("llm:\n")]
    [InlineData("llm:\n  model:\n  thinking: ~\n")]
    public void An_empty_file_section_or_value_leaves_the_setting_unset(string yaml)
    {
        Assert.DoesNotContain(ChartulaYamlConfiguration.Flatten(yaml), pair => pair.Value is not null);
    }

    [Fact]
    public void Keys_are_matched_whatever_their_casing()
    {
        IReadOnlyList<KeyValuePair<string, string?>> pairs =
            ChartulaYamlConfiguration.Flatten("LLM:\n  Model: m\nFaithfulness:\n  THOROUGH: False\n");

        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(pairs).Build();
        Assert.Equal("m", config["Chartula:Llm:Model"]);
        Assert.Equal("False", config["Chartula:Faithfulness:Thorough"]);
    }

    // Users copy the documented example and this repository's own file, so both must read.
    [Fact]
    public void The_documented_example_and_the_repository_s_own_file_are_accepted()
    {
        string root = RepositoryRoot();
        string docs = File.ReadAllText(Path.Combine(root, "docs", "configuration.md")).ReplaceLineEndings("\n");
        string example = docs[docs.IndexOf("## Example", StringComparison.Ordinal)..];
        example = example[(example.IndexOf("```yaml\n", StringComparison.Ordinal) + "```yaml\n".Length)..];
        example = example[..example.IndexOf("```", StringComparison.Ordinal)];

        Assert.NotEmpty(ChartulaYamlConfiguration.Flatten(example));
        Assert.NotEmpty(ChartulaYamlConfiguration.Flatten(File.ReadAllText(Path.Combine(root, "chartula.yaml"))));
    }

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Chartula.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Chartula.slnx not found above the test output.");
    }
}
