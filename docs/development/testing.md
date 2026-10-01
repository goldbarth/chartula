# Testing

The suite needs no API key, no network and no tokens, and it runs in seconds.
So run it as often as you like; there is no test you have to avoid because it costs something.

Each test project tests one layer, through the seam the layer is used by:

| Project | What it tests | How |
| --- | --- | --- |
| `Chartula.Core.Tests` | The domain: curation, facts, rendering, checks, the pipeline | Plain unit tests, and the pipeline replayed on stored fact bases with a stand-in model |
| `Chartula.Infrastructure.Tests` | The adapters: git, the GitHub API, the file writers | Real git in a temporary repository, GitHub answered by a stub handler, files in a temporary directory |
| `Chartula.Cli.Tests` | The command surface and the composition root | The built binary as its own process, the real provider SDKs with the network stubbed, and the report around a stub pipeline |

## Running the tests

The whole suite, as CI runs it:

```bash
dotnet test Chartula.slnx -c Release
```

One class or one test, in the project that holds it:

```bash
dotnet test tests/Chartula.Core.Tests -c Release --filter "FullyQualifiedName~FactBaseBuilderTests"
dotnet test tests/Chartula.Infrastructure.Tests -c Release --filter "FullyQualifiedName~Reads_the_url_of_the_named_remote"
```

Name the project rather than the solution with a filter, because every other project then reports `No test matches the given testcase filter`.
`FullyQualifiedName~` matches any part of `Namespace.Class.Method`, so a class name, a folder such as `Faithfulness` or a method name each work.

## `Chartula.Core.Tests`

The tests follow the folders of `src/Chartula.Core`, a class of tests per type, and touch no network, no git and no model.
On top of them, the pipeline runs end to end on stored fact bases, and the prompt text is held against snapshots.

### Fixtures: stored fact bases

`tests/Chartula.Core.Tests/Fixtures/` holds fact bases the pipeline replays:

| Fixture | The case it represents |
| --- | --- |
| `typical-release` | A spread of categories, with links, linked issues, labels and descriptions. |
| `breaking-release` | Breaking changes among ordinary ones. |
| `commits-only-release` | Built from commits: no pull request numbers, links, labels or descriptions. |
| `internal-only-release` | Nothing user-visible, so the customer rendering has nothing to say. |
| `empty-release` | No changes at all. |

They differ along the axes the pipeline branches on, and `FactBaseFixtureTests` asserts that they still do, so the set cannot quietly collapse into five variations of the same release.

