using System.Text.Json;
using System.Text.Json.Serialization;
using Chartula.Core.Observability;
using Chartula.Core.Pipeline;

namespace Chartula.Core.Serialization;

/// <summary>
/// Serializes a <see cref="RunRecord"/> to its documented format. Pure: the time
/// of the run and its provenance are passed in, so the same run always reads the
/// same. Source-generated, like <see cref="ChangelogJsonSerializer"/>.
/// </summary>
public static class RunRecordJsonSerializer
{
    /// <summary>The current on-disk schema version.</summary>
    public const int SchemaVersion = 1;

    public static string Serialize(RunRecord record, DateTimeOffset recordedAt, RunProvenance? provenance = null)
    {
        ArgumentNullException.ThrowIfNull(record);

        RunReport metrics = record.Metrics;
        RunRecordDocument document = new(
            SchemaVersion,
            // Whole seconds in UTC: the time orders runs, the fraction only adds noise.
            DateTimeOffset.FromUnixTimeSeconds(recordedAt.ToUnixTimeSeconds()),
            record.Tag,
            $"{record.Repository.Owner}/{record.Repository.Name}",
            ModeName(record.Mode),
            ChangelogJsonSerializer.ToDocument(provenance),
            [.. record.Audiences.Select(static audience => new RunRecordAudience(
                audience.Audience.ToString().ToLowerInvariant(),
                audience.Success,
                audience.Success ? audience.Flags : null,
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
                metrics.Duration is { } duration ? Seconds(duration) : null));

        return JsonSerializer.Serialize(document, RunRecordJsonContext.Default.RunRecordDocument);
    }

    /// <summary>Reads a record back, for tests and for comparing runs.</summary>
    public static RunRecordDocument Deserialize(string json)
        => JsonSerializer.Deserialize(json, RunRecordJsonContext.Default.RunRecordDocument)
           ?? throw new InvalidOperationException("The run record deserialized to null.");

    // The command line's own words, so a record reads as the command that made it.
    private static string ModeName(PipelineMode mode) => mode switch
    {
        PipelineMode.Preview => "preview",
        PipelineMode.Generate => "generate",
        PipelineMode.GenerateWithoutPublishing => "generate --no-publish",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown pipeline mode."),
    };

    private static RunRecordLlmUsage Usage(LlmUsage usage)
        => new(
            usage.TotalCalls,
            usage.CallsWithoutUsage,
            usage.Tokens.InputTokens,
            usage.Tokens.OutputTokens,
            usage.FailedCalls,
            Seconds(usage.Duration),
            Seconds(usage.LongestCall),
            usage.Retries);

    // Milliseconds are the finest a model call is worth measuring in.
    private static double Seconds(TimeSpan duration) => Math.Round(duration.TotalSeconds, 3);
}

/// <summary>Source-generated (reflection-free) context for the run record format.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(RunRecordDocument))]
internal sealed partial class RunRecordJsonContext : JsonSerializerContext;
