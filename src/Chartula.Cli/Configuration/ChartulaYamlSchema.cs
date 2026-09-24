using Chartula.Core.Review;

namespace Chartula.Cli.Configuration;

/// <summary>The shape a key's value has to have in <c>chartula.yaml</c>.</summary>
internal enum YamlValueKind
{
    /// <summary>A map of the keys listed with it.</summary>
    Section,

    /// <summary>A single value. Its allowed values are checked where it is read.</summary>
    Scalar,

    /// <summary><c>true</c> or <c>false</c>.</summary>
    Boolean,

    /// <summary>A list of single values.</summary>
    List,

    /// <summary>A map of names the user chooses to single values, such as label to category.</summary>
    Map,
}

/// <summary>A key <c>chartula.yaml</c> may hold, and the shape of its value.</summary>
/// <param name="EnvironmentOnly">
/// A known key that is refused in the file, because it is read from the environment only.
/// It is listed so the refusal can name the variable to use instead. It is never
/// offered as a suggestion.
/// </param>
internal sealed record YamlKey(string Name, YamlValueKind Kind, IReadOnlyList<YamlKey> Keys, bool EnvironmentOnly = false)
{
    public YamlKey? Find(string name) => Keys.FirstOrDefault(key => key.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Every key <c>chartula.yaml</c> may hold.
/// Configuration binding ignores unknown keys. So a misspelled key, or a section
/// indented under another one, was silently not in effect. For example,
/// <c>faithfulness: thorough: false</c> nested under <c>llm:</c> left the paid check on.
/// The keys are listed by hand instead of reflected from the options types, because a
/// trimmed build cannot rely on reflection. <c>ChartulaYamlSchemaTests</c> checks the
/// list against those types.
/// </summary>
internal static class ChartulaYamlSchema
{
    public static YamlKey Root { get; } = Section(
        "chartula.yaml",
        Section(
            "llm",
            Scalar(nameof(LlmOptions.Provider)),
            Scalar(nameof(LlmOptions.Model)),
            Scalar(nameof(LlmOptions.MaxOutputTokens)),
            Scalar(nameof(LlmOptions.Thinking)),
            EnvironmentOnly(nameof(LlmOptions.BaseUrl)),
            EnvironmentOnly(nameof(LlmOptions.ApiKeyEnvironmentVariable))),
        Section(
            "github",
            EnvironmentOnly(nameof(GitHubOptions.ApiBaseUrl)),
            EnvironmentOnly(nameof(GitHubOptions.TokenEnvironmentVariable))),
        Section(
            "labels",
            List(nameof(LabelOptions.Exclude)),
            Map(nameof(LabelOptions.Category)),
            Boolean(nameof(LabelOptions.OnlyIncludeLabeled)),
            List(nameof(LabelOptions.Internal)),
            List(nameof(LabelOptions.UserFacing)),
            List(nameof(LabelOptions.ActionRequired))),
        Section(
            "filter",
            List(nameof(FilterOptions.ExcludeCategories))),
        Section(
            "factBase",
            Scalar(nameof(FactBaseOptions.Depth))),
        Section(
            "categories",
            List(nameof(CategoryOptions.Order)),
            Map(nameof(CategoryOptions.Names)),
            Boolean(nameof(CategoryOptions.BreakingProminent))),
        Section(
            "faithfulness",
            Boolean(nameof(FaithfulnessOptions.Thorough)),
            Scalar(nameof(FaithfulnessOptions.Model)),
            Scalar(nameof(FaithfulnessOptions.Thinking))),
        Section(
            "review",
            Boolean(nameof(ReviewOptions.Enabled))));

    private static YamlKey Section(string name, params YamlKey[] keys) => new(name, YamlValueKind.Section, keys);

    private static YamlKey Scalar(string name) => Leaf(name, YamlValueKind.Scalar);

    private static YamlKey EnvironmentOnly(string name) => Leaf(name, YamlValueKind.Scalar) with { EnvironmentOnly = true };

    private static YamlKey Boolean(string name) => Leaf(name, YamlValueKind.Boolean);

    private static YamlKey List(string name) => Leaf(name, YamlValueKind.List);

    private static YamlKey Map(string name) => Leaf(name, YamlValueKind.Map);

    // The file uses camelCase, and nameof gives the property's PascalCase.
    private static YamlKey Leaf(string name, YamlValueKind kind)
        => new(char.ToLowerInvariant(name[0]) + name[1..], kind, []);
}
