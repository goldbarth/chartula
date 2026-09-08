namespace Chartula.Cli.Commands;

/// <summary>
/// A tiny parser for <c>--name value</c> options and <c>--name</c> flags, enough
/// for the scaffold's <c>generate</c> and <c>preview</c> commands.
/// </summary>
internal static class CommandLineArguments
{
    /// <summary>Reads the value following <paramref name="name"/>, or <c>null</c>.</summary>
    public static string? GetOption(IReadOnlyList<string> args, string name)
    {
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// Every value given for <paramref name="name"/>, whether it was repeated or
    /// written once with commas between the values. Both spellings appear in the
    /// wild and neither is worth making a person remember, so both are read.
    /// </summary>
    public static IReadOnlyList<string> GetOptions(IReadOnlyList<string> args, string name)
    {
        List<string> values = [];
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.Ordinal))
            {
                continue;
            }

            values.AddRange(args[i + 1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return values;
    }

    /// <summary>Whether <paramref name="name"/> is present as a flag.</summary>
    public static bool HasFlag(IReadOnlyList<string> args, string name)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
