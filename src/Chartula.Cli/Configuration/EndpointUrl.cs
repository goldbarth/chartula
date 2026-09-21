namespace Chartula.Cli.Configuration;

/// <summary>
/// The check every configured endpoint passes where it is read, before any work
/// starts. A key or token travels to it in a header, so plain http to another
/// machine hands it to anyone on the path - a typo or a downgraded URL is enough.
/// http is kept for loopback only, which is where the local model servers live.
/// A server elsewhere on the network needs https too: the stricter rule is also the
/// simpler one, and a private-range exception would leave the same path open.
/// </summary>
internal static class EndpointUrl
{
    /// <summary>
    /// Parses <paramref name="value"/>, or throws naming <paramref name="setting"/>
    /// when it is not an absolute https URL, or http to a loopback host.
    /// </summary>
    public static Uri Require(string setting, string value)
    {
        // The scheme is checked, not just the parse: 'localhost:11434' parses as an
        // absolute URI whose scheme is 'localhost' and whose path is '11434', so a
        // forgotten http:// would otherwise be accepted here and fail much later,
        // somewhere that no longer mentions the setting that caused it.
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException(
                $"Invalid {setting} '{value}'. Expected an absolute https URL, " +
                "or http for a server on this machine such as http://localhost:11434/v1.");
        }

        if (endpoint.Scheme == Uri.UriSchemeHttp && !endpoint.IsLoopback)
        {
            throw new InvalidOperationException(
                $"{setting} '{value}' uses http to another machine, which would send credentials in cleartext. " +
                "Use https; http is accepted only for this machine (localhost, 127.0.0.1, ::1).");
        }

        return endpoint;
    }
}
