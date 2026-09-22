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
| `schemaVersion` | integer | The format version. Bumped only on a breaking change. Currently `1`. |
| `recordedAt` | string | When the run was recorded, ISO 8601 in UTC, to the second. |
| `tag` | string | The release tag the run was for. |
| `repository` | string | The repository, as `owner/name`. |
| `mode` | string | `generate` or `generate --no-publish`. |
| `provenance` | object | What the run was made with, in the same fields as [`changelog.json`'s provenance](changelog-json.md#provenance): tool version, provider, model, prompt hash, `thinking`, `thoroughCheck` and `factBaseDepth`. |
| `audiences` | array | One entry per audience the run asked for (see below). |
| `metrics` | object | Calls, tokens and check activity (see below). |

### Audience entry

| Field | Type | Description |
| --- | --- | --- |
| `audience` | string | `technical`, `customer` or `product`. |
| `rendered` | boolean | Whether the audience rendered. |
| `flags` | array of strings | What the faithfulness checks flagged. Present when the audience rendered, empty when nothing was flagged. |
| `error` | string | Why the audience failed. Present only when it did. |

A failed audience has no `flags`: it was never checked, and an empty list would read as a clean check.

### Metrics

| Field | Description |
| --- | --- |
| `rephrase` | The rephrasing calls: `calls`, `callsWithoutUsage`, `inputTokens`, `outputTokens`, `failedCalls`, `durationSeconds`, `longestCallSeconds`, and - when they could be counted or were reported - `retries`, `cachedInputTokens` and `reasoningTokens`. |
| `faithfulnessCheck` | The thorough check's calls, in the same fields. All zero when the check was off. |
| `ruleBasedCheck` | `runs`, `runsWithFindings` and `flags` of the free check. |
| `thoroughCheck` | The same three for the thorough check, plus `onlyThoroughFlags` - the claims only it caught - and `notEvaluated` - the runs that came back unreadable. |
| `durationSeconds` | How long the whole run took. |

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

## Example

```json
{
  "schemaVersion": 1,
  "recordedAt": "2026-09-22T12:30:15+00:00",
  "tag": "v1.2.0",
  "repository": "owner/repo",
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
        "The text mentions 'faster', which no fact supports."
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
      "runsWithFindings": 0,
      "flags": 0
    },
    "thoroughCheck": {
      "runs": 2,
      "runsWithFindings": 1,
      "flags": 1,
      "onlyThoroughFlags": 1,
      "notEvaluated": 0
    },
    "durationSeconds": 64.311
  }
}
```
