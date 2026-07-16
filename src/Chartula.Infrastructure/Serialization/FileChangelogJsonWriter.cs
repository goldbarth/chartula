using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Serialization;

namespace Chartula.Infrastructure.Serialization;

/// <summary>
/// An <see cref="IChangelogJsonWriter"/> that writes <c>changelog.json</c> to a
/// directory on disk. The serialization format lives in
/// <see cref="ChangelogJsonSerializer"/>; this adapter only handles the file I/O.
/// It writes a single file - the audience texts go inside it, never as separate
/// marketing files.
/// </summary>
public sealed class FileChangelogJsonWriter(string outputDirectory) : IChangelogJsonWriter
{
    /// <summary>The fixed file name written into the output directory.</summary>
    public const string FileName = "changelog.json";

    public async Task<string> WriteAsync(
        FactBase factBase,
        IReadOnlyDictionary<Audience, string>? renderings = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factBase);

        Directory.CreateDirectory(outputDirectory);
        string path = Path.Combine(outputDirectory, FileName);
        string json = ChangelogJsonSerializer.Serialize(factBase, renderings);

        await File.WriteAllTextAsync(path, json, cancellationToken);
        return path;
    }
}
