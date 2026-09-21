using System.Text;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Faithfulness;
using Chartula.Core.Formatting;
using Chartula.Core.Generation;
using Chartula.Core.Labeling;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.Prompting;
using Chartula.Core.Rendering;
using Microsoft.Extensions.AI;

namespace Chartula.Core.Budget;

/// <summary>
/// Estimates the most a run can consume before it makes a call, from the prompts
/// it is about to send - built by the same builder, from the same plan, so the
/// estimate cannot describe a different run.
/// <list type="bullet">
/// <item>Input: the prompt's UTF-8 bytes. A tokenizer that falls back to bytes
/// never produces more tokens than there are bytes, so this bounds the count from
/// above, loosely - real prose runs about four characters to a token. Added to it:
/// the response schema, and <see cref="PerCallAllowance"/> for what the provider
/// adds around a request.</item>
/// <item>Output: <see cref="ChatModelOptions.MaxOutputTokens"/>, the ceiling the
/// provider enforces, thinking included. It is almost always far above what a call
/// uses; it is the only number that is a bound.</item>
/// <item>A thorough check reads back the rendering: at most the model's output of
/// the call before it, plus the structure code puts around the entries.</item>
/// </list>
/// </summary>
public sealed class RunEstimator(
    IChangelogPromptBuilder promptBuilder,
    IChangelogFormatter formatter,
    ChatModelOptions chatOptions,
    ThoroughFaithfulnessOptions thorough,
    CategorySettings? categorySettings = null,
    LabelRules? labelRules = null)
{
    /// <summary>
    /// Tokens a provider adds to a call that are not in the prompt: message framing
    /// and the instructions that come with structured output.
    /// </summary>
    public const int PerCallAllowance = 1_000;

    private static readonly long RephraseSchemaBytes = SchemaBytes<RenderedEntries>();
    private static readonly long VerdictSchemaBytes = SchemaBytes<FaithfulnessVerdict>();

    private readonly CategorySettings _categorySettings = categorySettings ?? CategorySettings.Default;
    private readonly LabelRules _labelRules = labelRules ?? LabelRules.None;

    public RunEstimate Estimate(FactBase factBase, IReadOnlyCollection<Audience>? audiences)
    {
        ArgumentNullException.ThrowIfNull(factBase);

        long maxOutput = chatOptions.MaxOutputTokens;
        GroundedFacts checkedAgainst = ThoroughFaithfulnessChecker.ToGroundedFacts(factBase);
        List<CallEstimate> calls = [];
        foreach (Audience audience in ReleaseRenderer.AllAudiences.Where(a => audiences?.Contains(a) ?? true))
        {
            RenderPlan plan = GroundedFactsFactory.Build(
                factBase, audience, _categorySettings, _labelRules.ActionRequiredLabels);

            // The generator makes no call for an empty plan, so neither does this.
            if (plan.Entries.Count == 0)
            {
                continue;
            }

            ChangelogPrompt rephrase = promptBuilder.BuildRephrasePrompt(plan.Facts, audience);
            calls.Add(new CallEstimate(
                LlmOperation.Rephrase, audience, Bytes(rephrase) + RephraseSchemaBytes + PerCallAllowance, maxOutput));

            if (thorough.Enabled)
            {
                // The rendering the check reads is the model's words inside code's
                // structure. A one-character entry stands in for the words, which are
                // bounded by the rephrase call's output ceiling.
                RenderedEntries placeholder = new([.. plan.Entries.Select(entry => new RenderedEntry(entry.Id, "x"))]);
                string structure = formatter.Format(RenderingComposer.Compose(plan, placeholder, audience));
                ChangelogPrompt check = promptBuilder.BuildFaithfulnessPrompt(structure, checkedAgainst);
                calls.Add(new CallEstimate(
                    LlmOperation.FaithfulnessCheck,
                    audience,
                    Bytes(check) + maxOutput + VerdictSchemaBytes + PerCallAllowance,
                    maxOutput));
            }
        }

        return new RunEstimate(calls);
    }

    private static long Bytes(ChangelogPrompt prompt)
        => Encoding.UTF8.GetByteCount(prompt.System) + Encoding.UTF8.GetByteCount(prompt.User);

    private static long SchemaBytes<T>()
        => Encoding.UTF8.GetByteCount(AIJsonUtilities.CreateJsonSchema(typeof(T)).GetRawText());
}
