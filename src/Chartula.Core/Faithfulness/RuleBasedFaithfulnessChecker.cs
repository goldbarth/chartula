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
    // A number with thousands separators, such as "8,000" or "1,234.50", or a plain run
    // of digits with its dots, such as "8000", "1.5" or "10.0.1". The first form needs a
    // comma directly followed by exactly three digits, so "1, 2, 3" and "12,34" stay
    // lists. Three-digit numbers listed without a space, "100,200", read as one number:
    // text alone cannot tell them from a hundred thousand, and prose puts the space.
    [GeneratedRegex(@"\d{1,3}(?:,\d{3})+(?!\d)(?:\.\d+)*|\d+(?:\.\d+)*", RegexOptions.CultureInvariant)]
    private static partial Regex Number();

    // Spans in backticks or double quotes: the usual shape of an invented API,
    // feature or option name.
    [GeneratedRegex("`([^`]+)`|\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedName();

    // A code fence: three or more backticks. A rendering is one line per entry, so a
    // fenced block a description held lands on one line, where its backticks would pair
    // with those of the code spans around it.
    [GeneratedRegex("`{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex Fence();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    public FaithfulnessReport Check(string output, FactBase factBase)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(factBase);

        string haystack = BuildHaystack(factBase);
        HashSet<string> allowedNumbers = CollectAllowedNumbers(factBase, haystack);

        // The rendering and the facts are compared in the same form: a rendering folds
        // every run of whitespace into one space (RenderingComposer.SingleLine), while a
        // description keeps its line breaks, so a name it wraps would otherwise not be found (#315).
        string comparable = Comparable(output);
        List<string> findings = [];

        foreach (Match match in Number().Matches(comparable))
        {
            if (!allowedNumbers.Contains(WithoutSeparators(match.Value)))
            {
                findings.Add($"The number '{match.Value}' is not supported by the facts.");
            }
        }

        foreach (Match match in QuotedName().Matches(comparable))
        {
            string name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (!haystack.Contains(name.ToLowerInvariant(), StringComparison.Ordinal))
            {
                findings.Add($"'{name}' is not supported by the facts.");
            }
        }

        // A finding here is a token no fact contains, so it names no fact.
        return FaithfulnessReport.Checked([.. findings.Distinct().Select(static finding => new FaithfulnessFlag(finding))]);
    }

    private static string BuildHaystack(FactBase factBase)
    {
        IEnumerable<string> parts = new[] { factBase.Tag }
            .Concat(factBase.Changes.SelectMany(static change =>
                new[] { change.Title, change.Description ?? string.Empty }));
        // Joined by a character no rendering contains, so a name cannot be found across
        // the end of one fact and the start of the next.
        return string.Join('\0', parts.Select(Comparable)).ToLowerInvariant();
    }

    private static string Comparable(string text) => Whitespace().Replace(Fence().Replace(text, " "), " ");

    // "8,000" and "8000" are one number (#350). Compared as written, the facts' "8,000"
    // did not back a rendering's "8000", and read as "8" and "000" it backed both of those.
    private static string WithoutSeparators(string number) => number.Replace(",", string.Empty, StringComparison.Ordinal);

    private static HashSet<string> CollectAllowedNumbers(FactBase factBase, string haystack)
    {
        HashSet<string> allowed = new(Number().Matches(haystack).Select(static m => WithoutSeparators(m.Value)));
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
