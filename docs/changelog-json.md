# `changelog.json` format

Chartula writes the release fact base and the audience texts to `changelog.json`, a machine-readable record of one release.
A tool that reacts to a release, such as a webhook, reads it instead of parsing Markdown.

The file holds the current release only, and every `generate` run overwrites it.
One file is one release notification: a consumer that receives it has everything about that release, and nothing it has to tell apart from earlier ones.
To keep a history, store each file where your release pipeline keeps its artifacts.

The file is UTF-8, indented JSON.

**`description` carries each pull request description in full.**
Publishing the file publishes every one of them, including notes written for reviewers rather than readers.
Read them before you publish the file.

## Schema

| Field | Type | Description |
| --- | --- | --- |
| `schemaVersion` | integer | The format version. Bumped only on a breaking change. Currently `1`. |
| `tag` | string | The release tag the facts belong to. |
| `changes` | array | One entry per included change (see below). |
| `renderings` | object | The rendered audience texts as Markdown, keyed by audience (`technical`, `customer`, `product`). Only the audiences the run rendered are present. A written file has at least one, because a run in which no audience rendered writes no files. |
| `provenance` | object, optional | How the file was made (see below). Absent in a file written without it, including every file from before it existed. |

### Change entry

| Field | Type | Description |
| --- | --- | --- |
| `title` | string | For a pull request, its title, trimmed. When the title says nothing - empty, starting with `Merge `, or exactly `wip`, `update`, `updates`, `misc`, `changes`, `fix`, `fixes` or `cleanup` - the first informative line of the description takes its place, or `PR #<n>` when there is none. This fallback reads the description whatever `factBase.depth` says, so even at `title-only` a pull request titled `fix` shows a line of its description. For a commit-based change, the commit subject. |
| `number` | integer or null | The pull request number, or `null` for commit-based changes. |
| `url` | string or null | The pull request link, or `null` for commit-based changes. |
| `category` | string | One of `Feature`, `Fix`, `Performance`, `Documentation`, `Refactor`, `Internal`, `Other`. |
| `userVisible` | boolean | Whether a reader can come into contact with the change. Decided by the visibility labels in [`configuration.md`](configuration.md), with the category as the fallback; a breaking change is always `true`. |
| `breaking` | boolean | Whether the change is a breaking change. |
| `linkedIssues` | array of integers | The numbers after `close`, `closes`, `closed`, `fix`, `fixes`, `fixed`, `resolve`, `resolves` or `resolved` and a `#` in the title or description (`closes #12`), in the order found, each once. Chartula reads no issue, so the number is all the file knows about it. Empty unless `factBase.depth` is `title-description-and-issues`. |
| `labels` | array of strings | The labels on the pull request, verbatim and unfiltered. Empty when the source carries none, as a commit-based change does. |
| `description` | string or null | The source description with HTML comments removed, since GitHub does not show them to a reader. `null` for a commit-based change, when the depth excludes it, or when the body is empty or an unfilled template (nothing but headings and checklist items). |

Every field of a change entry is an established fact derived deterministically from the pull request or commit.
The `renderings` object holds the audience texts the LLM produced by rephrasing those facts; the facts themselves are never LLM-generated.

### Provenance

What a run was made with, so a release can be explained from the file after the run's terminal output is gone.
Each field is left out when the run does not have a value for it, never written empty.

