using System.Text.Json;
using System.Text.Json.Serialization;
using Chartula.Core.Facts;
using Chartula.Core.History;
using Chartula.Core.Observability;
using Chartula.Core.Pipeline;

namespace Chartula.Core.Serialization;

/// <summary>
/// Serializes a <see cref="RunRecord"/> to its documented format.
/// Pure: the run's time and provenance are passed in, so the same run always
/// serializes the same way. Source-generated, like <see cref="ChangelogJsonSerializer"/>.
/// </summary>
public static class RunRecordJsonSerializer
{
    /// <summary>
    /// The current on-disk schema version.
    /// Version 2 turned each flag from a string into an object that names its pull request,
    /// and added the range the run read.
    /// Version 3 renamed the range start <c>whole-history</c> to <c>first-commit</c>, since
    /// the range ends at the tag and never was the whole history.
    /// </summary>
    public const int SchemaVersion = 3;

    public static string Serialize(RunRecord record, DateTimeOffset recordedAt, RunProvenance? provenance = null)
    {
        ArgumentNullException.ThrowIfNull(record);

        RunReport metrics = record.Metrics;
        RunRecordDocument document = new(
            SchemaVersion,
            // Whole seconds in UTC: the time only orders runs, and fractions add noise.
            DateTimeOffset.FromUnixTimeSeconds(recordedAt.ToUnixTimeSeconds()),
            record.Tag,
            $"{record.Repository.Owner}/{record.Repository.Name}",
            record.Range is { } range
                ? new RunRecordRange(StartName(range, record.Since), range.From, range.FromCommit, range.ToCommit)
                : null,
            ModeName(record.Mode),
            ChangelogJsonSerializer.ToDocument(provenance),
            [.. record.Audiences.Select(static audience => new RunRecordAudience(
                audience.Audience.ToString().ToLowerInvariant(),
                audience.Success,
                audience.Success
                    ? [.. audience.Flags.Select(static flag => new RunRecordFlag(flag.Text, flag.PullRequest))]
                    : null,
                audience.Success ? null : audience.Error))],
            new RunRecordMetrics(
                Usage(metrics.UsageOf(LlmOperation.Rephrase)),
                Usage(metrics.UsageOf(LlmOperation.FaithfulnessCheck)),
                new RunRecordCheck(metrics.RuleBased.Runs, metrics.RuleBased.RunsWithFindings, metrics.RuleBased.Flags),
                new RunRecordThoroughCheck(
                    metrics.Thorough.Runs,
                    metrics.Thorough.RunsWithFindings,
                    metrics.Thorough.Flags,
                    metrics.ThoroughOnlyFlags,
                    metrics.ThoroughNotEvaluated),
                metrics.Duration is { } duration ? Seconds(duration) : null,
                metrics.Scope is { } scope
                    ? new RunRecordRelease(
                        scope.Commits,
                        scope.PullRequests,
                        scope.Facts,
                        scope.FactsWithDescription,
                        scope.DescriptionCharacters,
                        scope.CommitsWithoutPullRequest,
                        scope.MergeCommitsSkipped)
                    : null),
            record.Facts is { } facts ? [.. facts.Changes.Select(FactEntry.From)] : null);

        return JsonSerializer.Serialize(document, RunRecordJsonContext.Default.RunRecordDocument);
    }

    /// <summary>
    /// The tag and facts of <paramref name="factBase"/> in the run record's fields: the
    /// format of a test fixture.
    /// </summary>
    public static string SerializeFacts(FactBase factBase)
    {
        ArgumentNullException.ThrowIfNull(factBase);
        return JsonSerializer.Serialize(
            new RunRecordFacts(factBase.Tag, [.. factBase.Changes.Select(FactEntry.From)]),
            RunRecordJsonContext.Default.RunRecordFacts);
    }

    /// <summary>
    /// The fact base a run rendered from, read from its record - or from anything with the
    /// record's <c>tag</c> and <c>facts</c>, such as a test fixture. The other fields are not read.
    /// </summary>
    public static FactBase DeserializeFactBase(string json)
    {
        RunRecordFacts read = JsonSerializer.Deserialize(json, RunRecordJsonContext.Default.RunRecordFacts)
                              ?? throw new InvalidOperationException("The run record deserialized to null.");
        if (read.Facts is null)
        {
            throw new InvalidOperationException(
                $"The run record of {read.Tag} holds no facts: it was written before run records kept them.");
        }

        return new FactBase(read.Tag, [.. read.Facts.Select(static entry => entry.ToFact())]);
    }

    /// <summary>Reads a record back, for tests and for comparing runs.</summary>
    public static RunRecordDocument Deserialize(string json)
        => JsonSerializer.Deserialize(json, RunRecordJsonContext.Default.RunRecordDocument)
           ?? throw new InvalidOperationException("The run record deserialized to null.");

    // Use the CLI's own words, so the record names the command that produced it.
    private static string ModeName(PipelineMode mode) => mode switch
    {
        PipelineMode.Preview => "preview",
        PipelineMode.Generate => "generate",
        PipelineMode.GenerateWithoutPublishing => "generate --no-publish",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown pipeline mode."),
    };

    // Named after the CLI's options. A named start is where the range begins whatever the
    // tags say; without one, it begins after the previous tag, and without that, at the first commit.
    private static string StartName(CommitRange range, string? since)
        => since is not null ? "since"
            : range.StartsAtFirstCommit ? "first-commit"
            : "previous-tag";

    private static RunRecordLlmUsage Usage(LlmUsage usage)
        => new(
            usage.TotalCalls,
            usage.CallsWithoutUsage,
            usage.Tokens.InputTokens,
            usage.Tokens.OutputTokens,
            usage.FailedCalls,
            Seconds(usage.Duration),
            Seconds(usage.LongestCall),
            usage.Retries,
            usage.CachedInputTokens,
            usage.ReasoningTokens);

    // Round to milliseconds: finer precision is meaningless for a model call.
    private static double Seconds(TimeSpan duration) => Math.Round(duration.TotalSeconds, 3);
}

/// <summary>Source-generated (reflection-free) context for the run record format.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(RunRecordDocument))]
[JsonSerializable(typeof(RunRecordFacts))]
internal sealed partial class RunRecordJsonContext : JsonSerializerContext;
