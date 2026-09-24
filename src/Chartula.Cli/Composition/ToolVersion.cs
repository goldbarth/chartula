using System.Reflection;

namespace Chartula.Cli.Composition;

/// <summary>
/// The Chartula version, read from the assembly, so the <c>Version</c> in the csproj
/// is the only place it is written.
/// The release workflow refuses a tag that does not match it, so a binary, its tag and
/// the version it reports cannot drift apart.
/// </summary>
internal static class ToolVersion
{
    /// <summary>
    /// The version followed by <c>+</c> and the commit it was built from, as the SDK appends it.
    /// <c>changelog.json</c> records this form, so each file names the build that wrote it.
    /// </summary>
    public static string? Informational { get; } = typeof(ToolVersion).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    /// <summary>The released version without build metadata, as a user sees it on the tag.</summary>
    public static string Release { get; } = Informational?.Split('+')[0] ?? "0.0.0";
}
