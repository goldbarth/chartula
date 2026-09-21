using System.Reflection;

namespace Chartula.Cli.Composition;

/// <summary>
/// The Chartula version, read from the assembly so that the <c>Version</c> in the
/// csproj is the only place it is written. The release workflow refuses a tag that
/// does not match it, so a binary, its tag and what it reports cannot drift apart.
/// </summary>
internal static class ToolVersion
{
    /// <summary>
    /// The version with the commit it was built from after a <c>+</c>, which the SDK
    /// appends; <c>changelog.json</c> records this form, so a file names its build.
    /// </summary>
    public static string? Informational { get; } = typeof(ToolVersion).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    /// <summary>The released version without build metadata, as a user sees it on the tag.</summary>
    public static string Release { get; } = Informational?.Split('+')[0] ?? "0.0.0";
}
