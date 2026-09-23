using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Configuration;

/// <summary>
/// Loads <c>chartula.yaml</c> into configuration under the <c>Chartula:</c> prefix,
/// so a present file refines behavior while the tool still runs with sensible
/// defaults when it is absent. YAML is flattened into
/// <c>Microsoft.Extensions.Configuration</c> keys (nested maps become
/// <c>a:b</c>, sequences become <c>a:0</c>); config keys are case-insensitive, so
/// the YAML's own casing is preserved.
/// </summary>
/// <remarks>
/// The file is repository content, written by anyone whose pull request is merged,
/// so it may not set where data and credentials are sent: the endpoints, and the
/// names of the environment variables whose values go to them. One YAML value
/// could otherwise send any variable of the operator's environment to any host, or
/// point the reader at an API that fabricates the fact base. Those keys come from
/// the environment only, and a file that sets one is refused rather than ignored,
/// so nobody believes a setting is in force that is not.
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
    /// Adds <c>chartula.yaml</c> (or <c>chartula.yml</c>) from
    /// <paramref name="directory"/> if present; a no-op when neither exists.
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
    /// Flattens a YAML document into prefixed configuration key/value pairs. Throws
    /// when the document sets a key that only the environment may set, and when it is
    /// not valid YAML or holds a key or a value <see cref="ChartulaYamlSchema"/> does
    /// not allow - each named by <paramref name="fileName"/>, line and column.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string?>> Flatten(string yaml, string fileName = "chartula.yaml")
    {
        (IReadOnlyList<KeyValuePair<string, string?>> pairs, IReadOnlyList<string> problems) =
            ChartulaYamlReader.Read(yaml, fileName, RootKey);

        // First: it is the refusal that decides where credentials go.
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
