using System.Collections;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Configuration;

/// <summary>
/// A run's configuration: <c>chartula.yaml</c>, overridden by <c>Chartula__</c>
/// environment variables, plus the credentials those settings name.
/// The environment is not loaded whole. Otherwise any setting that names a variable
/// could read every variable of the operator's shell. Only the credential variables
/// Chartula actually uses are loaded, by name.
/// </summary>
internal static class ChartulaConfiguration
{
    /// <summary>The prefix of a setting given as an environment variable.</summary>
    public const string EnvironmentPrefix = "Chartula__";

    // Windows variable names are case-insensitive, everywhere else they are not.
    private static readonly StringComparer NameComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>The configuration for a run started in <paramref name="directory"/>.</summary>
    public static IConfiguration Build(string directory)
        => Build(new ConfigurationBuilder().AddChartulaYaml(directory), Environment.GetEnvironmentVariables());

    /// <summary>
    /// The configuration from a file source and an environment.
    /// <see cref="Build(string)"/> passes the process environment.
    /// </summary>
    public static IConfiguration Build(IConfigurationBuilder file, IDictionary environment)
    {
        // Build the settings first, because they decide which credentials are read.
        IConfiguration settings = file
            .AddInMemoryCollection(Settings(environment))
            .Build();

        return new ConfigurationBuilder()
            .AddConfiguration(settings)
            .AddInMemoryCollection(Credentials(settings, environment))
            .Build();
    }

    // The same mapping as the stock environment-variable provider, prefix kept:
    // Chartula__Llm__Model becomes Chartula:Llm:Model.
    private static IEnumerable<KeyValuePair<string, string?>> Settings(IDictionary environment)
    {
        foreach (DictionaryEntry variable in environment)
        {
            string name = (string)variable.Key;
            if (name.Length > EnvironmentPrefix.Length
                && name.StartsWith(EnvironmentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                yield return new KeyValuePair<string, string?>(
                    "Chartula:" + name[EnvironmentPrefix.Length..].Replace("__", ":"), (string?)variable.Value);
            }
        }
    }

    // Load the default key variables of both providers, whichever provider runs. Which
    // one applies is decided later, and Chartula uses both.
    private static IEnumerable<KeyValuePair<string, string?>> Credentials(IConfiguration settings, IDictionary environment)
    {
        HashSet<string> names = new(NameComparer)
        {
            LlmProviderDefaults.For(LlmProvider.Anthropic).ApiKeyEnvironmentVariable,
            LlmProviderDefaults.For(LlmProvider.OpenAiCompatible).ApiKeyEnvironmentVariable,
            new GitHubOptions().TokenEnvironmentVariable,
        };
        foreach (string key in (string[])[
                     $"{LlmOptions.SectionName}:ApiKeyEnvironmentVariable",
                     $"{GitHubOptions.SectionName}:TokenEnvironmentVariable"])
        {
            if (settings[key] is { Length: > 0 } name)
            {
                names.Add(name);
            }
        }

        foreach (DictionaryEntry variable in environment)
        {
            if (names.Contains((string)variable.Key))
            {
                yield return new KeyValuePair<string, string?>((string)variable.Key, (string?)variable.Value);
            }
        }
    }
}
