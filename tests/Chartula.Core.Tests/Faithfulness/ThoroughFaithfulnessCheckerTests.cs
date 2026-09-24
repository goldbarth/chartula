using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Faithfulness;
using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Faithfulness;

public sealed class ThoroughFaithfulnessCheckerTests
{
    private static FactBase Facts() => new("v1.0.0", [
        new ChangeFact("fix: correct an off-by-one in the parser", 7, "https://example/pull/7",
            ChangeCategory.Fix, IsUserVisible: true, IsBreaking: false, [], [], "Fixes a parser bug."),
    ]);

    [Fact]
    public async Task Runs_a_second_pass_and_flags_unsupported_claims_when_enabled()
    {
        StubFaithfulnessModel model = new(
            FaithfulnessReport.Checked([new FaithfulnessFlag("'security hole' is not supported: the fact is a parser bug fix")]));
        IThoroughFaithfulnessChecker checker = new ThoroughFaithfulnessChecker(
            model, new ThoroughFaithfulnessOptions(Enabled: true));

        FaithfulnessReport report = await checker.CheckAsync("This release closed a security hole.", Facts());

        Assert.True(report.HasFindings);
        Assert.Contains(report.UnsupportedClaims, c => c.Text.Contains("security hole"));
        Assert.Equal(1, model.CheckCallCount); // the second pass ran
    }

    [Fact]
    public async Task Feeds_the_output_and_the_grounded_facts_to_the_model()
    {
        StubFaithfulnessModel model = new(FaithfulnessReport.Checked([]));
        IThoroughFaithfulnessChecker checker = new ThoroughFaithfulnessChecker(
            model, new ThoroughFaithfulnessOptions(Enabled: true));

        await checker.CheckAsync("Fixed a parser bug.", Facts());

        Assert.Equal("Fixed a parser bug.", model.LastRequest!.Output);
        Assert.Contains(model.LastRequest.Facts.Statements, s => s.Contains("off-by-one"));
    }

    // The number opens the fact, where the check takes it from, and with the link it is
    // also the reference the composer adds to a technical entry.
    [Fact]
    public async Task Grounds_the_pull_request_reference_a_rendering_carries()
    {
        StubFaithfulnessModel model = new(FaithfulnessReport.Checked([]));
        IThoroughFaithfulnessChecker checker = new ThoroughFaithfulnessChecker(
            model, new ThoroughFaithfulnessOptions(Enabled: true));

        await checker.CheckAsync("Fixed a parser bug. ([#7](https://example/pull/7))", Facts());

        Assert.Equal(
            "[#7] Fix: fix: correct an off-by-one in the parser (https://example/pull/7) - Fixes a parser bug.",
            Assert.Single(model.LastRequest!.Facts.Statements));
    }

    [Fact]
    public async Task A_flag_keeps_the_pull_request_the_fact_base_has()
    {
        StubFaithfulnessModel model = new(FaithfulnessReport.Checked([new FaithfulnessFlag("overstates the fix", 7)]));
        IThoroughFaithfulnessChecker checker = new ThoroughFaithfulnessChecker(
            model, new ThoroughFaithfulnessOptions(Enabled: true));

        FaithfulnessReport report = await checker.CheckAsync("Fixed every parser bug.", Facts());

        Assert.Equal(new FaithfulnessFlag("overstates the fix", 7), Assert.Single(report.UnsupportedClaims));
    }

    // The model named a number no fact has, such as the issue a title mentions. It is no
    // fact of the release, so the flag loses it as its fact but keeps the model's lead.
    [Fact]
    public async Task A_flag_loses_a_pull_request_the_fact_base_does_not_have()
    {
        StubFaithfulnessModel model = new(FaithfulnessReport.Checked([new FaithfulnessFlag("overstates the fix", 243)]));
        IThoroughFaithfulnessChecker checker = new ThoroughFaithfulnessChecker(
            model, new ThoroughFaithfulnessOptions(Enabled: true));

        FaithfulnessReport report = await checker.CheckAsync("Fixed every parser bug.", Facts());

        FaithfulnessFlag flag = Assert.Single(report.UnsupportedClaims);
        Assert.Null(flag.PullRequest);
        Assert.Equal(
            "overstates the fix (The check named #243, which is not a pull request of this release.)",
            flag.Text);
    }

