using System.Text.RegularExpressions;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Faithfulness;

/// <summary>
/// Default <see cref="IRuleBasedFaithfulnessChecker"/>. It flags crude, obvious
/// hallucinations with no LLM call and zero token cost:
/// <list type="bullet">
///   <item>a number in the output that is not present in the fact base;</item>
///   <item>a quoted or backticked name that does not appear in the facts.</item>
/// </list>
/// Flags are advisory. They point a reviewer at passages and do not fail the run.
/// </summary>
/// <remarks>
/// Both checks ask the same decidable question: is this token in the fact base?
/// Breaking-change claims are deliberately not checked here. The output is free prose
/// without a marker to anchor to. Telling "this release contains a breaking change"
/// apart from prose that merely uses the word needs judgement, not a lookup, and a
/// regex on the word flags every mention.
/// <see cref="IThoroughFaithfulnessChecker"/> checks that claim instead. Its prompt
/// already covers distortions of meaning, and it sees which facts are marked breaking.
/// </remarks>
public sealed partial class RuleBasedFaithfulnessChecker : IRuleBasedFaithfulnessChecker
{
    [GeneratedRegex(@"\d+(?:\.\d+)*", RegexOptions.CultureInvariant)]
    private static partial Regex Number();

    // Spans in backticks or double quotes: the usual shape of an invented API,
    // feature or option name.
    [GeneratedRegex("`([^`]+)`|\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedName();

    public FaithfulnessReport Check(string output, FactBase factBase)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(factBase);

        string haystack = BuildHaystack(factBase);
        HashSet<string> allowedNumbers = CollectAllowedNumbers(factBase, haystack);

        List<string> findings = [];

        foreach (Match match in Number().Matches(output))
        {
            if (!allowedNumbers.Contains(match.Value))
            {
                findings.Add($"The number '{match.Value}' is not supported by the facts.");
            }
        }

        foreach (Match match in QuotedName().Matches(output))
        {
            string name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (!haystack.Contains(name.ToLowerInvariant(), StringComparison.Ordinal))
            {
                findings.Add($"'{name}' is not supported by the facts.");
            }
        }

        List<string> distinct = findings.Distinct().ToList();
        return FaithfulnessReport.Checked(distinct);
    }

    private static string BuildHaystack(FactBase factBase)
    {
        IEnumerable<string> parts = new[] { factBase.Tag }
            .Concat(factBase.Changes.SelectMany(static change =>
                new[] { change.Title, change.Description ?? string.Empty }));
        return string.Join('\n', parts).ToLowerInvariant();
    }

    private static HashSet<string> CollectAllowedNumbers(FactBase factBase, string haystack)
    {
        HashSet<string> allowed = new(Number().Matches(haystack).Select(static m => m.Value));
        foreach (ChangeFact change in factBase.Changes)
        {
            if (change.Number is { } number)
            {
                allowed.Add(number.ToString());
            }

            foreach (int issue in change.LinkedIssues)
            {
                allowed.Add(issue.ToString());
            }
        }

        return allowed;
    }
}
