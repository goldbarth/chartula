using Chartula.Core.Categorization;

namespace Chartula.Core.Labeling;

/// <summary>
/// Label-driven curation rules. They configure:
/// <list type="bullet">
/// <item>which labels exclude a change,</item>
/// <item>which labels force a category,</item>
/// <item>which labels mark a change as user-visible or internal,</item>
/// <item>which labels say the reader has to act on the change,</item>
/// <item>whether only labeled changes are included.</item>
/// </list>
/// Every rule is optional. With <see cref="None"/> labels are ignored entirely, so the
/// tool works in repositories without labels.
/// Label matching is case-insensitive.
/// </summary>
public sealed class LabelRules
{
    /// <summary>Rules that do nothing: nothing excluded, no overrides, everything included.</summary>
    public static LabelRules None { get; } = new();

    public LabelRules(
        IEnumerable<string>? excludedLabels = null,
        IReadOnlyDictionary<string, ChangeCategory>? categoryByLabel = null,
        bool onlyIncludeLabeled = false,
        IEnumerable<string>? internalLabels = null,
        IEnumerable<string>? userFacingLabels = null,
        IEnumerable<string>? actionRequiredLabels = null)
    {
        ExcludedLabels = new HashSet<string>(excludedLabels ?? [], StringComparer.OrdinalIgnoreCase);
        InternalLabels = new HashSet<string>(internalLabels ?? [], StringComparer.OrdinalIgnoreCase);
        UserFacingLabels = new HashSet<string>(userFacingLabels ?? [], StringComparer.OrdinalIgnoreCase);
        ActionRequiredLabels = new HashSet<string>(actionRequiredLabels ?? [], StringComparer.OrdinalIgnoreCase);

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

    /// <summary>Labels saying users cannot meet the change, whatever its category.</summary>
    public IReadOnlySet<string> InternalLabels { get; }

    /// <summary>Labels saying users can meet the change.</summary>
    public IReadOnlySet<string> UserFacingLabels { get; }

    /// <summary>
    /// Labels saying the reader has to act on a change that is not breaking.
    /// A breaking change needs no such label, because it always requires action.
    /// </summary>
    public IReadOnlySet<string> ActionRequiredLabels { get; }

    /// <summary>
    /// Builds rules from configuration-shaped values, parsing category names into
    /// <see cref="ChangeCategory"/> (case-insensitive).
    /// </summary>
    /// <exception cref="InvalidOperationException">A category name is not recognized.</exception>
    public static LabelRules From(
        IEnumerable<string>? excludedLabels,
        IReadOnlyDictionary<string, string>? categoryByLabel,
        bool onlyIncludeLabeled,
        IEnumerable<string>? internalLabels = null,
        IEnumerable<string>? userFacingLabels = null,
        IEnumerable<string>? actionRequiredLabels = null)
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

        return new LabelRules(
            excludedLabels, parsed, onlyIncludeLabeled, internalLabels, userFacingLabels, actionRequiredLabels);
    }
}
