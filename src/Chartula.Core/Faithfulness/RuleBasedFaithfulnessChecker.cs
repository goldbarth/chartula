using System.Text.RegularExpressions;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Faithfulness;

/// <summary>
/// Default <see cref="IRuleBasedFaithfulnessChecker"/>. It flags crude, obvious
/// hallucinations with no LLM call and zero token cost:
/// <list type="bullet">
///   <item>a number in the output that is not present in the fact base;</item>
///   <item>a quoted or backticked name that does not appear in the facts;</item>
///   <item>a breaking-change claim when no fact is marked breaking.</item>
/// </list>
/// Flags are advisory - they surface passages for review, not hard failures.
/// </summary>
public sealed partial class RuleBasedFaithfulnessChecker : IRuleBasedFaithfulnessChecker
{
    [GeneratedRegex(@"\d+(?:\.\d+)*", RegexOptions.CultureInvariant)]
    private static partial Regex Number();

    // Backtick- or double-quote-delimited spans - the usual shape of an invented
    // API, feature, or option name.
    [GeneratedRegex("`([^`]+)`|\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedName();

    [GeneratedRegex(@"\bbreaking\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BreakingClaim();

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

        if (BreakingClaim().IsMatch(output) && !factBase.Changes.Any(static change => change.IsBreaking))
        {
            findings.Add("The output claims a breaking change, but the facts mark none.");
        }

        List<string> distinct = findings.Distinct().ToList();
        return new FaithfulnessReport(distinct.Count == 0, distinct);
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
