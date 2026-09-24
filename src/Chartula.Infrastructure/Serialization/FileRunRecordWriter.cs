using System.Globalization;
using System.Text;
using Chartula.Core.Observability;
using Chartula.Core.Serialization;

namespace Chartula.Infrastructure.Serialization;

/// <summary>
/// An <see cref="IRunRecordWriter"/> that keeps one file per run in its own directory.
/// One file per run instead of one appended file:
/// <list type="bullet">
/// <item>each record is a complete, indented JSON document that can be read and
/// diffed on its own,</item>
/// <item>a run never rewrites what an earlier run recorded.</item>
/// </list>
/// The directory keeps the records apart from the release's files and takes one
/// line in a <c>.gitignore</c>.
/// </summary>
public sealed class FileRunRecordWriter(
    string outputDirectory,
    RunProvenance? provenance = null,
    TimeProvider? timeProvider = null) : IRunRecordWriter
{
    /// <summary>The directory the records are written to, inside the output directory.</summary>
    public const string DirectoryName = "chartula-runs";

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public async Task<string> WriteAsync(RunRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        DateTimeOffset now = _time.GetUtcNow();
        string directory = Path.Combine(outputDirectory, DirectoryName);
        Directory.CreateDirectory(directory);

        byte[] json = Encoding.UTF8.GetBytes(RunRecordJsonSerializer.Serialize(record, now, provenance));

        // Start with the time, so the files sort in run order. A second run in the same
        // second gets a suffix instead of replacing the first file.
        string stem = now.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)
                      + "-" + FileNamePart(record.Tag);
        for (int attempt = 1; ; attempt++)
        {
            string path = Path.Combine(directory, attempt == 1 ? $"{stem}.json" : $"{stem}-{attempt}.json");
            try
            {
                await using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write);
                await stream.WriteAsync(json, cancellationToken);
                return path;
            }
            catch (IOException) when (File.Exists(path))
            {
            }
        }
    }

    // A tag may contain a slash (release/1.2) or a character a file system does not allow.
    private static string FileNamePart(string tag)
        => string.Concat(tag.Select(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '-'));
}
