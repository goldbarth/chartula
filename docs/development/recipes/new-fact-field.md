# Recipe: adding a field to the fact base

A fact is one change of a release as Chartula establishes it before a model is involved, and the fact base is all facts of one release ([Glossary](../../glossary.md#fact)).
A fact field is one thing a fact knows about its change: its category, whether it is breaking, its labels.
Adding one touches every layer between the source and the files a run writes, and the test fixtures, the stored fact bases the tests replay.
[#120](https://github.com/goldbarth/chartula/pull/120), which added `Labels`, is a worked example; the steps below follow it, with the files as they are named today.

Read [ADR 0001](../adr/0001-facts-first-then-rephrase.md) before you start: a fact field is decided by code, never by a prompt.

## 1. Get it from the source

A fact is only as good as where it comes from.

- **From a pull request:** add it to `PullRequestInfo` (`Core/PullRequests`), then read it in `GitHubPullRequestReader` and, if the GitHub response carries it, map it in `GitHubJson.cs` (`Chartula.Infrastructure/PullRequests`).
  The JSON context is source-generated, so a new property on the response type needs nothing else.
- **From git:** add it to `CommitInfo` (`Core/History`) and read it in `GitCliCommitReader`.
- Carry it through `ReleaseChange` (`Core/Curation`), set in `ReleaseChangeResolver` for both sources.
  A change built from a commit alone often has nothing to give: say so with `null` or an empty list, as `Labels: []` does for commits.

## 2. Put it on the fact

- Add the parameter to `ChangeFact` (`Core/Facts/ChangeFact.cs`) with a doc comment that says what an absent value means.
- Add it to `Equals` and `GetHashCode` in the same file.
  A collection is compared by content, not by reference, like `LinkedIssues` and `Labels`, because a fact is a value.
- Set it in `FactBaseBuilder.ToFact` (`Core/Facts/FactBaseBuilder.cs`).
  If `factBase.depth` should decide whether it is included, check it there, as the description does.

Carry the value as the source gives it.
Which part of it an output shows is that output's decision, and a fact dropped here cannot be recovered downstream.

## 3. Keep it in the run record, and decide whether it is published

The run record, the local file each run writes to `chartula-runs/`, keeps every field of every fact; `changelog.json` is the published release payload and keeps the fields a reader may see ([Outputs](../../outputs.md)).

- **The run record, always:** add the property to `FactEntry` (`Core/Serialization/FactEntry.cs`), and carry it in `From`, `ToFact` and `Equals`.
  A record written before the field existed does not carry it; read that as absent (`Labels ?? []`), never as a failure.
- **`changelog.json`, only if it may be published:** add the property to `ChangelogChange` (`Core/Serialization/ChangelogDocument.cs`) and set it in `ChangelogJsonSerializer.Serialize`.
  Text an author wrote for reviewers, such as the description, stays out of it ([#260](https://github.com/goldbarth/chartula/issues/260)).
- **Document it** where it is written: in the change entry of [`changelog-json.md`](../../changelog-json.md#change-entry) when it is published, which the run record's facts share, or in the facts table of [`run-record.md`](../../run-record.md#facts) when it is not.

Each file carries a `schemaVersion`, its format version.
A new field is additive and keeps it; renaming or removing a field, or changing what one means, bumps it ("Stability" on both pages).

## 4. Decide whether the model sees it

A field on the fact does not reach the prompt by itself.
`GroundedFactsFactory` (`Core/Generation`) builds the statement each fact is sent to the model as, and today that holds the title, the description, the category and a breaking or action-required marker.

- **If the field changes structure,** the group an entry stands under, its order or a marker: decide it in code in `GroundedFactsFactory` or `RenderingComposer`, as `Labels` decide "action required". Do not describe it to the model and hope.
- **If the model should be able to mention it:** add it to the statement, and add its text to `BuildHaystack` in `Core/Faithfulness/RuleBasedFaithfulnessChecker.cs`, the text the rule-based check searches.
  Otherwise that check flags every number or name the model takes from the new field as unsupported.
- **If it needs a word in the system prompt:** that lives in `Core/Prompting/ChangelogPromptBuilder.Prompts.cs`, and changing it fails `PromptSnapshotTests` until the snapshot is updated ([Prompt snapshots](../testing.md#prompt-snapshots)).
  A prompt change moves the output in ways the suite cannot judge; say in the pull request how you checked it.

Leaving the model out is a valid answer: `Labels` are never sent as text; they only decide, in code, which customer entries need action.

## 5. Update the fixtures

The fixtures in `tests/Chartula.Core.Tests/Fixtures/` are the `tag` and `facts` of run records.
`FactBaseFixtureTests`, in `tests/Chartula.Core.Tests/Fixtures/FixturePipelineTests.cs`, writes each of them again with the run record's writer and compares the result to the file (`Every_fixture_is_exactly_what_a_real_run_would_write`).
After step 3 it fails for every fixture, which is the point: the files no longer match what a run writes.

There is no update switch.
Add the field to each fact of each fixture by hand, at the position `FactEntry` writes it, with a value that fits the case the fixture stands for: a change from a commit in `commits-only-release.json` has no pull request to take it from.
The failing test shows the expected text, so the diff tells you where it goes.
If the field opens a case none of the five fixtures covers, add a fixture as [Testing](../testing.md#fixtures-stored-fact-bases) describes.

## 6. Test it

The places a new field touches, as a checklist:

- `FactBaseBuilderTests`: the value reaches the fact, including when the source has none.
- `FactEqualityTests`: two facts differing only in the new field are unequal, and equal content in different instances is equal.
- `FactBaseRoundTripTests`: the fact base survives a round trip through the run record, and a record written before the field still loads.
- `ChangelogJsonSerializerTests`, if the field is published: it is written, and the description still is not.
- Tests that construct a `ChangeFact` directly need the new argument; the compiler lists them.

```bash
dotnet build Chartula.slnx -c Release   # no warnings
dotnet test Chartula.slnx -c Release    # no key, no network, no tokens
```
