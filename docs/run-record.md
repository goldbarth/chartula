# Run record

The schema of the run record, the file `chartula-runs/<time>-<tag>.json` that every `generate` run keeps on your machine.
[Outputs](outputs.md#the-run-record) says when it is written and how it is named, and [Comparing runs](costs-and-checks.md#comparing-runs) shows what to do with it.

The file is UTF-8, indented JSON, and escapes characters the way [`changelog.json`](changelog-json.md#characters) does.

## Schema

| Field | Type | Description |
| --- | --- | --- |
| `schemaVersion` | integer | The format version. Bumped only on a breaking change. Currently `3`. |
| `recordedAt` | string | When the run was recorded, ISO 8601 in UTC, to the second. |
| `tag` | string | The release tag the run was for. |
| `repository` | string | The repository, as `owner/name`. |
| `range` | object | The commits the run read (see below). |
| `mode` | string | `generate` or `generate --no-publish`. |
| `provenance` | object | What the run was made with, in the fields of [`changelog.json`'s provenance](changelog-json.md#provenance). |
| `audiences` | array | One entry per [audience](glossary.md#audience) the run asked for (see below). |
| `metrics` | object | The model calls, tokens and check findings of the run (see below). |
| `facts` | array | The [facts](glossary.md#fact) the run rendered from, descriptions included (see below). |

### Range

| Field | Type | Description |
| --- | --- | --- |
| `start` | string | Where the [range](glossary.md#range) started: `previous-tag` (found on its own), `since` (named with `--since`) or `first-commit` (a first tag). |
| `from` | string | The tag or commit the range starts after, as named or found. Absent for `first-commit`. |
| `fromCommit` | string | The full hash `from` pointed to when the run read the range. Absent for `first-commit`. |
| `toCommit` | string | The full hash the release tag pointed to when the run read the range. |

`git log <fromCommit>..<toCommit>` lists the commits the run read, even after a tag has moved.

### Facts

One entry per fact, in the fields of a [`changelog.json` change entry](changelog-json.md#change-entry), plus one:

| Field | Type | Description |
| --- | --- | --- |
| `description` | string or null | The pull request description without its HTML comments. `null` for a change from a commit, at `factBase.depth: title-only`, and when the description is empty or an unfilled template ([The title and the description](what-goes-into-a-release.md#the-title-and-the-description)). |

Together they are the whole [fact base](glossary.md#fact-base) the model read, so a [rendering](glossary.md#rendering) can be traced back to its input after a pull request was edited.

### Audience entry

| Field | Type | Description |
| --- | --- | --- |
| `audience` | string | `technical`, `customer` or `product`. |
| `rendered` | boolean | Whether the audience rendered. |
| `flags` | array of objects | What the checks flagged (see below). Present when the audience rendered, and empty when nothing was flagged. Absent when it failed, because a failed audience was never checked and an empty list would read as a clean check. |
| `error` | string | Why the audience failed. Present only when it did. |

### Flag

| Field | Type | Description |
| --- | --- | --- |
| `text` | string | What was flagged, and why. |
| `pullRequest` | integer | The pull request whose fact the flagged passage rephrases (see below). |

A [flag](glossary.md#flag) has a `pullRequest` when the [thorough check](glossary.md#thorough-check) names a pull request that is part of this release.
It has none when it comes from the [rule-based check](glossary.md#rule-based-check), concerns the release as a whole or a change from a commit, names a number the release does not have, or reports a thorough check that could not be evaluated.
Chartula verifies that the number is a pull request of the release, not that the check picked the right one.

### Metrics

Four objects describe the model calls and the checks: `rephrase` and `faithfulnessCheck` count the calls of the rendering and of the thorough check, and `ruleBasedCheck` and `thoroughCheck` count what each check found.

| Field | Description |
| --- | --- |
| `rephrase` | The rendering calls: `calls`, `callsWithoutUsage`, `inputTokens`, `outputTokens`, `failedCalls`, `durationSeconds`, `longestCallSeconds`, and, when they could be counted or were reported, `retries`, `cachedInputTokens` and `reasoningTokens`. |
| `faithfulnessCheck` | The calls of the thorough check, in the same fields. All zero when the check was off. |
| `ruleBasedCheck` | What the rule-based check found: `runs`, `runsWithFindings` and `flags`. |
| `thoroughCheck` | What the thorough check found, in the same three fields, plus `onlyThoroughFlags`, the flags the rule-based check did not raise, and `notEvaluated`, the runs whose answer could not be read. |
| `durationSeconds` | How long the whole run took. |
| `release` | How much release the run worked on: `commits`, `pullRequests`, `facts`, `factsWithDescription`, `descriptionCharacters`, `commitsWithoutPullRequest` and `mergeCommitsSkipped`. |

Each figure means what the same line of the [run summary](costs-and-checks.md#reading-the-run-summary) means.
Times are seconds with millisecond precision.
`retries`, `cachedInputTokens` and `reasoningTokens` are left out when they could not be counted or were not reported, rather than written as zero.
Both call objects are always present, with zeros when they made no call, so any two records compare field by field.

## Stability

- `schemaVersion` is the contract, as for [`changelog.json`](changelog-json.md#stability).
- New optional fields may be added without bumping it; removing or renaming a field, or changing its meaning, bumps it.

Version 2 turned each flag from a string into an object with `text` and `pullRequest`, and added `range`.
A version 1 record holds the same text as a plain string in `flags` and has no `range`; nothing else changed.
Version 3 renamed the start `whole-history` to `first-commit`, since the range ends at the tag and never was the whole history.
A version 2 record with `whole-history` means the same as `first-commit`; nothing else changed.
`facts` was added within version 3 as an optional field, so an earlier version 3 record has none.
Fields added to `metrics` after a record was written read as zero in it, and `retries` as absent.

## Example

The record of the same run as the [`changelog.json` example](changelog-json.md#example), as written, with one of its six facts:

```json
{
  "schemaVersion": 3,
  "recordedAt": "2026-10-01T14:18:19+00:00",
  "tag": "v0.1.0-preview.3",
  "repository": "goldbarth/chartula",
  "range": {
    "start": "previous-tag",
    "from": "v0.1.0-preview.2",
    "fromCommit": "ca35f082b088099ffc62deabc99111c95cbd874f",
    "toCommit": "7620e6d95b62190d12f57ba345479b524674fea1"
  },
  "mode": "generate --no-publish",
  "provenance": {
    "toolVersion": "0.1.0-preview.3+dde0019754ac9f23ec2615fe85b58d3b88ca6b38",
    "provider": "openai-compatible",
    "model": "gpt-6-sol",
    "promptHash": "sha256:18467240be1e8a49ca134d8d94ebaf281104fff70ad715fbc0c6b71dc048b5c1",
    "thinking": "disabled",
    "thoroughCheck": true,
    "factBaseDepth": "title-and-description",
    "checkModel": "gpt-6-sol",
    "checkThinking": "disabled"
  },
  "audiences": [
    {
      "audience": "technical",
      "rendered": true,
      "flags": []
    },
    {
      "audience": "customer",
      "rendered": true,
      "flags": []
    }
  ],
  "metrics": {
    "rephrase": {
      "calls": 2,
      "callsWithoutUsage": 0,
      "inputTokens": 8369,
      "outputTokens": 411,
      "failedCalls": 0,
      "durationSeconds": 9.786,
      "longestCallSeconds": 5.393,
      "retries": 0,
      "cachedInputTokens": 0,
      "reasoningTokens": 0
    },
    "faithfulnessCheck": {
      "calls": 2,
      "callsWithoutUsage": 0,
      "inputTokens": 9084,
      "outputTokens": 40,
      "failedCalls": 0,
      "durationSeconds": 2.687,
      "longestCallSeconds": 1.757,
      "retries": 0,
      "cachedInputTokens": 0,
      "reasoningTokens": 0
    },
    "ruleBasedCheck": {
      "runs": 2,
      "runsWithFindings": 0,
      "flags": 0
    },
    "thoroughCheck": {
      "runs": 2,
      "runsWithFindings": 0,
      "flags": 0,
      "onlyThoroughFlags": 0,
      "notEvaluated": 0
    },
    "durationSeconds": 15.427,
    "release": {
      "commits": 7,
      "pullRequests": 7,
      "facts": 6,
      "factsWithDescription": 6,
      "descriptionCharacters": 15083,
      "commitsWithoutPullRequest": 0,
      "mergeCommitsSkipped": 0
    }
  },
  "facts": [
    {
      "title": "docs: add the changelog Chartula generated for 0.1.0-preview.2",
      "number": 246,
      "url": "https://github.com/goldbarth/chartula/pull/246",
      "category": "Documentation",
      "userVisible": false,
      "breaking": false,
      "linkedIssues": [],
      "labels": [],
      "description": "Follows #139 and the `v0.1.0-preview.2` tag.\n\n- `CHANGELOG.md` section for `0.1.0-preview.2` generated by Chartula and committed as written.\n- Run: `generate --tag v0.1.0-preview.2`, provider `openai-compatible`, model `gpt-6-sol`, thinking `disabled`, thorough check on, binary `0.1.0-preview.2+ca35f08`. 24 pull requests, 23 facts, 69,876 tokens, 41 s. The same run wrote the draft release notes.\n- Only the note above the sections was edited by hand. The old note said nothing was released and named one provider and model for the whole file. Both are no longer true, so it now lists the settings per release.\n- `changelog.json` goes to chartula-evals (`test-runs/`), not here, as with #139.\n\n**Verification**\n\n- `toolVersion` in the run's `changelog.json` is `0.1.0-preview.2+ca35f082b088099ffc62deabc99111c95cbd874f`, the tagged commit.\n- Thorough check: technical 0 flags, customer 2 (customer rendering is not in this file).\n\nTests: none, documentation only."
    }
  ]
}
```
