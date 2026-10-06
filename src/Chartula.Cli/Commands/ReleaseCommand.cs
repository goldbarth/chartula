using System.Globalization;
using System.Text;
using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;

namespace Chartula.Cli.Commands;

/// <summary>
/// Runs the release pipeline for the <c>generate</c> and <c>preview</c> commands and
/// prints a clear summary.
/// Preview shows the facts generate would render and what it would send, without a
/// model call, and writes nothing.
/// Generate writes the outputs and reports where they went. With <c>--no-publish</c>
/// it also lists what it left out.
/// </summary>
internal static class ReleaseCommand
{
    public static async Task<int> RunAsync(
        IReleasePipeline pipeline,
        PipelineMode mode,
        ReleaseRequest request,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        try
        {
            ReleaseOutcome outcome = await pipeline.RunAsync(request, mode, cancellationToken);
            if (outcome.Preview is { } preview)
            {
                output.Write(FormatPreview(outcome, preview));
                return 0;
            }

            output.Write(Format(outcome));

            // Exit with 1 when a requested audience or the publication failed.
            // Scripts and CI jobs read the exit code, not the output, so a failure must
            // show there. Whatever did render is still written.
            return outcome.Renderings.All(audience => audience.Success) && outcome.PublishFailure is null ? 0 : 1;
        }
        catch (UnconfirmedRangeException ex)
        {
            // Not an error: the operator declined, or nobody could be asked. The range
            // and the way out were shown with the question.
            output.WriteLine($"Stopped: {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            output.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (ArgumentException ex)
        {
            output.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static string Format(ReleaseOutcome outcome)
    {
        StringBuilder builder = new();
        int failed = outcome.Renderings.Count(audience => !audience.Success);
        bool nothingRendered = failed > 0 && failed == outcome.Renderings.Count;
        builder.AppendLine(nothingRendered
            ? $"No changelog generated for {outcome.Tag}: no audience rendered."
            : $"Generated changelog for {outcome.Tag}");
        builder.AppendLine();

        // Print a repeated error only once. Two audiences failing the same way, for
        // example one endpoint refusing one model, are one problem, and printing it
        // twice reads as two.
        Dictionary<string, Audience> firstFailedWith = [];
        foreach (AudienceOutcome audience in outcome.Renderings)
        {
            builder.AppendLine($"--- {audience.Audience} ---");
            if (!audience.Success)
            {
                string error = audience.Error ?? string.Empty;
                if (firstFailedWith.TryGetValue(error, out Audience first))
                {
                    builder.AppendLine($"  (failed) The same as {first}.");
                }
                else
                {
                    firstFailedWith[error] = audience.Audience;
                    AppendIndented(builder, "  (failed) ", error);
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(audience.Description))
                {
                    builder.AppendLine($"  description: {audience.Description}");
                    builder.AppendLine();
                }

                builder.AppendLine(audience.Text);
                // A flag may concern any entry of the rendering, so the flags stand apart
                // from the text instead of reading as a line of its last entry (#211).
                if (audience.Flags.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("Flagged for review:");
                    foreach (FaithfulnessFlag flag in audience.Flags)
                    {
                        AppendIndented(builder, "  ! ", flag.ToString());
                    }
                }
            }

            builder.AppendLine();
        }

        AppendOutputs(builder, outcome);

        if (failed > 0 && !nothingRendered)
        {
            builder.AppendLine($"{failed} of {outcome.Renderings.Count} audiences failed.");
        }

        builder.AppendLine();
        builder.Append(RunReportFormatter.Format(outcome.Metrics));
        if (outcome.RunRecord is { } runRecord)
        {
            builder.AppendLine($"  Recorded in {runRecord}");
        }

        return builder.ToString();
    }

    // Each fact on two lines, the audiences below it, so a long title does not push them
    // out of sight. Audience names are written as --audience takes them.
    private static string FormatPreview(ReleaseOutcome outcome, ReleasePreview preview)
    {
        StringBuilder builder = new();
        builder.AppendLine($"Preview of {outcome.Tag} - no model call was made, and nothing was written or published.");
        builder.AppendLine();
        if (outcome.Metrics.Scope is { } scope)
        {
            builder.AppendLine($"Release: {RunReportFormatter.FormatScope(scope)}");
            builder.AppendLine();
        }

        if (preview.Facts.Count == 0)
        {
            builder.AppendLine("Facts: none");
        }
        else
        {
            builder.AppendLine($"Facts ({Number(preview.Facts.Count)}):");
            foreach (PreviewFact fact in preview.Facts)
            {
                string category = fact.Fact.IsBreaking ? $"{fact.Fact.Category}, breaking" : fact.Fact.Category.ToString();
                builder.AppendLine($"  {Reference(fact.Fact.Number, commitSha: null),-9}{category}: {fact.Fact.Title}");
                builder.AppendLine($"           {(fact.Audiences.Count == 0 ? "in no rendering" : Names(fact.Audiences))}");
            }
        }

        if (preview.Dropped.Count > 0)
        {
            builder.AppendLine($"Dropped ({Number(preview.Dropped.Count)}):");
            foreach (DroppedChange dropped in preview.Dropped)
            {
                builder.AppendLine($"  {Reference(dropped.Change.Number, dropped.Change.CommitSha),-9}{dropped.Change.Title}");
                builder.AppendLine($"           {dropped.Reason}");
            }
        }

        builder.AppendLine();
        builder.AppendLine(preview.ModelCalls == 1
            ? "generate would make 1 model call:"
            : $"generate would make {Number(preview.ModelCalls)} model calls:");
        foreach (AudiencePreview audience in preview.Audiences)
        {
            string name = Name(audience.Audience).PadRight(10);
            if (audience.Entries == 0)
            {
                builder.AppendLine($"  {name}nothing to render, no call");
                continue;
            }

            string check = preview.ThoroughCheck ? ", then 1 thorough check" : string.Empty;
            builder.AppendLine($"  {name}1 rephrasing call, {Number(audience.PromptCharacters)} characters of prompt{check}");
        }

        if (preview.ThoroughCheck && preview.ModelCalls > 0)
        {
            builder.AppendLine("  A thorough check sends the rendering along with the facts, so its size is known only once the rendering is.");
        }

        return builder.ToString();
    }

    // A pull request by its number, a commit by its short hash. A fact keeps no hash,
    // so a fact from a commit shows only that it is one.
    private static string Reference(int? number, string? commitSha)
        => number is { } n ? $"#{n}" : commitSha is { Length: >= 7 } sha ? sha[..7] : "commit";

    private static string Names(IEnumerable<Audience> audiences) => string.Join(", ", audiences.Select(Name));

    private static string Name(Audience audience) => audience.ToString().ToLowerInvariant();

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>
    /// Appends <paramref name="text"/> after <paramref name="prefix"/>, with further lines
    /// indented to align with the first line's text.
    /// A failed model call explains itself over several lines. Without indentation they
    /// would look like separate output.
    /// </summary>
    private static void AppendIndented(StringBuilder builder, string prefix, string text)
    {
        string indent = new(' ', prefix.Length);
        string[] lines = text.Split('\n');
        builder.Append(prefix).AppendLine(lines[0].TrimEnd());
        foreach (string line in lines.Skip(1))
        {
            builder.Append(indent).AppendLine(line.TrimEnd());
        }
    }

    /// <summary>
    /// Lists what the run produced and, below it, what it deliberately skipped, so a
    /// skipped publication is as visible as a written file.
    /// </summary>
    private static void AppendOutputs(StringBuilder builder, ReleaseOutcome outcome)
    {
        if (outcome.WrittenOutputs.Count > 0)
        {
            builder.AppendLine("Wrote:");
            foreach (string written in outcome.WrittenOutputs)
            {
                builder.AppendLine($"  - {written}");
            }
        }
        else
        {
            builder.AppendLine("Nothing to write.");
        }

        if (outcome.PublishFailure is { } failure)
        {
            // The written files stay listed above, so the reader sees the run was not lost.
            // The message also states what a re-run costs, before the reader starts one.
            builder.AppendLine("Not published: the release notes.");
            foreach (string line in failure.Split('\n'))
            {
                builder.AppendLine($"  {line.TrimEnd()}");
            }

            builder.AppendLine("  The files above are written. A re-run replaces this release's entries rather");
            builder.AppendLine("  than adding them, but pays for the model calls again; --no-publish skips this step.");
        }

        if (outcome.SkippedOutputs.Count == 0)
        {
            return;
        }

        string reason = outcome.Mode == PipelineMode.GenerateWithoutPublishing ? " (--no-publish)" : string.Empty;
        builder.AppendLine($"Skipped{reason}:");
        foreach (string skipped in outcome.SkippedOutputs)
        {
            builder.AppendLine($"  - {skipped}");
        }
    }

    /// <summary>Parses an <c>owner/name</c> string into repository coordinates.</summary>
    public static bool TryParseRepository(string? value, out RepositoryCoordinates repository)
    {
        repository = new RepositoryCoordinates(string.Empty, string.Empty);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            return false;
        }

        repository = new RepositoryCoordinates(parts[0], parts[1]);
        return true;
    }
}