| Field | Type | Description |
| --- | --- | --- |
| `toolVersion` | string | The Chartula version that wrote the file, with the commit it was built from after a `+`. |
| `provider` | string | The model provider as configured: `anthropic` or `openai-compatible`. |
| `model` | string | The model id the renderings were written with. |
| `promptHash` | string | `sha256:` and a hex digest of every instruction Chartula sends - each audience's system prompt and the thorough check's - without the facts. Two files with the same hash were rendered from the same instructions. |
| `thinking` | string | The configured `llm.thinking`: `provider-default`, `disabled`, `low`, `medium`, `high` or `xhigh`, as the configuration spells it (`adaptive` is recorded as `high`). `provider-default` records the setting, not whether the model thought - some models think by default, others do not (see [`thinking`](providers.md#thinking)). |
| `thoroughCheck` | boolean | Whether the thorough faithfulness check ran (`faithfulness.thorough`). |
| `factBaseDepth` | string | The configured `factBase.depth`: `title-only`, `title-and-description` or `title-description-and-issues`. |
| `checkModel` | string | The model the thorough check asked: `faithfulness.model`, or `llm.model` when not set. Present only when the check ran. |
| `checkThinking` | string | The thinking mode the thorough check asked for, spelled as `thinking`. Present only when the check ran. |

`thinking`, `thoroughCheck`, `factBaseDepth`, `checkModel` and `checkThinking` are the settings that move a run's cost and output most, so two files can be compared by what they were made with rather than by what someone remembers.

Endpoint hosts, flags and check verdicts are deliberately not recorded: the file may be published, and a host can name an internal gateway.

## Stability

- `schemaVersion` is the contract. Consumers should read it and reject versions they do not understand.
- The fields of a change entry and the top-level fields other than `provenance` are always present, including when their value is `null`.
- New optional fields may be added without bumping `schemaVersion`; removing or renaming a field, or
  changing a field's meaning, bumps it.

## Example

Three of the six changes of Chartula's own `v0.1.0-preview.3`, as its run wrote them.
Each description is cut after its first line here, and the renderings keep only the entries of these changes; the file holds both in full.
#246 is a `Documentation` change and not `userVisible`, so it is a fact without an entry in either rendering.

```json
{
  "schemaVersion": 1,
  "tag": "v0.1.0-preview.3",
  "changes": [
    {
      "title": "feat(observability): record the range a run read in its run record",
      "number": 251,
      "url": "https://github.com/goldbarth/chartula/pull/251",
      "category": "Feature",
      "userVisible": true,
      "breaking": false,
      "linkedIssues": [],
      "labels": [],
      "description": "Follows #250, which introduced run record schema version 2, and #227 and #231."
    },
    {
      "title": "fix(cli): refuse review mode until an interactive reviewer exists",
      "number": 247,
      "url": "https://github.com/goldbarth/chartula/pull/247",
      "category": "Fix",
      "userVisible": true,
      "breaking": false,
      "linkedIssues": [],
      "labels": [],
      "description": "Follows #19."
    },
    {
      "title": "docs: add the changelog Chartula generated for 0.1.0-preview.2",
      "number": 246,
      "url": "https://github.com/goldbarth/chartula/pull/246",
      "category": "Documentation",
      "userVisible": false,
      "breaking": false,
      "linkedIssues": [],
      "labels": [],
      "description": "Follows #139 and the `v0.1.0-preview.2` tag."
    }
  ],
  "renderings": {
    "technical": "### Added\n\n- Record the range read for each run, including its start, source ref, and resolved commit hashes. ([#251](https://github.com/goldbarth/chartula/pull/251))\n\n### Fixed\n\n- Reject `review.enabled: true` because no interactive reviewer is available. ([#247](https://github.com/goldbarth/chartula/pull/247))",
    "customer": "### What's New\n\n- **Run range records:** Run records now show the range each run read, including its starting point and the commits at both ends. You can count on those commits to identify the range later, even if a tag or branch moves.\n\n### Bug Fixes\n\n- **Review mode availability:** Turning on review mode no longer approves generated text without showing it to a reviewer. The command stops with a configuration error, so you can count on enabled review mode not silently approving text; remove the setting or set it to false in chartula.yaml or through Chartula__Review__Enabled."
  },
  "provenance": {
    "toolVersion": "0.1.0-preview.3+7620e6d95b62190d12f57ba345479b524674fea1",
    "provider": "openai-compatible",
    "model": "gpt-6-sol",
    "promptHash": "sha256:18467240be1e8a49ca134d8d94ebaf281104fff70ad715fbc0c6b71dc048b5c1",
    "thinking": "disabled",
    "thoroughCheck": true,
    "factBaseDepth": "title-and-description",
    "checkModel": "gpt-6-sol",
    "checkThinking": "disabled"
  }
}
```
