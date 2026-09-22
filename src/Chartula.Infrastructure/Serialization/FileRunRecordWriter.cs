using System.Globalization;
using System.Text;
using Chartula.Core.Observability;
using Chartula.Core.Serialization;

namespace Chartula.Infrastructure.Serialization;

/// <summary>
/// An <see cref="IRunRecordWriter"/> that keeps one file per run in a directory of
/// its own. One file per run rather than one appended file: each record is a
/// whole, indented JSON document that can be read and diffed on its own, and a
/// run never rewrites what an earlier one recorded. The directory keeps them out
/// of the release's files and is one line in a <c>.gitignore</c>.
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

        // The time first, so the files list in the order the runs were made. Two
        // runs in the same second get a suffix rather than one replacing the other.
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

    // A tag may carry a slash (release/1.2) or a character a file system refuses.
    private static string FileNamePart(string tag)
        => string.Concat(tag.Select(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '-'));
}
