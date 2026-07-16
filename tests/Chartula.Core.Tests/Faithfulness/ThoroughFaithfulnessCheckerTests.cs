using Chartula.Core.Categorization;
using Chartula.Core.Faithfulness;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Faithfulness;

public sealed class ThoroughFaithfulnessCheckerTests
{
    private static FactBase Facts() => new("v1.0.0", [
        new ChangeFact("fix: correct an off-by-one in the parser", 7, "https://example/pull/7",
            ChangeCategory.Fix, IsUserVisible: true, IsBreaking: false, [], "Fixes a parser bug."),
    ]);

    [Fact]
    public async Task Runs_a_second_pass_and_flags_unsupported_claims_when_enabled()
    {
        StubFaithfulnessModel model = new(
            new FaithfulnessReport(false, ["'security hole' is not supported: the fact is a parser bug fix"]));
        IThoroughFaithfulnessChecker checker = new ThoroughFaithfulnessChecker(
            model, new ThoroughFaithfulnessOptions(Enabled: true));

        FaithfulnessReport report = await checker.CheckAsync("This release closed a security hole.", Facts());

        Assert.False(report.IsFaithful);
        Assert.Contains(report.UnsupportedClaims, c => c.Contains("security hole"));
        Assert.Equal(1, model.CheckCallCount); // the second pass ran
    }

    [Fact]
    public async Task Feeds_the_output_and_the_grounded_facts_to_the_model()
    {
        StubFaithfulnessModel model = new(new FaithfulnessReport(true, []));
        IThoroughFaithfulnessChecker checker = new ThoroughFaithfulnessChecker(
            model, new ThoroughFaithfulnessOptions(Enabled: true));

        await checker.CheckAsync("Fixed a parser bug.", Facts());

        Assert.Equal("Fixed a parser bug.", model.LastRequest!.Output);
        Assert.Contains(model.LastRequest.Facts.Statements, s => s.Contains("off-by-one"));
    }

    [Fact]
    public async Task Makes_no_call_and_reports_faithful_when_disabled()
    {
        StubFaithfulnessModel model = new(new FaithfulnessReport(false, ["should not be used"]));
        IThoroughFaithfulnessChecker checker = new ThoroughFaithfulnessChecker(
            model, new ThoroughFaithfulnessOptions(Enabled: false));

        FaithfulnessReport report = await checker.CheckAsync("This release closed a security hole.", Facts());

        Assert.True(report.IsFaithful);
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
        StubFaithfulnessModel model = new(new FaithfulnessReport(true, []));
        IThoroughFaithfulnessChecker checker = new ThoroughFaithfulnessChecker(
            model, new ThoroughFaithfulnessOptions(Enabled: true));

        FaithfulnessReport report = await checker.CheckAsync(string.Empty, Facts());

        Assert.True(report.IsFaithful);
        Assert.Equal(0, model.CheckCallCount);
    }
}