A fixture is the `tag` and `facts` of a [run record](../run-record.md#facts), byte for byte as the run wrote them.
Not `changelog.json`: it leaves out the pull request descriptions, which the model reads.
`FactBaseFixtureTests` writes every fixture again with the run record's writer and compares the result to the file, so a fixture that no longer matches what a real run writes fails the suite.

To freeze a real release into a fixture:

1. Run `generate --no-publish` on it, which writes a record to `chartula-runs/`.
2. Copy the two fields out of the record as they are:

   ```bash
   record=chartula-runs/20261001T141819Z-v0.1.0-preview.3.json
   { echo '{'; grep -m1 '^  "tag": ' "$record"; sed -n '/^  "facts": \[/,/^  \]/p' "$record"; echo '}'; } \
     > tests/Chartula.Core.Tests/Fixtures/my-release.json
   ```

   Not through `jq`: it writes the characters the record escapes as plain characters, so the comparison fails.
3. Name a constant for it on `FactBaseFixture` and add it to `FactBaseFixture.All`. The project copies every `.json` in `Fixtures/` to the output, so nothing else changes.
4. Run `dotnet test tests/Chartula.Core.Tests -c Release`.

A release whose descriptions hold a fenced code block, or a phrase broken over two lines, fails `Text_that_only_repeats_the_facts_is_never_flagged` ([#315](https://github.com/goldbarth/chartula/issues/315)).
The stand-in model echoes each fact on one line, as a rendering is, and the rule-based check then looks for the joined text in descriptions that still have their line breaks.
Pick a release without them until that is fixed.

### Stand-in models

The pipeline under test is the production one: generator, renderer, formatter, both checks, review and the writers.
Only the model is a stand-in, from `FixtureModels.cs`:

- `EchoingChangelogModel` writes each fact back as its entry. It invents nothing, so any flag a test sees is the checker's doing.
- `InventingChangelogModel` adds a fact, so a test can prove the checks catch it, not only that clean input stays clean.
- `UnreachableChangelogModel` throws when it is called, so "this path costs no tokens" is something the suite enforces.

No test in this project can reach a real model, because `Chartula.Core` references no provider package ([ADR 0004](adr/0004-models-through-ichatclient-alone.md)).

### Prompt snapshots

The prompt text is held against a copy in `tests/Chartula.Core.Tests/Prompting/Snapshots/`: the system prompt of each audience, the thorough check's system prompt and user template, and the prompt hash that `changelog.json` records as `promptHash`.
A prompt change moves the output in ways no unit test catches, so `PromptSnapshotTests` makes it at least visible: changing the text fails the test until the snapshot is updated.

To accept a change, run the tests with the update switch and commit the rewritten snapshots next to the prompt change:

```bash
CHARTULA_UPDATE_SNAPSHOTS=1 dotnet test tests/Chartula.Core.Tests --filter "FullyQualifiedName~PromptSnapshotTests"
```

The snapshot diff is then what a reviewer reads, and the changed hash is what tells two runs' outputs apart.
Whether the change is an improvement is not something this test can say; that is what the evaluation harness in [chartula-evals](https://github.com/goldbarth/chartula-evals) is for.

## `Chartula.Infrastructure.Tests`

The adapters are tested against what they talk to, or a stand-in that answers the same way:

- **git:** `TempGitRepository` creates a repository in the temporary directory with the git on your `PATH`, commits and tags in it, and can clone it shallow over `file://`. The reader then parses real git output, not a copy of it.
- **GitHub:** `StubHttpMessageHandler`, or a routing handler in the test, answers each request with GitHub's JSON and records what was asked. So a test asserts both the parsing and the requests, without a request leaving the machine.
- **Files:** the writers write into a fresh directory under the temporary directory, which the test deletes.

The git tests need git on the `PATH`.
A git configuration that rewrites `git@github.com:` URLs, through `url.<base>.insteadOf` or the `GIT_CONFIG_COUNT` variables some cloud containers set, fails `Reads_the_url_of_the_named_remote`, because git hands the rewritten URL back.
Run the suite without that rewrite, for example with `env -u GIT_CONFIG_COUNT dotnet test Chartula.slnx -c Release`.

## `Chartula.Cli.Tests`

The CLI is tested three ways, each for a different question:

- **What a user types and reads:** `CliProcess.RunChartulaAsync` starts the built `chartula` as its own process, in a temporary directory, with the environment the test sets and stdin closed, as in a CI job.
  The argument checks, configuration errors, first tags, shallow clones, `--version` and the progress lines are tested this way.
  Where a run would go on to GitHub, the test points `Chartula__GitHub__ApiBaseUrl` at `https://127.0.0.1:1/`, where nothing listens, so the run stops there without leaving the machine.
- **What reaches a provider or GitHub:** the test builds the configuration from its own environment with `ChartulaConfiguration.Build`, and the client with the production factory, passing an `HttpMessageHandler` in place of the network: `LlmServiceCollectionExtensions.CreateChatClient(configuration, transport)` for a model, `DoctorCommand.RunAsync(..., new DoctorTransports(gitHub, model))` for `doctor`.
  The real provider SDKs run, so what each SDK sends and what it puts into an error is tested; only the network is replaced.
- **How a result is reported:** `ReleaseCommand.RunAsync(new StubPipeline(outcome), ...)` prints the report and returns the exit status for an outcome the test builds, without running a pipeline.

The process tests start the binary from the test's output directory, where the project reference puts it, so `dotnet test` builds it first.
