# Recipe: adding a field to the fact base

A fact field is something Chartula knows about a change before a model is involved: its category, whether it is breaking, its labels.
Adding one touches every layer between the source and `changelog.json`, and the fixtures on disk.
[#120](https://github.com/goldbarth/chartula/pull/120), which added `Labels`, is a complete worked example; the steps below follow it.

Read [Facts first, then rephrased](../adr/0001-facts-first-then-rephrase.md) before you start: a fact field is decided by code, never by a prompt.

## 1. Get it from the source

A fact is only as good as where it comes from.

- **From a pull request:** add it to `PullRequestInfo` (`Core/PullRequests`), then read it in `GitHubPullRequestReader` and, if the GitHub response carries it, map it in `GitHubJson.cs` (`Chartula.Infrastructure/PullRequests`).
  The JSON context is source-generated, so a new DTO property needs nothing else.
- **From git:** add it to `CommitInfo` (`Core/History`) and read it in `GitCliCommitReader`.
- Carry it through `ReleaseChange` (`Core/Curation`), set in `ReleaseChangeResolver` for both sources.
  A change built from a commit alone often has nothing to give: say so with `null` or an empty list, as `Labels: []` does for commits.

## 2. Put it on the fact

- Add the parameter to `ChangeFact` (`Core/Facts/ChangeFact.cs`) with a doc comment that says what an absent value means.
- Add it to `Equals` and `GetHashCode` in the same file.
  A collection is compared by content, not reference, like `LinkedIssues` and `Labels`.
- Set it in `FactBaseBuilder.ToFact` (`Core/Facts/FactBaseBuilder.cs`).
  If `FactBaseDepth` should decide whether it is included, check it there, as `Description` does.

Carry the value as the source gives it.
Which part of it an output shows is that output's decision, and a fact dropped here cannot be recovered downstream.

## 3. Write it to `changelog.json`

- Add the property to `ChangelogChange` (`Core/Serialization/ChangelogDocument.cs`).
- Write it in `ChangelogJsonSerializer.Serialize` and read it back in `DeserializeFactBase`.
  A file written before the field existed does not carry it; read that as absent (`change.Labels ?? []`), never as a failure.
- Document it in [`changelog-json.md`](../changelog-json.md): the field table and the example.

A new field is additive and keeps `schemaVersion: 1`.
Renaming or removing a field, or changing what one means, bumps it - see "Stability" there.

## 4. Decide whether the model sees it

A field on the fact does not reach the prompt by itself.
`GroundedFactsFactory` (`Core/Generation`) builds the statement each fact is sent as, and today it holds only the title, description, category and a breaking or action-required marker.

- **If the field changes structure** (which group an entry stands under, its order, a marker): decide it in code in `GroundedFactsFactory` or `RenderingComposer`, as `Labels` decide "action required". Do not describe it to the model and hope.
- **If the model should be able to mention it:** add it to the statement, and add its text to `BuildHaystack` in `Core/Faithfulness/RuleBasedFaithfulnessChecker.cs`.
  Otherwise the rule-based check flags every number or name the model takes from the new field as unsupported.
- **If it needs a word in the system prompt:** that lives in `Core/Prompting/ChangelogPromptBuilder.Prompts.cs`, and changing it fails `PromptSnapshotTests` until the snapshot is updated (see [Prompt snapshots](../test-fixtures.md#prompt-snapshots)).
  A prompt change moves the output in ways the suite cannot judge; say in the pull request how you checked it.

Leaving the model out is a valid answer: `Labels` are never sent as text; they only decide, in code, which customer entries are marked as requiring action.

## 5. Regenerate the fixtures

`Every_fixture_is_exactly_what_a_real_run_would_write` in `FixturePipelineTests` re-serializes each file in `tests/Chartula.Core.Tests/Fixtures/` and compares it to disk.
After step 3 it fails for every fixture, which is the point: the files no longer match what a run writes.

There is no update switch.
Add the field to each change of each fixture by hand, at the position the writer emits it, with a value that fits the case the fixture stands for: a commit-based change in `commits-only-release.json` has no pull request to take it from.
The failing test shows the expected text, so the diff tells you exactly where it goes.
If the field opens a case none of the five fixtures covers, add a fixture as [Test fixtures](../test-fixtures.md) describes.

## 6. Test it

The places #120 touched, as a checklist:

- `FactBaseBuilderTests`: the value reaches the fact, including when the source has none.
- `FactEqualityTests`: two facts differing only in the new field are unequal, and equal content in different instances is equal.
- `ChangelogJsonSerializerTests` and `ChangelogJsonRoundTripTests`: written, read back, and a document without the field still loads.
- Tests that construct a `ChangeFact` directly need the new argument; the compiler lists them.

```bash
dotnet build Chartula.slnx -c Release   # no warnings
dotnet test Chartula.slnx -c Release    # no key, no network, no tokens
```