    [Fact]
    public async Task Grounds_no_reference_for_a_change_without_a_pull_request()
    {
        StubFaithfulnessModel model = new(FaithfulnessReport.Checked([]));
        IThoroughFaithfulnessChecker checker = new ThoroughFaithfulnessChecker(
            model, new ThoroughFaithfulnessOptions(Enabled: true));
        FactBase commitOnly = new("v1.0.0", [
            new ChangeFact("fix: correct an off-by-one in the parser", null, null,
                ChangeCategory.Fix, IsUserVisible: true, IsBreaking: false, [], [], null),
        ]);

        await checker.CheckAsync("Fixed a parser bug.", commitOnly);

        Assert.Equal("Fix: fix: correct an off-by-one in the parser", Assert.Single(model.LastRequest!.Facts.Statements));
    }

    [Fact]
    public async Task Makes_no_call_and_reports_faithful_when_disabled()
    {
        StubFaithfulnessModel model = new(FaithfulnessReport.Checked([new FaithfulnessFlag("should not be used")]));
        IThoroughFaithfulnessChecker checker = new ThoroughFaithfulnessChecker(
            model, new ThoroughFaithfulnessOptions(Enabled: false));

        FaithfulnessReport report = await checker.CheckAsync("This release closed a security hole.", Facts());

        Assert.Equal(FaithfulnessCheckStatus.Skipped, report.Status);
        Assert.Empty(report.UnsupportedClaims);
        Assert.Equal(0, model.CheckCallCount); // toggle off: no second pass
    }

    [Fact]
    public void Is_enabled_by_default()
    {
        Assert.True(new ThoroughFaithfulnessOptions().Enabled);
    }

    [Fact]
    public async Task Makes_no_call_for_empty_output()
    {
        StubFaithfulnessModel model = new(FaithfulnessReport.Checked([]));
        IThoroughFaithfulnessChecker checker = new ThoroughFaithfulnessChecker(
            model, new ThoroughFaithfulnessOptions(Enabled: true));

        FaithfulnessReport report = await checker.CheckAsync(string.Empty, Facts());

        Assert.Equal(FaithfulnessCheckStatus.Skipped, report.Status);
        Assert.Equal(0, model.CheckCallCount);
    }

    // #234: a check whose model the endpoint does not serve used to end the whole run
    // with an unhandled exception, taking the renderings with it.
    [Fact]
    public async Task A_failed_call_leaves_the_text_unverified_with_the_failure_as_the_reason()
    {
        IThoroughFaithfulnessChecker checker = new ThoroughFaithfulnessChecker(
            new FailingModel(new InvalidOperationException("anthropic at https://x/v1/messages answered 404 Not Found")),
            new ThoroughFaithfulnessOptions(Enabled: true));

        FaithfulnessReport report = await checker.CheckAsync("Fixed a parser bug.", Facts());

        Assert.Equal(FaithfulnessCheckStatus.NotEvaluated, report.Status);
        Assert.Equal("anthropic at https://x/v1/messages answered 404 Not Found", report.Reason);
        Assert.Empty(report.UnsupportedClaims);
    }

    [Fact]
    public async Task Cancellation_is_not_a_failed_check()
    {
        IThoroughFaithfulnessChecker checker = new ThoroughFaithfulnessChecker(
            new FailingModel(new OperationCanceledException()), new ThoroughFaithfulnessOptions(Enabled: true));

        await Assert.ThrowsAsync<OperationCanceledException>(() => checker.CheckAsync("Fixed a parser bug.", Facts()));
    }

    private sealed class FailingModel(Exception failure) : IChangelogModel
    {
        public Task<RenderedEntries> RephraseAsync(RephraseRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not exercised by faithfulness tests.");

        public Task<FaithfulnessReport> CheckFaithfulnessAsync(
            FaithfulnessRequest request, CancellationToken cancellationToken = default)
            => Task.FromException<FaithfulnessReport>(failure);
    }
}
