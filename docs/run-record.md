# Run record

Every `generate` run keeps what it did and what it cost in a file of its own, `chartula-runs/<time>-<tag>.json`, next to the other outputs.
It holds the [run metrics](run-metrics.md) as the summary prints them, the settings the run was made with, and how each audience came out.

Comparing runs - a prompt change, another model, thinking on or off - then means comparing two files, not copying numbers out of a terminal.
A figure written down later is a figure from memory; the record is the run's own.

## What it is not

The record is never published or uploaded.
Token usage is a fact about the run, not about the release, so it stays out of `changelog.json` and the release notes.

It holds no rendered text: the texts are in `changelog.json`.
It does hold the faithfulness flags and any error message, so treat it like a local log.

Whether to commit it is yours to decide.
For a repository whose releases Chartula writes, ignore it:

```gitignore
/chartula-runs/
```

Commit it where the history of runs is the point, as in an evaluation repository.

## When it is written

- `generate` and `generate --no-publish` write one record per run.
- A run in which no audience rendered is recorded too: its tokens were spent all the same.
  Only `changelog.json` is held back then, so an earlier run's file is not replaced.
- `preview` writes nothing, and that includes the record.

The run summary names the file under its metrics:

```text
  Total:            12,656 tokens
  Recorded in /home/me/repo/chartula-runs/20260922T123015Z-v1.2.0.json
```

One file per run rather than one file appended to: each record is a whole JSON document that can be read and diffed on its own, and no run rewrites what an earlier one recorded.
The name starts with the time in UTC, so the files list in the order the runs were made.
A character a file name cannot carry, such as the `/` in `release/1.2`, becomes `-`; two runs in the same second get `-2`, `-3` and so on.

## Schema

The file is UTF-8, indented JSON.

