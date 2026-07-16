namespace Chartula.Core.Facts;

/// <summary>
/// Parses the configured fact-base depth, defaulting to the middle mode when it
/// is not set. Accepts the enum names plus friendly aliases.
/// </summary>
public static class FactBaseDepthParser
{
    /// <summary>The default when depth is not configured: title and description.</summary>
    public const FactBaseDepth Default = FactBaseDepth.TitleAndDescription;

    /// <summary>
    /// Maps a configuration value to a <see cref="FactBaseDepth"/>. <c>null</c> or
    /// blank yields <see cref="Default"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value is not recognized.</exception>
    public static FactBaseDepth Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Default;
        }

        string normalized = value.Trim().Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "titleonly" or "title" => FactBaseDepth.TitleOnly,
            "titleanddescription" or "description" => FactBaseDepth.TitleAndDescription,
            "titledescriptionandissues" or "full" or "issues" => FactBaseDepth.TitleDescriptionAndIssues,
            _ => throw new InvalidOperationException(
                $"Unknown fact-base depth '{value}'. Valid values: title-only, title-and-description, " +
                "title-description-and-issues (aliases: title, description, full)."),
        };
    }
}
