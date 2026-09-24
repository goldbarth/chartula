using Chartula.Core.Curation;
using Chartula.Core.History;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Tests.Curation;

/// <summary>
/// A revert and the change it takes back, in the same release, shipped nothing, so
/// neither reaches the facts.
/// Only a revert that names its target unambiguously is paired: commit hashes from the
/// range, or GitHub's "Reverts owner/repo#N". Any other revert stays as a fact of its own.
/// </summary>
public sealed class RevertPairingTests
{
    private const string Waterfall = "d85a4b9aa1b2c3d4e5f60718293a4b5c6d7e8f90";
    private const string DeepLink = "d883341aa1b2c3d4e5f60718293a4b5c6d7e8f90";
    private const string Pulse = "bc826baaa1b2c3d4e5f60718293a4b5c6d7e8f90";
    private const string Localise = "dd883e5aa1b2c3d4e5f60718293a4b5c6d7e8f90";
    private const string RevertSha = "823e48daa1b2c3d4e5f60718293a4b5c6d7e8f90";

    private readonly ReleaseChangeResolver _resolver = new();

    private static CommitRange Range(params string[] shas)
        => new("v1.3.0", "v1.2.0", [.. shas.Select(sha => new CommitInfo(sha, "subject"))]);

    private static PullRequestInfo Pull(int number, string title, string? body, params string[] shas)
        => new(number, title, body, [], $"https://github.com/goldbarth/port-tidewatch/pull/{number}") { CommitShas = shas };

    // The shape found in the port-tidewatch v1.3.0 run.
    private static readonly PullRequestInfo[] TidewatchPulls =
    [
        Pull(57, "feat(dashboard): localise UI to German", null, Localise),
        Pull(59, "feat(observability): surface pipeline latency pulse in the dashboard (#44)", null, Pulse),
        Pull(60, "feat(observability): add per-gauge Jaeger trace deep-link (#45)", null, DeepLink),
        Pull(61, "feat(observability): self-rendered trace waterfall under the hood (#46)", null, Waterfall),
        Pull(62, "revert(observability): drop M8 dashboard observability surfacing (#44, #45, #46)",
            "Reverts the three M8 commits - latency pulse (#59), Jaeger deep-link (#60), trace waterfall (#61).\n\nReverts d85a4b9, d883341, bc826ba.",
            RevertSha),
    ];

    private IReadOnlyList<int?> Numbers(CommitRange range, params PullRequestInfo[] pulls)
        => [.. _resolver.Resolve(range, pulls).Select(change => change.Number)];

    [Fact]
    public void A_revert_and_the_changes_it_names_in_the_same_release_all_drop_out()
        => Assert.Equal([57], Numbers(Range(Localise, Pulse, DeepLink, Waterfall, RevertSha), TidewatchPulls));

    [Fact]
    public void A_revert_of_something_from_an_earlier_release_stays_as_a_change()
    {
        PullRequestInfo revert = Pull(62, "revert: drop the trace waterfall", "This reverts commit 0123abc4567.", RevertSha);

        Assert.Equal([57, 62], Numbers(Range(Localise, RevertSha), TidewatchPulls[0], revert));
    }

    [Fact]
    public void A_revert_that_names_something_outside_the_range_takes_back_only_what_it_found_and_stays()
    {
        PullRequestInfo revert = Pull(62, "revert: drop the waterfall and an older change", "Reverts d85a4b9 and 0123abc4567.", RevertSha);

        Assert.Equal([57, 62], Numbers(Range(Localise, Waterfall, RevertSha), TidewatchPulls[0], TidewatchPulls[3], revert));
    }

    [Fact]
    public void GitHubs_revert_button_is_read_by_the_pull_request_it_names()
    {
        PullRequestInfo revert = Pull(62, "Revert \"feat(observability): self-rendered trace waterfall under the hood (#46)\"",
            "Reverts goldbarth/port-tidewatch#61", RevertSha);

        Assert.Equal([57], Numbers(Range(Localise, Waterfall, RevertSha), TidewatchPulls[0], TidewatchPulls[3], revert));
    }

    [Fact]
    public void A_pull_request_number_in_prose_does_not_pair()
    {
        // "#61" can be context as easily as a target. Only hashes and GitHub's form count.
        PullRequestInfo revert = Pull(62, "revert: drop the waterfall", "The waterfall from #61 goes.", RevertSha);

        Assert.Equal([61, 62], Numbers(Range(Waterfall, RevertSha), TidewatchPulls[3], revert));
    }

    [Fact]
    public void Reverting_a_revert_leaves_the_original_change_in_the_release()
    {
        const string ReRevertSha = "9f9f9f9aa1b2c3d4e5f60718293a4b5c6d7e8f90";
        PullRequestInfo revert = Pull(62, "revert: drop the waterfall", "Reverts d85a4b9.", RevertSha);
        PullRequestInfo reRevert = Pull(63, "revert: bring the waterfall back", "Reverts 823e48d.", ReRevertSha);

        Assert.Equal([61], Numbers(Range(Waterfall, RevertSha, ReRevertSha), TidewatchPulls[3], revert, reRevert));
    }

    [Fact]
    public void A_hash_that_is_ambiguous_in_the_range_pairs_nothing()
    {
        // Two commits share the prefix, so the hash does not identify one commit.
        const string Twin = "d85a4b9ff1b2c3d4e5f60718293a4b5c6d7e8f90";
        PullRequestInfo other = Pull(64, "feat: another", null, Twin);
        PullRequestInfo revert = Pull(62, "revert: drop the waterfall", "Reverts d85a4b9.", RevertSha);

        Assert.Equal([61, 64, 62], Numbers(Range(Waterfall, Twin, RevertSha), TidewatchPulls[3], other, revert));
    }

    [Fact]
    public void Only_a_revert_pairs_a_feature_that_names_a_hash_does_not()
    {
        PullRequestInfo feature = Pull(64, "feat: follow up on d85a4b9", "Builds on d85a4b9.", "aaaaaaaaa1b2c3d4e5f60718293a4b5c6d7e8f90");

        Assert.Equal([61, 64], Numbers(Range(Waterfall, feature.CommitShas[0]), TidewatchPulls[3], feature));
    }
}