| Field | Type | Description |
| --- | --- | --- |
| `schemaVersion` | integer | The format version. Bumped only on a breaking change. Currently `2`. |
| `recordedAt` | string | When the run was recorded, ISO 8601 in UTC, to the second. |
| `tag` | string | The release tag the run was for. |
| `repository` | string | The repository, as `owner/name`. |
| `range` | object | The commits the run read (see below). Absent in a version 1 record. |
| `mode` | string | `generate` or `generate --no-publish`. |
| `provenance` | object | What the run was made with, in the same fields as [`changelog.json`'s provenance](changelog-json.md#provenance): tool version, provider, model, prompt hash, `thinking`, `thoroughCheck`, `factBaseDepth`, `checkModel` and `checkThinking`. |
| `audiences` | array | One entry per audience the run asked for (see below). |
| `metrics` | object | Calls, tokens and check activity (see below). |

### Range

| Field | Type | Description |
| --- | --- | --- |
| `start` | string | Where the range started: `previous-tag` (found on its own), `since` (named with `--since`) or `whole-history` (`--whole-history`, no start at all). |
| `from` | string | The tag or commit the range starts after, as named or found. Absent for `whole-history`. |
| `fromCommit` | string | The full hash `from` pointed to when the run read the range. Absent for `whole-history`. |
| `toCommit` | string | The full hash the release tag pointed to when the run read the range. |

A run over a wrong range looks like any other run: the tag is the same, the facts and flags are not.
The range tells the two apart, and `git log <fromCommit>..<toCommit>` lists the commits the run read, even after a tag has moved.
Its size is `commits` under [`metrics.release`](#metrics).

### Audience entry

| Field | Type | Description |
| --- | --- | --- |
| `audience` | string | `technical`, `customer` or `product`. |
| `rendered` | boolean | Whether the audience rendered. |
| `flags` | array of objects | What the faithfulness checks flagged (see below). Present when the audience rendered, empty when nothing was flagged. |
| `error` | string | Why the audience failed. Present only when it did. |

A failed audience has no `flags`: it was never checked, and an empty list would read as a clean check.

### Flag

| Field | Type | Description |
| --- | --- | --- |
| `text` | string | What was flagged, and why. |
| `pullRequest` | integer | The pull request whose fact the flagged passage rephrases. Absent when the flag concerns no single fact. |

The thorough check names the pull request, and Chartula keeps the number only when the release has that pull request among its facts.
A number the release does not have, such as an issue a title mentions, stays in the flag's text as the check's lead, not as its `pullRequest`.
A number the release does have is still the check's reading: Chartula verifies that the fact exists, not that the check picked the right one.
The rule-based check's flags never have one: they name a number or a name that no fact contains.
Neither does a claim about the release as a whole, a fact that came from a commit without a pull request, or a thorough check that could not be evaluated.

Flags from several runs group by `pullRequest`, so a finding that repeats is found by the fact it concerns rather than by how alike its wording is.

### Metrics

| Field | Description |
| --- | --- |
| `rephrase` | The rephrasing calls: `calls`, `callsWithoutUsage`, `inputTokens`, `outputTokens`, `failedCalls`, `durationSeconds`, `longestCallSeconds`, and - when they could be counted or were reported - `retries`, `cachedInputTokens` and `reasoningTokens`. |
| `faithfulnessCheck` | The thorough check's calls, in the same fields. All zero when the check was off. |
| `ruleBasedCheck` | `runs`, `runsWithFindings` and `flags` of the free check. |
| `thoroughCheck` | The same three for the thorough check, plus `onlyThoroughFlags` - the claims only it caught - and `notEvaluated` - the runs that came back unreadable. |
| `durationSeconds` | How long the whole run took. |
| `release` | How much release the run worked on: `commits`, `pullRequests`, `facts`, `factsWithDescription` and `descriptionCharacters` - the description text the model read, at the configured `factBase.depth`. |

Times are seconds with millisecond precision.
`cachedInputTokens` is the part of `inputTokens` served from the provider's cache, `reasoningTokens` the part of `outputTokens` spent reasoning.
`retries`, `cachedInputTokens` and `reasoningTokens` are left out when they could not be counted or were not reported, rather than written as zero - see [When a run was slow](run-metrics.md#when-a-run-was-slow).
A record written before these fields existed reads with zeros for them and no `retries`.

The numbers mean what they mean in the [run summary](run-metrics.md#what-the-numbers-mean).
`callsWithoutUsage` above zero makes the token counts a lower bound.
Both operations are always present, with zeros when they made no call, so any two records compare field by field.

## Stability

- `schemaVersion` is the contract, as for [`changelog.json`](changelog-json.md#stability).
- New optional fields may be added without bumping it; removing or renaming a field, or changing its meaning, bumps it.

Version 2 turned each flag from a string into an object with `text` and `pullRequest`, and added `range`.
A version 1 record holds the same text as a plain string in `flags` and has no `range`; nothing else changed.

## Example

```json
{
  "schemaVersion": 2,
  "recordedAt": "2026-09-22T12:30:15+00:00",
  "tag": "v1.2.0",
  "repository": "owner/repo",
  "range": {
    "start": "previous-tag",
    "from": "v1.1.0",
    "fromCommit": "9f2e1c47b0a6d3e85c1f4a7b2d9e0c3f6a8b1d42",
    "toCommit": "4c8a0d1e7f3b92a65e0d4c1b8f7a3e2d9c6b5a10"
  },
  "mode": "generate --no-publish",
  "provenance": {
    "toolVersion": "0.1.0-preview.1+2c43772833e4ed32790e5549848f7f42bc0e4eee",
    "provider": "anthropic",
    "model": "claude-sonnet-5",
    "promptHash": "sha256:3f1c...e9a0",
    "thinking": "disabled",
    "thoroughCheck": true,
    "factBaseDepth": "title-and-description"
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
      "flags": [
        {
          "text": "'Search is now faster' is a claim of degree the facts do not back.",
          "pullRequest": 412
        },
        {
          "text": "The number '3' is not supported by the facts."
        }
      ]
    }
  ],
  "metrics": {
    "rephrase": {
      "calls": 2,
      "callsWithoutUsage": 0,
      "inputTokens": 5437,
      "outputTokens": 859,
      "failedCalls": 0,
      "durationSeconds": 41.023,
      "longestCallSeconds": 22.105,
      "retries": 1
    },
    "faithfulnessCheck": {
      "calls": 2,
      "callsWithoutUsage": 0,
      "inputTokens": 4120,
      "outputTokens": 96,
      "failedCalls": 0,
      "durationSeconds": 9.87,
      "longestCallSeconds": 5.2,
      "retries": 0
    },
    "ruleBasedCheck": {
      "runs": 2,
      "runsWithFindings": 1,
      "flags": 1
    },
    "thoroughCheck": {
      "runs": 2,
      "runsWithFindings": 1,
      "flags": 1,
      "onlyThoroughFlags": 1,
      "notEvaluated": 0
    },
    "durationSeconds": 64.311,
    "release": {
      "commits": 14,
      "pullRequests": 10,
      "facts": 9,
      "factsWithDescription": 7,
      "descriptionCharacters": 22512
    }
  }
}
```
