using Chartula.Core.Prompting;
using Microsoft.Extensions.AI;

namespace Chartula.Core.Llm;

/// <summary>
/// Whether a model's answer counts, decided in one place (#94). A readable answer is
/// not evidence that the call behind it was sound: an endpoint that cut the prompt to
/// fit its context window still answers, from the facts it kept (#85), and an
/// endpoint that enforces the schema by constrained decoding returns a well-formed
/// verdict from a model that never understood the task (#86). So every answer passes
/// the same preconditions before it is read, in this order:
/// <list type="number">
/// <item>The whole prompt reached the model. Proof, not suspicion: see
/// <see cref="EnsurePromptArrived"/>. Failing it fails the call - for a rendering and
/// a verdict alike, since both would otherwise be built from facts the model never
/// saw.</item>
/// <item>The answer can be read as the shape that was asked for. A rendering that
/// cannot be read fails; a verdict that cannot be read is reported as not evaluated
/// (#76), because an empty claim list would read as a clean check.</item>
/// <item>A verdict agrees with itself: "unfaithful" with no claims named is not a
/// verdict either.</item>
/// </list>
/// A new way for a call to verify nothing belongs here, as one more precondition.
/// <para>
/// What cannot be checked here, and is not pretended to be: an endpoint that reports
/// the untruncated length whatever it processed, one that reports no usage at all,
/// and - the case no precondition reaches - a model that saw every fact and judged
/// badly. That last one is why the rule-based check exists and always runs.
/// </para>
/// </summary>
internal static class CallValidity
{
    // No real tokenizer packs prose, code and JSON into fewer than roughly 4
    // characters per token; dividing by double that leaves wide margin and still
    // turns "characters Chartula sent" into a token count no tokenizer can undercut.
    private const int MaxCharsPerToken = 8;

    /// <summary>The entries a rephrasing answer supports, or an exception saying why there are none.</summary>
    public static RenderedEntries Entries(ChangelogPrompt prompt, ChatResponse<RenderedEntries> response)
    {
        EnsurePromptArrived(prompt, response.Usage);

        // Unlike the thorough check, an unreadable answer has no honest partial form:
        // without the entries there is no rendering. The generator turns this into a
        // failed audience that says why.
        if (!response.TryGetResult(out RenderedEntries? entries) || entries.Entries is null)
        {
            throw new InvalidOperationException("the model's answer did not match the expected entry format");
        }

        return entries;
    }

    /// <summary>The report a faithfulness answer supports.</summary>
    public static FaithfulnessReport Verdict(ChangelogPrompt prompt, ChatResponse<FaithfulnessVerdict> response)
    {
        EnsurePromptArrived(prompt, response.Usage);

        // A check we cannot read is not a check that passed. Both failures below leave
        // the output unverified, and saying so is the only honest result.
        if (!response.TryGetResult(out FaithfulnessVerdict? verdict))
        {
            return FaithfulnessReport.NotEvaluated("the response did not match the expected format");
        }

        IReadOnlyList<string> claims = verdict.UnsupportedClaims ?? [];
        if (!verdict.IsFaithful && claims.Count == 0)
        {
            return FaithfulnessReport.NotEvaluated("the model called the output unfaithful but listed no claims");
        }

        return FaithfulnessReport.Checked(claims);
    }

    /// <summary>
    /// Fails when the endpoint reports fewer input tokens than the prompt Chartula sent
    /// can have. It runs before the answer is inspected, since an answer from a cut
    /// prompt is well-formed. A provider that reports no usage gives nothing to check,
    /// and the call is let through.
    /// </summary>
    private static void EnsurePromptArrived(ChangelogPrompt prompt, UsageDetails? usage)
    {
        if (usage?.InputTokenCount is not { } reported)
        {
            return;
        }

        long characters = prompt.System.Length + prompt.User.Length;
        long minimumTokens = characters / MaxCharsPerToken;
        if (reported < minimumTokens)
        {
            throw new InvalidOperationException(
                $"the endpoint reported {reported} input tokens for a prompt of {characters} characters, "
                + $"which no tokenizer produces fewer than {minimumTokens} tokens for - the endpoint's "
                + "context window is too small for what Chartula sends. See \"The context window is the "
                + "first thing to get right\" in docs/configuration.md.");
        }
    }
}
