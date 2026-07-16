using System.Text.RegularExpressions;
using Chartula.Core.Curation;

namespace Chartula.Core.Categorization;

/// <summary>
/// Categorizes changes from Conventional Commit conventions in the title
/// (<c>type(scope)!: subject</c>) plus a <c>BREAKING CHANGE</c> note in the body.
/// Pure and deterministic; no LLM. Unknown or prefix-less titles get
/// <see cref="ChangeCategory.Other"/>.
/// </summary>
public sealed partial class ConventionalCommitCategorizer : IChangeCategorizer
{
    // type, optional (scope), optional ! marker, then the colon.
    [GeneratedRegex(@"^(?<type>[a-zA-Z]+)(?:\([^)]*\))?(?<bang>!)?:", RegexOptions.CultureInvariant)]
    private static partial Regex ConventionalPrefix();

    public ChangeClassification Classify(ReleaseChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        Match match = ConventionalPrefix().Match(change.Title ?? string.Empty);

        string type = match.Success ? match.Groups["type"].Value.ToLowerInvariant() : string.Empty;
        bool isBreaking =
            match.Groups["bang"].Success
            || type == "breaking"
            || MentionsBreakingChange(change.Description);

        ChangeCategory category = match.Success ? MapType(type) : ChangeCategory.Other;
        return new ChangeClassification(category, isBreaking);
    }

    private static ChangeCategory MapType(string type) => type switch
    {
        "feat" or "feature" => ChangeCategory.Feature,
        "fix" or "bugfix" => ChangeCategory.Fix,
        "perf" => ChangeCategory.Performance,
        "docs" or "doc" => ChangeCategory.Documentation,
        "refactor" => ChangeCategory.Refactor,
        "build" or "ci" or "chore" or "test" or "tests" or "style" or "revert" => ChangeCategory.Internal,
        _ => ChangeCategory.Other,
    };

    private static bool MentionsBreakingChange(string? description)
        => description is not null
           && (description.Contains("BREAKING CHANGE", StringComparison.OrdinalIgnoreCase)
               || description.Contains("BREAKING-CHANGE", StringComparison.OrdinalIgnoreCase));
}
