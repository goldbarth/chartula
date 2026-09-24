using Chartula.Core.Serialization;

namespace Chartula.Infrastructure.Serialization;

/// <summary>
/// An <see cref="ICustomerPageWriter"/> that writes one file per release into a
/// directory on disk.
/// The serialisation lives in <see cref="CustomerPageComposer"/>. This adapter only
/// names the file and writes it.
/// </summary>
public sealed class FileCustomerPageWriter(string outputDirectory) : ICustomerPageWriter
{
    /// <summary>The prefix every release page shares, so the files sort together.</summary>
    public const string FileNamePrefix = "release-";

    /// <summary>
    /// The file name for a release. The tag identifies the release, so the tag names
    /// the file.
    /// Characters a file system does not allow become hyphens. For example,
    /// <c>release/1.0</c> is a legal git tag.
    /// </summary>
    public static string FileNameFor(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        // Replace both path separators, not only the platform's, so a tag gets the same
        // file name on every machine.
        char[] invalid = [.. Path.GetInvalidFileNameChars(), '/', '\\'];
        string safe = new([.. tag.Trim().Select(c => Array.IndexOf(invalid, c) >= 0 ? '-' : c)]);
        return FileNamePrefix + safe + ".md";
    }

    public async Task<string> WriteAsync(CustomerPage page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        Directory.CreateDirectory(outputDirectory);
        string path = Path.Combine(outputDirectory, FileNameFor(page.Tag));

        await File.WriteAllTextAsync(path, CustomerPageComposer.Compose(page), cancellationToken);
        return path;
    }
}
