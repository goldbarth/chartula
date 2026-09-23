using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Chartula.Cli.Configuration;

/// <summary>
/// Reads <c>chartula.yaml</c> against <see cref="ChartulaYamlSchema"/> and names
/// everything it cannot read as meant by file, line and column. The file is written by
/// hand, and indentation is its syntax: a key indented one level too deep either stops
/// the parser, which used to end the run with an unhandled exception, or parses as a
/// key of another section, which configuration binding ignores without a word (#237).
/// Both are refused here, before any work starts.
/// </summary>
internal static partial class ChartulaYamlReader
{
    private const string Indentation =
        "Check the indentation: the keys of one section line up, indented under it with spaces.";

    /// <summary>
    /// The configuration pairs <paramref name="yaml"/> holds under <paramref name="prefix"/>,
    /// and a line per key or value it cannot use. A syntax error throws instead: nothing
    /// after it can be read.
    /// </summary>
    public static (IReadOnlyList<KeyValuePair<string, string?>> Pairs, IReadOnlyList<string> Problems) Read(
        string yaml, string fileName, string prefix)
    {
        Reading reading = new(fileName);
        YamlNode? root = Parse(yaml, fileName);
        if (root is YamlMappingNode sections)
        {
            reading.Section(ChartulaYamlSchema.Root, sections, prefix, path: null);
        }
        else if (root is not null && !IsNull(root))
        {
            reading.Problem(root, $"the file has to hold sections such as 'llm:' and 'labels:', not {Describe(root)}.");
        }

        return (reading.Pairs, reading.Problems);
    }

    private static YamlNode? Parse(string yaml, string fileName)
    {
        YamlStream stream = new();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlException ex)
        {
            throw new InvalidOperationException(DescribeSyntaxError(yaml, fileName, ex), ex);
        }

