using System.Runtime.CompilerServices;
using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Prompting;

namespace Chartula.Core.Tests.Prompting;

/// <summary>
/// The prompt text Chartula sends, held against a copy in the repository. A prompt
/// change moves output in ways no unit test catches, so it has to at least show in
/// the diff: changing the text fails here until the snapshot is updated with it.
/// <para>
/// To accept a change, run the tests with <c>CHARTULA_UPDATE_SNAPSHOTS=1</c> and
/// commit the rewritten files under <c>Snapshots/</c> next to the prompt change.
/// </para>
/// </summary>
public sealed class PromptSnapshotTests
{
    private const string UpdateVariable = "CHARTULA_UPDATE_SNAPSHOTS";

    private static readonly ChangelogPromptBuilder Builder = new();

    // Stand-ins for the parts that are data, not instructions, so the snapshot shows
    // where they go without depending on any release.
    private static readonly GroundedFacts Facts = new(["{fact}"]);

    public static TheoryData<string> Prompts => ["technical", "customer", "product", "thorough-system", "thorough-user", "prompt-hash"];

    [Theory]
    [MemberData(nameof(Prompts))]
    public void The_prompt_matches_its_snapshot(string name)
    {
        string actual = Normalize(Build(name));
        string path = SnapshotPath(name);

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllText(path, actual);
            return;
        }

        Assert.True(File.Exists(path), $"No snapshot for '{name}' at '{path}'. Run the tests with {UpdateVariable}=1 to write it.");
        string expected = Normalize(File.ReadAllText(path));
        Assert.True(
            expected == actual,
            $"The '{name}' prompt differs from its snapshot{FirstDifference(expected, actual)}. "
            + $"If the change is intended, run the tests with {UpdateVariable}=1 and commit the updated snapshot with it.");
    }

    private static string Build(string name) => name switch
    {
        "technical" => Builder.BuildRephrasePrompt(Facts, Audience.Technical).System,
        "customer" => Builder.BuildRephrasePrompt(Facts, Audience.Customer).System,
        "product" => Builder.BuildRephrasePrompt(Facts, Audience.Product).System,
        "thorough-system" => Builder.BuildFaithfulnessPrompt("{output}", Facts).System,
        "thorough-user" => Builder.BuildFaithfulnessPrompt("{output}", Facts).User,
        "prompt-hash" => ChangelogPromptBuilder.PromptHash + "\n",
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    // Git may check text out with CRLF on Windows; the prompt is the same text.
    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static string FirstDifference(string expected, string actual)
    {
        string[] expectedLines = expected.Split('\n');
        string[] actualLines = actual.Split('\n');
        for (int i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
        {
            string? was = i < expectedLines.Length ? expectedLines[i] : null;
            string? now = i < actualLines.Length ? actualLines[i] : null;
            if (was != now)
            {
                return $" from line {i + 1}:\n  snapshot: {was ?? "(end)"}\n  built:    {now ?? "(end)"}\n";
            }
        }

        return string.Empty;
    }

    // The source directory, not the build output: an update has to land in the files
    // that are committed.
    private static string SnapshotPath(string name, [CallerFilePath] string source = "")
        => Path.Combine(Path.GetDirectoryName(source)!, "Snapshots", name + ".txt");
}
