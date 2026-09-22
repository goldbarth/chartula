using System.Text.RegularExpressions;
using Chartula.Cli.Configuration;

namespace Chartula.Cli.Composition;

/// <summary>
/// The thinking modes a Claude model is known to reject. The request itself is
/// provider-neutral (<see cref="Microsoft.Extensions.AI.ReasoningOptions"/>); this is
/// only the part of the translation that can be checked before the first call, so a
/// run that was never going to work fails before it fetches anything.
/// </summary>
/// <remarks>
/// No other provider has a check here: OpenAI-compatible endpoints serve models
/// whose ids say nothing reliable about what they accept, so their refusal comes
/// from the endpoint, on the first call.
/// </remarks>
internal static partial class ClaudeThinkingSupport
{
    /// <summary>
    /// Refuses the combinations the API is known to reject. A model id this cannot
    /// read passes through: gateways and new releases name models in ways no list here
    /// anticipates, and refusing those would block runs that work. The API still
    /// rejects a bad combination there, just later.
    /// </summary>
    /// <exception cref="InvalidOperationException">The model is known to reject the mode.</exception>
    /// <param name="mode">The thinking mode asked for.</param>
    /// <param name="model">The model id it is asked of.</param>
    /// <param name="thinkingKey">The setting the mode came from, named in the refusal.</param>
    /// <param name="modelKey">The setting the model came from, named in the refusal.</param>
    public static void EnsureModelAccepts(
        ThinkingMode mode, string model, string thinkingKey = "llm.thinking", string modelKey = "llm.model")
    {
        Match match = ModelIdRegex().Match(model);
        if (!match.Success)
        {
            return;
        }

        string family = match.Groups["family"].Value.ToLowerInvariant();
        int major = int.Parse(match.Groups["major"].Value);
        int minor = match.Groups["minor"].Success ? int.Parse(match.Groups["minor"].Value) : 0;
        (int, int) version = (major, minor);
        string name = ThinkingModeParser.Name(mode);

        // Every effort level is sent as adaptive thinking with that effort, and
        // adaptive thinking arrived with Claude 4.6.
        if (mode is ThinkingMode.Low or ThinkingMode.Medium or ThinkingMode.High or ThinkingMode.ExtraHigh
            && version.CompareTo((4, 6)) < 0)
        {
            throw new InvalidOperationException(
                $"{thinkingKey} '{name}' is not supported by {modelKey} '{model}'. A thinking effort " +
                "needs Claude 4.6 or newer; set {thinkingKey} to disabled or provider-default, or pick a newer model.");
        }

        if (mode == ThinkingMode.ExtraHigh && version == (4, 6))
        {
            throw new InvalidOperationException(
                $"{thinkingKey} 'xhigh' is not supported by {modelKey} '{model}'. The xhigh effort arrived " +
                "with Claude Opus 4.7; set {thinkingKey} to high, or pick a newer model.");
        }

        if (mode == ThinkingMode.Disabled && family == "fable")
        {
            throw new InvalidOperationException(
                $"{thinkingKey} 'disabled' is not supported by {modelKey} '{model}'. Claude Fable always " +
                "thinks and rejects an explicit off; set {thinkingKey} to provider-default.");
        }
    }

    // Both id shapes Anthropic has used: claude-haiku-4-5 (family first) and
    // claude-3-5-haiku (version first). The minor version is one or two digits so a
    // date suffix (claude-opus-4-20250514) is not read as one. Unanchored, because
    // gateways prefix the id (us.anthropic.claude-...).
    [GeneratedRegex(
        @"claude-(?:(?<family>[a-z]+)-(?<major>\d+)(?:-(?<minor>\d{1,2})(?!\d))?|(?<major>\d+)(?:-(?<minor>\d{1,2})(?!\d))?-(?<family>[a-z]+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ModelIdRegex();
}
