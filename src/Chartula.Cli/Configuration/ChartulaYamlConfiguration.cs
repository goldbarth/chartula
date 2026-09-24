using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Configuration;

/// <summary>
/// Loads <c>chartula.yaml</c> into configuration under the <c>Chartula:</c> prefix.
/// A present file refines the behaviour, and without it the tool runs with sensible defaults.
/// The YAML is flattened into <c>Microsoft.Extensions.Configuration</c> keys: nested
/// maps become <c>a:b</c>, sequences become <c>a:0</c>. Configuration keys are
/// case-insensitive, so the YAML's own casing is kept.
/// </summary>
/// <remarks>
/// The file is repository content, written by anyone whose pull request is merged.
/// So it may not set where data and credentials are sent: the endpoints, and the names
/// of the environment variables whose values are sent to them.
/// Otherwise one YAML value could send any variable of the operator's environment to
/// any host, or point the reader at an API that fabricates the fact base.
/// These keys come from the environment only. A file that sets one is refused, not
/// ignored, so nobody believes a setting is in effect when it is not.
/// </remarks>
internal static class ChartulaYamlConfiguration
{
    private const string RootKey = "Chartula";

    /// <summary>The keys the file may not set, each with the variable that sets it instead.</summary>
    private static readonly Dictionary<string, string> EnvironmentOnlyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        [$"{LlmOptions.SectionName}:BaseUrl"] = LlmOptions.BaseUrlVariable,
        [$"{LlmOptions.SectionName}:ApiKeyEnvironmentVariable"] = "Chartula__Llm__ApiKeyEnvironmentVariable",
        [$"{GitHubOptions.SectionName}:ApiBaseUrl"] = GitHubOptions.ApiBaseUrlVariable,
        [$"{GitHubOptions.SectionName}:TokenEnvironmentVariable"] = "Chartula__GitHub__TokenEnvironmentVariable",
    };

    /// <summary>
    /// Adds <c>chartula.yaml</c> (or <c>chartula.yml</c>) from <paramref name="directory"/>
    /// if present. Does nothing when neither exists.
    /// </summary>
    public static IConfigurationBuilder AddChartulaYaml(this IConfigurationBuilder builder, string directory)
    {
        string? path = FindConfigFile(directory);
        if (path is null)
        {
            return builder;
        }

        return builder.AddInMemoryCollection(Flatten(File.ReadAllText(path), Path.GetFileName(path)));
    }

    /// <summary>
    /// Flattens a YAML document into prefixed configuration key/value pairs.
    /// Throws when the document:
    /// <list type="bullet">
    /// <item>sets a key that only the environment may set,</item>
    /// <item>is not valid YAML,</item>
    /// <item>holds a key or a value that <see cref="ChartulaYamlSchema"/> does not allow.</item>
    /// </list>
    /// Each problem names <paramref name="fileName"/>, line and column.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string?>> Flatten(string yaml, string fileName = "chartula.yaml")
    {
        (IReadOnlyList<KeyValuePair<string, string?>> pairs, IReadOnlyList<string> problems) =
            ChartulaYamlReader.Read(yaml, fileName, RootKey);

        // Refuse environment-only keys first, because they decide where credentials go.
        RefuseEnvironmentOnlyKeys(pairs);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(string.Join('\n', problems));
        }

        return pairs;
    }

    private static void RefuseEnvironmentOnlyKeys(IEnumerable<KeyValuePair<string, string?>> pairs)
    {
        List<string> refused = [.. pairs
            .Where(pair => EnvironmentOnlyKeys.ContainsKey(pair.Key))
            .Select(pair => $"{pair.Key[(RootKey.Length + 1)..].Replace(':', '.')} (set {EnvironmentOnlyKeys[pair.Key]} instead)")];
        if (refused.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"chartula.yaml sets {string.Join(", ", refused)}. " +
            "Endpoints and the names of credential variables are read from the environment only: " +
            "the file is repository content, and these decide where release data and credentials are sent.");
    }

    private static string? FindConfigFile(string directory)
    {
        foreach (string name in (string[])["chartula.yaml", "chartula.yml"])
        {
            string candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
