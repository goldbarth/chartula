namespace Chartula.Core.Observability;

/// <summary>
/// Keeps a <see cref="RunRecord"/> on the machine the run was made on.
/// It is never published: usage is a fact about the run, not about the release, so it
/// does not belong in <c>changelog.json</c> or the release notes.
/// </summary>
public interface IRunRecordWriter
{
    /// <summary>Writes the record and returns where it went.</summary>
    Task<string> WriteAsync(RunRecord record, CancellationToken cancellationToken = default);
}
