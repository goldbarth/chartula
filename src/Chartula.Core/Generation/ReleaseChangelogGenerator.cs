using System.Text;
using Chartula.Core.Facts;
using Chartula.Core.Formatting;
using Chartula.Core.Llm;

namespace Chartula.Core.Generation;

/// <summary>
/// Default <see cref="IReleaseChangelogGenerator"/>. It turns the fact base into
/// grounded fact statements and makes exactly one <see cref="IChangelogModel"/>
/// call per release, then normalizes the output for consistent formatting. An
/// empty fact base makes no call at all. Provider failures are caught and
/// returned as a failed result; cancellation propagates.
/// </summary>
public sealed class ReleaseChangelogGenerator(IChangelogModel model, IChangelogFormatter formatter)
    : IReleaseChangelogGenerator
{
    private readonly IChangelogModel _model = model ?? throw new ArgumentNullException(nameof(model));
    private readonly IChangelogFormatter _formatter =
        formatter ?? throw new ArgumentNullException(nameof(formatter));

    public async Task<ChangelogGenerationResult> GenerateAsync(
        FactBase factBase,
        Audience audience,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factBase);

        GroundedFacts facts = ToGroundedFacts(factBase, audience);

        // Nothing to generate - skip the call entirely (keeps calls minimal).
        if (facts.Statements.Count == 0)
        {
            return ChangelogGenerationResult.Success(string.Empty);
        }

        try
        {
            string text = await _model.RephraseAsync(new RephraseRequest(facts, audience), cancellationToken);
            return ChangelogGenerationResult.Success(_formatter.Format(text));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ChangelogGenerationResult.Failure(
                $"Changelog generation for '{factBase.Tag}' failed: {ex.Message}");
        }
    }

    // One grounded statement per change, from established facts only, selected and
    // detailed for the audience: the customer view omits non-user-visible changes,
    // and the technical view keeps the pull request link. The same fact base feeds
    // every audience, so the renderings cannot contradict each other.
    private static GroundedFacts ToGroundedFacts(FactBase factBase, Audience audience)
    {
        List<string> statements = [];
        foreach (ChangeFact change in factBase.Changes)
        {
            // Customer entries are user-facing only.
            if (audience == Audience.Customer && !change.IsUserVisible)
            {
                continue;
            }

            StringBuilder statement = new();
            statement.Append(change.Category);
            if (change.IsBreaking)
            {
                statement.Append(" (breaking)");
            }

            statement.Append(": ").Append(change.Title);
            if (!string.IsNullOrEmpty(change.Description))
            {
                statement.Append(" - ").Append(change.Description);
            }

            // Technical entries keep the link.
            if (audience == Audience.Technical && !string.IsNullOrEmpty(change.Url))
            {
                statement.Append(" (").Append(change.Url).Append(')');
            }

            statements.Add(statement.ToString());
        }

        return new GroundedFacts(statements);
    }
}
