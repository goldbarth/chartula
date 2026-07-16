using Microsoft.Extensions.Configuration;
using YamlDotNet.Serialization;

namespace Chartula.Cli.Configuration;

/// <summary>
/// Loads <c>chartula.yaml</c> into configuration under the <c>Chartula:</c> prefix,
/// so a present file refines behavior while the tool still runs with sensible
/// defaults when it is absent. YAML is flattened into
/// <c>Microsoft.Extensions.Configuration</c> keys (nested maps become
/// <c>a:b</c>, sequences become <c>a:0</c>); config keys are case-insensitive, so
/// the YAML's own casing is preserved.
/// </summary>
internal static class ChartulaYamlConfiguration
{
    private const string RootKey = "Chartula";

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

        return builder.AddInMemoryCollection(Flatten(File.ReadAllText(path)));
    }

    /// <summary>Flattens a YAML document into prefixed configuration key/value pairs.</summary>
    public static IReadOnlyList<KeyValuePair<string, string?>> Flatten(string yaml)
    {
        object? root = new Deserializer().Deserialize<object?>(yaml);

        List<KeyValuePair<string, string?>> pairs = [];
        Flatten(RootKey, root, pairs);
        return pairs;
    }

    private static void Flatten(string prefix, object? node, List<KeyValuePair<string, string?>> pairs)
    {
        switch (node)
        {
            case IDictionary<object, object> map:
                foreach (KeyValuePair<object, object> entry in map)
                {
                    Flatten($"{prefix}:{entry.Key}", entry.Value, pairs);
                }

                break;

            case IList<object> list:
                for (int i = 0; i < list.Count; i++)
                {
                    Flatten($"{prefix}:{i}", list[i], pairs);
                }

                break;

            case null:
                break;

            default:
                pairs.Add(new KeyValuePair<string, string?>(prefix, node.ToString()));
                break;
        }
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
