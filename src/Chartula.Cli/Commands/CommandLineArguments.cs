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
