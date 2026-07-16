using Chartula.Core.Categorization;

namespace Chartula.Core.Filtering;

/// <summary>
/// Which categories are dropped from the changelog. By default internal work is
/// excluded so the changelog stays relevant to its audience. Overridable from
/// config: an explicit (possibly empty) set replaces the default.
/// </summary>
public sealed class ChangeFilterRules
{
    /// <summary>The default: exclude <see cref="ChangeCategory.Internal"/>.</summary>
    public static ChangeFilterRules Default { get; } = new([ChangeCategory.Internal]);

    public ChangeFilterRules(IEnumerable<ChangeCategory> excludedCategories)
        => ExcludedCategories = new HashSet<ChangeCategory>(excludedCategories);

    /// <summary>Categories excluded from the changelog.</summary>
    public IReadOnlySet<ChangeCategory> ExcludedCategories { get; }

    /// <summary>
    /// Builds rules from configuration-shaped category names. <c>null</c> keeps the
    /// default; any provided set (including empty) replaces it.
    /// </summary>
    /// <exception cref="InvalidOperationException">A category name is not recognized.</exception>
    public static ChangeFilterRules From(IEnumerable<string>? excludedCategoryNames)
    {
        if (excludedCategoryNames is null)
        {
            return Default;
        }

        HashSet<ChangeCategory> categories = [];
        foreach (string name in excludedCategoryNames)
        {
            if (!Enum.TryParse(name, ignoreCase: true, out ChangeCategory category))
            {
                throw new InvalidOperationException(
                    $"Unknown category '{name}' in filter rules. " +
                    $"Valid categories: {string.Join(", ", Enum.GetNames<ChangeCategory>())}.");
            }

            categories.Add(category);
        }

        return new ChangeFilterRules(categories);
    }
}
