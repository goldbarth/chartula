namespace Chartula.Core.Serialization;

/// <summary>
/// Writes the customer rendering as a page of its own, in the published
/// serialisation - one file per release, front matter and body. The pipeline
/// depends only on this port, not on where or how the file is written.
/// <para>
/// Writing a page is not publishing one: it produces a file a person can publish
/// and touches nothing outside the output directory, which is why it happens
/// under <c>--no-publish</c> alongside the other local outputs.
/// </para>
/// </summary>
public interface ICustomerPageWriter
{
    /// <summary>Writes <paramref name="page"/> and returns the path.</summary>
    Task<string> WriteAsync(CustomerPage page, CancellationToken cancellationToken = default);
}
