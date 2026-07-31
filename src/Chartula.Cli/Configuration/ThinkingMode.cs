namespace Chartula.Cli.Configuration;

/// <summary>
/// Whether the model reasons before it answers. Thinking is billed as output
/// tokens, so this is a cost knob as much as a quality one.
/// </summary>
public enum ThinkingMode
{
    /// <summary>
    /// Send nothing, and let each model apply its own default. Models disagree about
    /// what that means - some think, some do not - so this is the only mode whose
    /// behavior depends on the configured model.
    /// </summary>
    ProviderDefault,

    /// <summary>No thinking, on every model.</summary>
    Disabled,

    /// <summary>Adaptive thinking, on every model that supports it.</summary>
    Adaptive,
}

/// <summary>
/// Parses the configured thinking mode, defaulting to the provider's own behavior
/// when it is not set.
/// </summary>
public static class ThinkingModeParser
{
    /// <summary>The default when thinking is not configured: whatever the model does on its own.</summary>
    public const ThinkingMode Default = ThinkingMode.ProviderDefault;

    /// <summary>
    /// Maps a configuration value to a <see cref="ThinkingMode"/>. <c>null</c> or
    /// blank yields <see cref="Default"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value is not recognized.</exception>
    public static ThinkingMode Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Default;
        }

        string normalized = value.Trim().Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "providerdefault" or "default" => ThinkingMode.ProviderDefault,
            "disabled" or "off" or "false" => ThinkingMode.Disabled,
            "adaptive" or "on" or "true" => ThinkingMode.Adaptive,
            _ => throw new InvalidOperationException(
                $"Unknown llm.thinking value '{value}'. Valid values: provider-default, disabled, adaptive " +
                "(aliases: default, off, on)."),
        };
    }
}
