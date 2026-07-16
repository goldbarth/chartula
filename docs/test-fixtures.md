# Test fixtures

The pipeline is tested against stored fact bases in `tests/Chartula.Core.Tests/Fixtures/`.
The suite can run as often as you like at no token cost, so tests are never something you avoid running.

## The fixtures are ordinary `changelog.json` files

A fixture is exactly what a run writes - the format in [`changelog-json.md`](changelog-json.md), nothing else.
That means a real release can be frozen into a fixture by copying the `changelog.json` a run produced into the fixtures folder.

`FactBaseFixtureTests` re-serializes every fixture and compares it to the file on disk.
If the writer's output ever drifts from these files, that test fails: a fixture that no longer matches what a real run writes has stopped representing one.

To add a fixture, drop the file in `Fixtures/`, name a constant for it on `FactBaseFixture`, and add it to `FactBaseFixture.All`.

## What the fixtures cover

| Fixture | The case it represents |
| --- | --- |
| `typical-release` | A spread of categories, with links, linked issues and descriptions. |
| `breaking-release` | Breaking changes among ordinary ones. |
| `commits-only-release` | Built from commits: no pull request numbers, links or descriptions. |
| `internal-only-release` | Nothing user-visible, so the customer rendering has nothing to say. |
| `empty-release` | No changes at all. |

They are picked to differ along the axes the pipeline actually branches on.
`FactBaseFixtureTests` asserts that they still do, so the set cannot quietly collapse into five variations of the same release.

## Why no live call can happen

`Chartula.Core` depends on `Microsoft.Extensions.AI` alone - no provider package.
The fixture tests live in `Chartula.Core.Tests`, so reaching a real model is not something the tests avoid by discipline; it is not reachable from there at all.

The stand-in models in `FixtureModels.cs` fill the seam:

- `EchoingChangelogModel` rephrases by echoing the grounded facts back. It invents nothing, so any flag a test sees is the checker's doing.
- `InventingChangelogModel` invents a fact, so tests can prove the checks still bite rather than only proving that clean input stays clean.
- `UnreachableChangelogModel` throws if reached, which turns "this path costs no tokens" into something the suite enforces.

Everything downstream of the facts is the production type: generator, renderer, formatter, both faithfulness checks, review and the writers.
Only the model is a stand-in.
