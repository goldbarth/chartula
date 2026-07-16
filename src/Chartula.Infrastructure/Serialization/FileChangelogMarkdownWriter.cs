using Chartula.Core.Serialization;

namespace Chartula.Infrastructure.Serialization;

/// <summary>
/// An <see cref="IChangelogMarkdownWriter"/> that reads and rewrites
/// <c>CHANGELOG.md</c> in a directory on disk. The prepend/preserve/idempotency
/// logic lives in <see cref="ChangelogMarkdownComposer"/>; this adapter only reads
/// the existing file and writes the composed result.
/// </summary>
public sealed class FileChangelogMarkdownWriter(string outputDirectory) : IChangelogMarkdownWriter
{
    /// <summary>The fixed file name written into the output directory.</summary>
    public const string FileName = "CHANGELOG.md";

    public async Task<string> WriteAsync(string tag, string body, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        string path = Path.Combine(outputDirectory, FileName);

        string? existing = File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken)
            : null;

        string composed = ChangelogMarkdownComposer.Compose(existing, tag, body);
        await File.WriteAllTextAsync(path, composed, cancellationToken);
        return path;
    }
}