        return stream.Documents.Count switch
        {
            0 => null,
            1 => stream.Documents[0].RootNode,
            _ => throw new InvalidOperationException(
                $"{Located(fileName, stream.Documents[1].RootNode.Start)}: a second YAML document starts here. " +
                "The file is read as one document, so everything from here on would be ignored."),
        };
    }

    /// <summary>
    /// A parser error with what it most likely means. YamlDotNet names a tab at the
    /// start of the mapping it broke rather than on its own line, so the tab is looked
    /// up here; and a line indented under a key that already has a value is the most
    /// common way to break a hand-written file, so it is named as such.
    /// </summary>
    private static string DescribeSyntaxError(string yaml, string fileName, YamlException ex)
    {
        string[] lines = yaml.ReplaceLineEndings("\n").Split('\n');
        string reason = ParserLocation().Replace(ex.Message, string.Empty).TrimEnd('.');

        if (reason.Contains("tab", StringComparison.OrdinalIgnoreCase) && FirstTabIndentedLine(lines) is var (line, column))
        {
            return $"{fileName}, line {line}, column {column}: this line is indented with a tab. YAML indents with spaces only.";
        }

        // Not a syntax error in YAML's own terms, but the parser is what finds it.
        if (reason.StartsWith("Duplicate key ", StringComparison.Ordinal))
        {
            return $"{Located(fileName, ex.Start)}: '{reason["Duplicate key ".Length..]}' is set a second time here; " +
                   "the second would replace the first. Set it once.";
        }

        string hint = reason.Contains("flow", StringComparison.OrdinalIgnoreCase)
            ? "Check that every '[' and '{' on it is closed."
            : IndentationHint(lines, (int)ex.Start.Line - 1);
        return $"{Located(fileName, ex.Start)}: this is not valid YAML ({reason}). {hint}";
    }

    private static string IndentationHint(string[] lines, int index)
    {
        if (index <= 0 || index >= lines.Length || !IsContent(lines[index]))
        {
            return Indentation;
        }

        int above = index - 1;
        while (above >= 0 && !IsContent(lines[above]))
        {
            above--;
        }

        if (above < 0)
        {
            return Indentation;
        }

        int indent = IndentOf(lines[index]);
        int aboveIndent = IndentOf(lines[above]);
        if (indent > aboveIndent && HasValue(lines[above]))
        {
            return $"Check the indentation: line {index + 1} is indented further than line {above + 1} " +
                   $"({indent} spaces against {aboveIndent}), but line {above + 1} already has a value, " +
                   "so nothing can be nested under it. The keys of one section line up.";
        }

        // Moving out to a level no key above it is at: neither in the section above nor out of it.
        List<int> levels = [.. lines[..index].Where(IsContent).Select(IndentOf).Distinct().Order()];
        if (indent < aboveIndent && !levels.Contains(indent))
        {
            return $"Check the indentation: line {index + 1} is indented {indent} spaces, which lines up with " +
                   $"no key above it (they are at {string.Join(" or ", levels)}). The keys of one section line up.";
        }

        return Indentation;
    }

    private static (int Line, int Column)? FirstTabIndentedLine(string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            string leading = lines[i][..(lines[i].Length - lines[i].TrimStart(' ', '\t').Length)];
            if (leading.Contains('\t'))
            {
                return (i + 1, leading.IndexOf('\t') + 1);
            }
        }

        return null;
    }

    private static bool IsContent(string line) => line.Trim() is { Length: > 0 } text && !text.StartsWith('#');

    private static int IndentOf(string line) => line.Length - line.TrimStart(' ').Length;

    // 'key: value', as opposed to 'key:' that opens a section.
    private static bool HasValue(string line)
    {
        int colon = line.IndexOf(": ", StringComparison.Ordinal);
        return colon >= 0 && line[(colon + 2)..].Trim() is { Length: > 0 } value && !value.StartsWith('#');
    }

    private static string Located(string fileName, Mark mark) => $"{fileName}, line {mark.Line}, column {mark.Column}";

    // YAML's null: an empty value, '~' or 'null'. It leaves the setting unset, as it always has.
    private static bool IsNull(YamlNode node)
        => node is YamlScalarNode { Style: ScalarStyle.Plain or ScalarStyle.Any, Value: null or "" or "~" or "null" or "Null" or "NULL" };

    private static string Describe(YamlNode node) => node switch
    {
        YamlScalarNode scalar => $"the value '{scalar.Value}'",
        YamlSequenceNode => "a list",
        _ => "keys nested under it",
    };

    /// <summary>YamlDotNet's message begins with the location it also carries as data.</summary>
    [GeneratedRegex(@"^\(Line: \d+, Col: \d+, Idx: \d+\) - \(Line: \d+, Col: \d+, Idx: \d+\): ")]
    private static partial Regex ParserLocation();

    /// <summary>One pass over the file: the pairs it sets, and what is wrong with it.</summary>
    private sealed class Reading(string fileName)
    {
        public List<KeyValuePair<string, string?>> Pairs { get; } = [];

        public List<string> Problems { get; } = [];

        public void Problem(YamlNode node, string text) => Problems.Add($"{Located(fileName, node.Start)}: {text}");

        public void Section(YamlKey section, YamlMappingNode map, string prefix, string? path)
        {
            foreach ((YamlNode keyNode, YamlNode value) in map.Children)
            {
                if (keyNode is not YamlScalarNode { Value: { Length: > 0 } name })
                {
                    Problem(keyNode, "a key has to be a plain name.");
                    continue;
                }

                if (section.Find(name) is not { } key)
                {
                    Problem(keyNode, Unknown(section, name, path));
                    continue;
                }

                // The file's own spelling: configuration keys are case-insensitive, and the
                // refusal of an environment-only key quotes the file back as written.
                Value(key, value, $"{prefix}:{name}", path is null ? key.Name : $"{path}.{key.Name}");
            }
        }

        private void Value(YamlKey key, YamlNode node, string configKey, string path)
        {
            if (IsNull(node))
            {
                return;
            }

            switch (key.Kind)
            {
                case YamlValueKind.Section when node is YamlMappingNode map:
                    Section(key, map, configKey, path);
                    break;
                case YamlValueKind.Section:
                    Problem(node, $"'{path}' is a section: its keys go on the lines below it, indented, not {Describe(node)}.");
                    break;

                case YamlValueKind.Scalar when node is YamlScalarNode scalar:
                    Pairs.Add(new(configKey, scalar.Value));
                    break;
                case YamlValueKind.Boolean when node is YamlScalarNode scalar:
                    if (bool.TryParse(scalar.Value, out _))
                    {
                        Pairs.Add(new(configKey, scalar.Value));
                    }
                    else
                    {
                        Problem(node, $"'{path}' is '{scalar.Value}'; expected true or false.");
                    }

                    break;
                case YamlValueKind.Scalar or YamlValueKind.Boolean:
                    Problem(node, node is YamlMappingNode
                        ? $"'{path}' takes a single value, but has keys nested under it. {Indentation}"
                        : $"'{path}' takes a single value, not a list.");
                    break;

                case YamlValueKind.List when node is YamlSequenceNode list:
                    for (int i = 0; i < list.Children.Count; i++)
                    {
                        Single(list.Children[i], $"{configKey}:{i}", $"the items of '{path}'");
                    }

                    break;
                case YamlValueKind.List:
                    Problem(node, $"'{path}' takes a list, such as [a, b], not {Describe(node)}.");
                    break;

                case YamlValueKind.Map when node is YamlMappingNode map:
                    // The names are the user's own - labels, categories - so none is unknown.
                    foreach ((YamlNode name, YamlNode value) in map.Children)
                    {
                        Single(value, $"{configKey}:{(name as YamlScalarNode)?.Value}", $"the values under '{path}'");
                    }

                    break;
                case YamlValueKind.Map:
                    Problem(node, $"'{path}' takes one 'name: value' per line below it, indented, not {Describe(node)}.");
                    break;
            }
        }

        private void Single(YamlNode node, string configKey, string what)
        {
            if (node is YamlScalarNode scalar && !IsNull(node))
            {
                Pairs.Add(new(configKey, scalar.Value));
            }
            else if (!IsNull(node))
            {
                Problem(node, $"{what} are single values, not {Describe(node)}.");
            }
        }

        /// <summary>
        /// An unknown key, with what it most likely is: a section or a key of another
        /// section, indented to the wrong level, or a misspelling.
        /// </summary>
        private static string Unknown(YamlKey section, string name, string? path)
        {
            YamlKey root = ChartulaYamlSchema.Root;
            string where = path is null ? "at the top level" : $"in '{path}'";

            if (path is not null && root.Find(name) is { Kind: YamlValueKind.Section } other)
            {
                return $"'{other.Name}' is not a key of '{path}' but a section of its own. " +
                       $"Check the indentation: '{other.Name}:' starts at the beginning of its line, like '{path}:'.";
            }

            List<string> owners = [.. root.Keys.Where(s => s != section && s.Find(name) is not null).Select(s => $"'{s.Name}'")];
            if (owners.Count > 0)
            {
                return $"unknown key '{name}' {where}: it is a key of {string.Join(" and ", owners)}. " +
                       "Check the indentation: it belongs indented under its section.";
            }

            if (Closest(name, section.Keys) is { } guess)
            {
                return $"unknown key '{name}' {where}. Did you mean '{guess}'?";
            }

            List<string> valid = [.. section.Keys.Where(key => !key.EnvironmentOnly).Select(key => key.Name)];
            return valid.Count > 0
                ? $"unknown key '{name}' {where}. Valid keys: {string.Join(", ", valid)}."
                : $"unknown key '{name}' {where}. '{path}' has no keys this file may set: its settings are read from the environment.";
        }

        private static string? Closest(string name, IReadOnlyList<YamlKey> keys)
            => keys
                .Where(key => !key.EnvironmentOnly)
                .Select(key => (key.Name, Distance: Distance(name.ToLowerInvariant(), key.Name.ToLowerInvariant())))
                .Where(candidate => candidate.Distance <= 2)
                .OrderBy(candidate => candidate.Distance)
                .Select(candidate => candidate.Name)
                .FirstOrDefault();

        // Levenshtein: a transposed or dropped letter or two is a typo; more is another word.
        private static int Distance(string a, string b)
        {
            int[] previous = [.. Enumerable.Range(0, b.Length + 1)];
            for (int i = 1; i <= a.Length; i++)
            {
                int[] current = new int[b.Length + 1];
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                    current[j] = Math.Min(substitution, Math.Min(previous[j], current[j - 1]) + 1);
                }

                previous = current;
            }

            return previous[b.Length];
        }
    }
}
