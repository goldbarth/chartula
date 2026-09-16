using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Formatting;
using Chartula.Core.Labeling;
using Chartula.Core.Llm;

namespace Chartula.Core.Generation;

/// <summary>
/// Default <see cref="IReleaseChangelogGenerator"/>. It plans the rendering from the
/// fact base - which changes, in which order, under which heading - makes exactly
/// one <see cref="IChangelogModel"/> call per release for the words of the entries,
/// and puts the two together. An empty plan makes no call at all. Provider failures
/// and answers that do not match the plan are returned as a failed result;
/// cancellation propagates.
/// <para>
/// The customer rendering carries a one-sentence description of the release, asked
/// for and returned in that same call as a field of its own.
/// </para>
/// </summary>
public sealed class ReleaseChangelogGenerator(
    IChangelogModel model,
    IChangelogFormatter formatter,
    CategorySettings? categorySettings = null,
    LabelRules? labelRules = null) : IReleaseChangelogGenerator
{
    private readonly IChangelogModel _model = model ?? throw new ArgumentNullException(nameof(model));
    private readonly IChangelogFormatter _formatter =
        formatter ?? throw new ArgumentNullException(nameof(formatter));
    private readonly CategorySettings _categorySettings = categorySettings ?? CategorySettings.Default;
    private readonly LabelRules _labelRules = labelRules ?? LabelRules.None;

    public async Task<ChangelogGenerationResult> GenerateAsync(
        FactBase factBase,
        Audience audience,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factBase);

        RenderPlan plan = GroundedFactsFactory.Build(
            factBase, audience, _categorySettings, _labelRules.ActionRequiredLabels);

        // Nothing to generate - skip the call entirely (keeps calls minimal).
        if (plan.Entries.Count == 0)
        {
            return ChangelogGenerationResult.Success(string.Empty);
        }

        try
        {
            RenderedEntries rendered = await _model.RephraseAsync(
                new RephraseRequest(plan.Facts, audience), cancellationToken);

            if (RenderingComposer.FindMismatch(plan, rendered) is { } mismatch)
            {
                return ChangelogGenerationResult.Failure(
                    $"Changelog generation for '{factBase.Tag}' failed: {mismatch}.");
            }

            string text = _formatter.Format(RenderingComposer.Compose(plan, rendered, audience));

            // Only the customer page has a description, so only that rendering is
            // asked for one and only that one is read for it.
            string? description = audience == Audience.Customer
                ? RenderingComposer.SingleLine(rendered.Description)
                : null;

            return ChangelogGenerationResult.Success(text, description);
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
}
