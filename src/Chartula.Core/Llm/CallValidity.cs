using Chartula.Core.Prompting;
using Microsoft.Extensions.AI;

namespace Chartula.Core.Llm;

/// <summary>
/// Decides in one place whether a model's answer counts (#94).
/// A readable answer does not prove that the call behind it was sound:
/// <list type="bullet">
/// <item>An endpoint that cut the prompt to fit its context window still answers,
/// from the facts it kept (#85).</item>
/// <item>An endpoint that enforces the schema by constrained decoding returns a
/// well-formed verdict from a model that never understood the task (#86).</item>
/// </list>
/// So every answer must pass the same preconditions before it is read, in this order:
/// <list type="number">
/// <item>The whole prompt reached the model. The check fails only on proof, not on
/// suspicion (see <see cref="EnsurePromptArrived"/>). Failing it fails the call, for a
/// rendering and a verdict alike: both would otherwise rest on facts the model never saw.</item>
/// <item>The answer can be read in the requested shape. An unreadable rendering fails.
/// An unreadable verdict is reported as not evaluated (#76), because an empty claim
/// list would look like a clean check.</item>
/// <item>A verdict is consistent: "unfaithful" without any named claim is not a verdict.</item>
/// </list>
/// Add any new way for a call to verify nothing here, as one more precondition.
/// <para>
/// These cases cannot be checked here:
/// <list type="bullet">
/// <item>an endpoint that reports the untruncated length whatever it processed,</item>
/// <item>an endpoint that reports no usage at all,</item>
/// <item>a model that saw every fact and judged badly. No precondition catches this
/// case, which is why the rule-based check exists and always runs.</item>
/// </list>
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

        // Unlike an unreadable verdict, an unreadable rendering cannot be reported
        // partially: without the entries there is no rendering. The generator turns
        // this exception into a failed audience that says why.
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

        // An unreadable check has not passed. Both failures below leave the output
        // unverified, so they report it as not evaluated.
        if (!response.TryGetResult(out FaithfulnessVerdict? verdict))
        {
            return FaithfulnessReport.NotEvaluated("the response did not match the expected format");
        }

        // A claim without text names nothing a reviewer can look at, whatever fact it names.
        List<FaithfulnessFlag> claims =
        [
            .. (verdict.UnsupportedClaims ?? [])
                .Where(static claim => !string.IsNullOrWhiteSpace(claim?.Claim))
                .Select(static claim => new FaithfulnessFlag(claim.Claim, claim.PullRequest)),
        ];
        if (!verdict.IsFaithful && claims.Count == 0)
        {
            return FaithfulnessReport.NotEvaluated("the model called the output unfaithful but listed no claims");
        }

        return FaithfulnessReport.Checked(claims);
    }

    /// <summary>
    /// Fails when the endpoint reports fewer input tokens than the prompt Chartula sent
    /// must have. It runs before the answer is inspected, because an answer from a cut
    /// prompt is still well-formed.
    /// A provider that reports no usage gives nothing to check, so the call passes.
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
