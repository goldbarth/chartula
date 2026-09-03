using System.Text;
using Chartula.Core.Observability;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;

namespace Chartula.Cli.Commands;

/// <summary>
/// Runs the release pipeline for the <c>generate</c> and <c>preview</c> commands
/// and prints a clear summary. Preview shows what would be produced and writes
/// nothing; generate writes the outputs and reports where they went, and names
/// what it left out when a run was told not to publish.
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
            output.Write(Format(outcome));
            return 0;
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
        string verb = outcome.Mode == PipelineMode.Preview ? "Preview" : "Generated";
        builder.AppendLine($"{verb} changelog for {outcome.Tag}");
        builder.AppendLine();

        foreach (AudienceOutcome audience in outcome.Renderings)
        {
            builder.AppendLine($"--- {audience.Audience} ---");
            if (!audience.Success)
            {
                builder.AppendLine($"  (failed) {audience.Error}");
            }
            else
            {
                builder.AppendLine(audience.Text);
                if (audience.Flags.Count > 0)
                {
                    builder.AppendLine("  Flagged for review:");
                    foreach (string flag in audience.Flags)
                    {
                        builder.AppendLine($"    ! {flag}");
                    }
                }
            }

            builder.AppendLine();
        }

        if (outcome.Mode == PipelineMode.Preview)
        {
            builder.AppendLine("Preview only - nothing was written or published.");
        }
        else
        {
            AppendOutputs(builder, outcome);
        }

        builder.AppendLine();
        builder.Append(RunReportFormatter.Format(outcome.Metrics));

        return builder.ToString();
    }

    /// <summary>
    /// Lists what the run produced and, below it, what it deliberately did not, so
    /// a skipped publication is as visible as a written file.
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
