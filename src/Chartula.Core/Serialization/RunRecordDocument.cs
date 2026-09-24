using System.Text.Json.Serialization;

namespace Chartula.Core.Serialization;

/// <summary>
/// The on-disk shape of a run record.
/// It is separate from the domain <c>RunRecord</c>, like <see cref="ChangelogDocument"/>:
/// runs are compared across Chartula versions, so the file format changes only on purpose.
/// Documented in <c>docs/run-record.md</c>.
/// </summary>
public sealed record RunRecordDocument(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("recordedAt")] DateTimeOffset RecordedAt,
    [property: JsonPropertyName("tag")] string Tag,
    [property: JsonPropertyName("repository")] string Repository,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("provenance"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ChangelogProvenance? Provenance,
    [property: JsonPropertyName("audiences")] IReadOnlyList<RunRecordAudience> Audiences,
    [property: JsonPropertyName("metrics")] RunRecordMetrics Metrics);

/// <summary>
/// One audience of the run.
/// A rendered audience has flags and no error. A failed audience has an error and no flags.
/// A failed audience was never checked, so its flags are absent. An empty list would
/// look like a clean check.
/// </summary>
public sealed record RunRecordAudience(
    [property: JsonPropertyName("audience")] string Audience,
    [property: JsonPropertyName("rendered")] bool Rendered,
    [property: JsonPropertyName("flags"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Flags,
    [property: JsonPropertyName("error"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Error);

/// <summary>
/// The run metrics as the summary prints them.
/// Both operations are always present: an operation without calls cost nothing, and
/// zero is a real value.
/// </summary>
public sealed record RunRecordMetrics(
    [property: JsonPropertyName("rephrase")] RunRecordLlmUsage Rephrase,
    [property: JsonPropertyName("faithfulnessCheck")] RunRecordLlmUsage FaithfulnessCheck,
    [property: JsonPropertyName("ruleBasedCheck")] RunRecordCheck RuleBasedCheck,
    [property: JsonPropertyName("thoroughCheck")] RunRecordThoroughCheck ThoroughCheck,
    [property: JsonPropertyName("durationSeconds"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? DurationSeconds = null,
    [property: JsonPropertyName("release"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    RunRecordRelease? Release = null);

/// <summary>The size of the release the run worked on: the context for its token counts.</summary>
public sealed record RunRecordRelease(
    [property: JsonPropertyName("commits")] int Commits,
    [property: JsonPropertyName("pullRequests")] int PullRequests,
    [property: JsonPropertyName("facts")] int Facts,
    [property: JsonPropertyName("factsWithDescription")] int FactsWithDescription,
    [property: JsonPropertyName("descriptionCharacters")] long DescriptionCharacters);

/// <summary>
/// Calls, tokens and time of one LLM operation.
/// The fields after the tokens were added under schema version 1. A record written
/// before them reads back with zeros and no retry count.
/// </summary>
public sealed record RunRecordLlmUsage(
    [property: JsonPropertyName("calls")] int Calls,
    [property: JsonPropertyName("callsWithoutUsage")] int CallsWithoutUsage,
    [property: JsonPropertyName("inputTokens")] long InputTokens,
    [property: JsonPropertyName("outputTokens")] long OutputTokens,
    [property: JsonPropertyName("failedCalls")] int FailedCalls = 0,
    [property: JsonPropertyName("durationSeconds")] double DurationSeconds = 0,
    [property: JsonPropertyName("longestCallSeconds")] double LongestCallSeconds = 0,
    [property: JsonPropertyName("retries"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Retries = null,
    [property: JsonPropertyName("cachedInputTokens"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    long? CachedInputTokens = null,
    [property: JsonPropertyName("reasoningTokens"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    long? ReasoningTokens = null);

/// <summary>How often the rule-based check ran and what it found.</summary>
public sealed record RunRecordCheck(
    [property: JsonPropertyName("runs")] int Runs,
    [property: JsonPropertyName("runsWithFindings")] int RunsWithFindings,
    [property: JsonPropertyName("flags")] int Flags);

/// <summary>
/// How often the thorough check ran and what it found.
/// Two numbers show whether it was worth its tokens: what only it caught, and how
/// often it came back unreadable.
/// </summary>
public sealed record RunRecordThoroughCheck(
    [property: JsonPropertyName("runs")] int Runs,
    [property: JsonPropertyName("runsWithFindings")] int RunsWithFindings,
    [property: JsonPropertyName("flags")] int Flags,
    [property: JsonPropertyName("onlyThoroughFlags")] int OnlyThoroughFlags,
    [property: JsonPropertyName("notEvaluated")] int NotEvaluated);
