namespace Chartula.Core.Categorization;

/// <summary>
/// How categories are presented: the order they appear in, a display name per
/// category, and whether breaking changes float to the top. Drives the order (and
/// naming) of the facts fed to generation.
/// </summary>
public sealed class CategorySettings
{
    /// <summary>The default order - user-facing categories first, internal last.</summary>
    public static readonly IReadOnlyList<ChangeCategory> DefaultOrder =
    [
        ChangeCategory.Feature,
        ChangeCategory.Fix,
        ChangeCategory.Performance,
        ChangeCategory.Documentation,
        ChangeCategory.Refactor,
        ChangeCategory.Other,
        ChangeCategory.Internal,
    ];

    /// <summary>Sensible defaults: the default order, enum names, breaking shown prominently.</summary>
    public static CategorySettings Default { get; } = new(DefaultOrder, new Dictionary<ChangeCategory, string>(), true);

    private readonly Dictionary<ChangeCategory, int> _rank;
    private readonly IReadOnlyDictionary<ChangeCategory, string> _names;

    public CategorySettings(
        IReadOnlyList<ChangeCategory> order,
        IReadOnlyDictionary<ChangeCategory, string> names,
        bool breakingProminent)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(names);

        Order = order.Count > 0 ? order : DefaultOrder;
        _names = names;
        BreakingProminent = breakingProminent;

        _rank = new Dictionary<ChangeCategory, int>();
        for (int i = 0; i < Order.Count; i++)
        {
            _rank.TryAdd(Order[i], i);
        }
    }

    /// <summary>The category display order.</summary>
    public IReadOnlyList<ChangeCategory> Order { get; }

    /// <summary>Whether breaking changes are shown prominently, near the top.</summary>
    public bool BreakingProminent { get; }

    /// <summary>The display name for a category - configured, or the category name.</summary>
    public string DisplayName(ChangeCategory category)
        => _names.TryGetValue(category, out string? name) && name.Length > 0 ? name : category.ToString();

    /// <summary>The sort rank of a category; unlisted categories sort last.</summary>
    public int RankOf(ChangeCategory category) => _rank.TryGetValue(category, out int rank) ? rank : Order.Count;

    /// <summary>
    /// Builds settings from configuration-shaped values, parsing category names
    /// (case-insensitive). <c>null</c> order keeps the default order.
    /// </summary>
    /// <exception cref="InvalidOperationException">A category name is not recognized.</exception>
    public static CategorySettings From(
        IEnumerable<string>? order,
        IReadOnlyDictionary<string, string>? names,
        bool breakingProminent)
    {
        List<ChangeCategory> parsedOrder = order is null
            ? [.. DefaultOrder]
            : order.Select(name => ParseCategory(name, "order")).ToList();

        Dictionary<ChangeCategory, string> parsedNames = new();
        if (names is not null)
        {
            foreach (KeyValuePair<string, string> entry in names)
            {
                parsedNames[ParseCategory(entry.Key, "names")] = entry.Value;
            }
        }

        return new CategorySettings(parsedOrder, parsedNames, breakingProminent);
    }

    private static ChangeCategory ParseCategory(string name, string where)
        => Enum.TryParse(name, ignoreCase: true, out ChangeCategory category)
            ? category
            : throw new InvalidOperationException(
                $"Unknown category '{name}' in categories {where}. " +
                $"Valid categories: {string.Join(", ", Enum.GetNames<ChangeCategory>())}.");
}
