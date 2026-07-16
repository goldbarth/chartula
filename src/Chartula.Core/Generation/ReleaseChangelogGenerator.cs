using System.Text;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Generation;

/// <summary>
/// Default <see cref="IReleaseChangelogGenerator"/>. It turns the fact base into
/// grounded fact statements and makes exactly one <see cref="IChangelogModel"/>
/// call per release. An empty fact base makes no call at all. Provider failures
/// are caught and returned as a failed result; cancellation propagates.
/// </summary>
public sealed class ReleaseChangelogGenerator(IChangelogModel model) : IReleaseChangelogGenerator
{
    private readonly IChangelogModel _model = model ?? throw new ArgumentNullException(nameof(model));

    public async Task<ChangelogGenerationResult> GenerateAsync(
        FactBase factBase,
        Audience audience,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factBase);

        GroundedFacts facts = ToGroundedFacts(factBase);

        // Nothing to generate - skip the call entirely (keeps calls minimal).
        if (facts.Statements.Count == 0)
        {
            return ChangelogGenerationResult.Success(string.Empty);
        }

        try
        {
            string text = await _model.RephraseAsync(new RephraseRequest(facts, audience), cancellationToken);
            return ChangelogGenerationResult.Success(text);
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

    // One grounded statement per change, from established facts only. The wording
    // here is a plain fact carrier; prompt design is its own issue.
    private static GroundedFacts ToGroundedFacts(FactBase factBase)
    {
        List<string> statements = [];
        foreach (ChangeFact change in factBase.Changes)
        {
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

            statements.Add(statement.ToString());
        }

        return new GroundedFacts(statements);
    }
}
