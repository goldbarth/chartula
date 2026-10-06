using System.Text.Encodings.Web;
using System.Text.Json;

namespace Chartula.Core.Serialization;

/// <summary>
/// The options both written files share: <c>changelog.json</c> and the run record are
/// read, committed and diffed by people, so they escape only what JSON requires (#226).
/// The default encoder also escapes backticks, quotes, <c>+</c> and every character
/// outside ASCII, which turned each code span and every non-English title into
/// <c>\u</c> sequences.
/// What the default encoder adds is safety for the raw file pasted into an HTML
/// <c>script</c> element, not for a consumer that parses it: a parser reads the same
/// strings either way, and those strings come from repository content, so a consumer
/// escapes them on output in any case.
/// </summary>
internal static class ReadableJson
{
    // A new instance per context: options are bound to the first context that uses them.
    public static JsonSerializerOptions Options() => new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
