namespace Chartula.Cli.Configuration;

/// <summary>
/// Checks every configured endpoint where it is read, before any work starts.
/// A key or token travels to the endpoint in a header. Over plain http to another
/// machine, anyone on the network path can read it, and a typo or a downgraded URL is
/// enough for that.
/// http is allowed for loopback only, where the local model servers run.
/// A server elsewhere on the local network also needs https. The stricter rule is also
/// the simpler one, and an exception for private address ranges would leave the same
/// path open.
/// </summary>
internal static class EndpointUrl
{
    /// <summary>
    /// Parses <paramref name="value"/>, or throws naming <paramref name="setting"/>
    /// when it is not an absolute https URL, or http to a loopback host.
    /// </summary>
    public static Uri Require(string setting, string value)
    {
        // Check the scheme, not only the parse. 'localhost:11434' parses as an absolute
        // URI with scheme 'localhost' and path '11434'. A forgotten http:// would pass
        // here and fail much later, in a place that no longer names the setting.
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
