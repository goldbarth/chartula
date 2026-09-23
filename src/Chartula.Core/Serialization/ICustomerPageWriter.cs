namespace Chartula.Core.Serialization;

/// <summary>
/// Writes the customer rendering as its own page in the published serialisation:
/// one file per release, front matter and body.
/// The pipeline depends only on this port, not on where or how the file is written.
/// <para>
/// Writing a page does not publish it. It produces a file a person can publish and
/// touches nothing outside the output directory. That is why the page is also
/// written under <c>--no-publish</c>, like the other local outputs.
/// </para>
/// </summary>
public interface ICustomerPageWriter
{
    /// <summary>Writes <paramref name="page"/> and returns the path.</summary>
    Task<string> WriteAsync(CustomerPage page, CancellationToken cancellationToken = default);
}
