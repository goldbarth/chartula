using Chartula.Core.Categorization;

namespace Chartula.Core.Labeling;

/// <summary>
/// Label-driven curation rules: which labels exclude a change, which labels force
/// a category, and whether only labeled changes are included. All optional -
/// <see cref="None"/> means labels are ignored entirely and the tool works with
/// no labels at all. Label matching is case-insensitive.
/// </summary>
public sealed class LabelRules
{
    /// <summary>Rules that do nothing: nothing excluded, no overrides, everything included.</summary>
    public static LabelRules None { get; } = new();

    public LabelRules(
        IEnumerable<string>? excludedLabels = null,
        IReadOnlyDictionary<string, ChangeCategory>? categoryByLabel = null,
        bool onlyIncludeLabeled = false)
    {
        ExcludedLabels = new HashSet<string>(excludedLabels ?? [], StringComparer.OrdinalIgnoreCase);

        Dictionary<string, ChangeCategory> categories = new(StringComparer.OrdinalIgnoreCase);
        if (categoryByLabel is not null)
        {
            foreach (KeyValuePair<string, ChangeCategory> entry in categoryByLabel)
            {
                categories[entry.Key] = entry.Value;
            }
        }

        CategoryByLabel = categories;
        OnlyIncludeLabeled = onlyIncludeLabeled;
    }

    /// <summary>Labels that exclude a change from the changelog.</summary>
    public IReadOnlySet<string> ExcludedLabels { get; }

    /// <summary>Labels that force a change into a specific category.</summary>
    public IReadOnlyDictionary<string, ChangeCategory> CategoryByLabel { get; }

    /// <summary>When true, only changes carrying at least one label are included.</summary>
    public bool OnlyIncludeLabeled { get; }

    /// <summary>
    /// Builds rules from configuration-shaped values, parsing category names into
    /// <see cref="ChangeCategory"/> (case-insensitive).
    /// </summary>
    /// <exception cref="InvalidOperationException">A category name is not recognized.</exception>
    public static LabelRules From(
        IEnumerable<string>? excludedLabels,
        IReadOnlyDictionary<string, string>? categoryByLabel,
        bool onlyIncludeLabeled)
    {
        Dictionary<string, ChangeCategory>? parsed = null;
        if (categoryByLabel is not null)
        {
            parsed = new Dictionary<string, ChangeCategory>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> entry in categoryByLabel)
            {
                if (!Enum.TryParse(entry.Value, ignoreCase: true, out ChangeCategory category))
                {
                    throw new InvalidOperationException(
                        $"Unknown category '{entry.Value}' for label '{entry.Key}'. " +
                        $"Valid categories: {string.Join(", ", Enum.GetNames<ChangeCategory>())}.");
                }

                parsed[entry.Key] = category;
            }
        }

        return new LabelRules(excludedLabels, parsed, onlyIncludeLabeled);
    }
}
