using Chartula.Core.Serialization;

namespace Chartula.Infrastructure.Serialization;

/// <summary>
/// An <see cref="ICustomerPageWriter"/> that writes one file per release into a
/// directory on disk. The serialisation lives in
/// <see cref="CustomerPageComposer"/>; this adapter only names the file and
/// writes it.
/// </summary>
public sealed class FileCustomerPageWriter(string outputDirectory) : ICustomerPageWriter
{
    /// <summary>The prefix every release page shares, so the files sort together.</summary>
    public const string FileNamePrefix = "release-";

    /// <summary>
    /// The file a release is written to. The tag identifies the release, so it
    /// names the file; characters a file system cannot carry - a tag like
    /// <c>release/1.0</c> is legal in git - become hyphens.
    /// </summary>
    public static string FileNameFor(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        // Both separators, not only the platform's: a tag written on one machine
        // must land in the same file name on another.
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
