namespace Chartula.Cli.Configuration;

/// <summary>
/// How much the model reasons before it answers, in one vocabulary for every
/// provider. Each adapter translates it to its own mechanism - Anthropic's thinking
/// and effort, OpenAI's <c>reasoning_effort</c> - so the same value in
/// <c>chartula.yaml</c> asks for the same thing whichever provider serves it.
/// Reasoning is billed as output tokens, so this is a cost knob as much as a
/// quality one.
/// </summary>
public enum ThinkingMode
{
    /// <summary>
    /// Send nothing, and let each model apply its own default. Models disagree about
    /// what that means - some reason, some do not - so this is the only mode whose
    /// behavior depends on the configured model.
    /// </summary>
    ProviderDefault,

    /// <summary>No reasoning.</summary>
    Disabled,

    /// <summary>Reasoning at low effort.</summary>
    Low,

    /// <summary>Reasoning at medium effort.</summary>
    Medium,

    /// <summary>
    /// Reasoning at high effort. What Anthropic's adaptive thinking does when no
    /// effort is given, so <c>adaptive</c> reads as this.
    /// </summary>
    High,

    /// <summary>Reasoning at the highest effort both providers name (<c>xhigh</c>).</summary>
    ExtraHigh,
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
            "disabled" or "off" or "false" or "none" => ThinkingMode.Disabled,
            "low" => ThinkingMode.Low,
            "medium" => ThinkingMode.Medium,
            // adaptive and on predate the effort levels; both meant thinking at the
            // provider's default depth, which is high.
            "high" or "adaptive" or "on" or "true" => ThinkingMode.High,
            "xhigh" or "extrahigh" => ThinkingMode.ExtraHigh,
            _ => throw new InvalidOperationException(
                $"Unknown llm.thinking value '{value}'. Valid values: provider-default, disabled, low, medium, " +
                "high, xhigh (aliases: default, off, adaptive, on)."),
        };
    }

    /// <summary>The mode as the configuration spells it, the name <see cref="Parse"/> reads back.</summary>
    public static string Name(ThinkingMode mode) => mode switch
    {
        ThinkingMode.ProviderDefault => "provider-default",
        ThinkingMode.Disabled => "disabled",
        ThinkingMode.Low => "low",
        ThinkingMode.Medium => "medium",
        ThinkingMode.High => "high",
        ThinkingMode.ExtraHigh => "xhigh",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
