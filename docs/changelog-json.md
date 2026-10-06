# `changelog.json` format

The schema of `changelog.json`, the file a `generate` run writes with the release's [facts](glossary.md#fact) and its [renderings](glossary.md#rendering).
[Outputs](outputs.md#changelogjson) says what the file is for, when it is written, and why it holds no pull request description.

The file is UTF-8, indented JSON.

## Schema

| Field | Type | Description |
| --- | --- | --- |
| `schemaVersion` | integer | The format version. Bumped only on a breaking change. Currently `2`. |
| `tag` | string | The release tag the facts belong to. |
| `changes` | array | One entry per change of the release (see below). |
| `renderings` | object | The rendered texts as Markdown, keyed by [audience](glossary.md#audience): `technical`, `customer`, `product`. Only the audiences the run rendered are present, and at least one is ([The renderings](#the-renderings)). |
| `provenance` | object, optional | How the file was made (see below). Absent in a file written without it. |

### Change entry

| Field | Type | Description |
| --- | --- | --- |
| `title` | string | The pull request title, or a line of its description when the title says nothing ([The title and the description](what-goes-into-a-release.md#the-title-and-the-description)). For a change from a commit without a pull request, the commit subject. |
| `number` | integer or null | The pull request number, or `null` for a change from a commit. |
| `url` | string or null | The pull request link, or `null` for a change from a commit. |
| `category` | string | One of the seven categories in [The category](what-goes-into-a-release.md#4-the-category), such as `Feature` or `Fix`. |
| `userVisible` | boolean | Whether a reader can meet the change ([Who can meet a change](what-goes-into-a-release.md#7-who-can-meet-a-change)). |
| `breaking` | boolean | Whether the change is breaking ([Breaking changes](what-goes-into-a-release.md#5-breaking-changes)). |
| `linkedIssues` | array of integers | The issue numbers the change closes, such as `12` for `closes #12`, in the order found, each once. Read from the text the model reads ([How much of each change the model reads](what-goes-into-a-release.md#8-how-much-of-each-change-the-model-reads)); Chartula reads no issue itself. |
| `labels` | array of strings | The labels on the pull request, verbatim and unfiltered. Empty for a change from a commit. |

### Provenance

What a run was made with, so a release can be explained from the file after the run's terminal output is gone.
Each field is left out when the run does not have a value for it, never written empty.

| Field | Type | Description |
| --- | --- | --- |
| `toolVersion` | string | The Chartula version that wrote the file, with the commit it was built from after a `+`: the text `chartula --version` prints. |
| `provider` | string | The provider the run used, `llm.provider` ([`llm`](configuration.md#llm)). |
| `model` | string | The model id the renderings were written with. |
| `promptHash` | string | `sha256:` and a hex digest of every instruction Chartula sends - each audience's system prompt and the [thorough check](glossary.md#thorough-check)'s - without the facts. Two files with the same hash were rendered from the same instructions. |
| `thinking` | string | The configured `llm.thinking`, as the configuration spells it, with `adaptive` recorded as `high`. `provider-default` records the setting, not whether the model thought ([`thinking`](providers.md#thinking)). |
| `thoroughCheck` | boolean | Whether the thorough check ran (`faithfulness.thorough`). |
| `factBaseDepth` | string | The configured `factBase.depth`. A file from before #258 may hold `title-description-and-issues`, which read the same text as `title-and-description`. |
| `checkModel` | string | The model the thorough check asked: `faithfulness.model`, or `llm.model` when that is not set. Present only when the check ran. |
| `checkThinking` | string | The thinking mode the thorough check asked for, spelled as `thinking`. Present only when the check ran. |

Endpoint hosts, [flags](glossary.md#flag) and check verdicts are deliberately not recorded: the file may be published, and a host can name an internal gateway.

## The renderings

Each value of `renderings` is the Markdown text of one rendering, as Chartula put it together: a `### ` heading per group, one `- ` line per entry, lines separated by `\n`.
[Outputs](outputs.md) shows the groups and markers of each audience.

- **No release heading and no front matter.** `CHANGELOG.md` adds `## VERSION - DATE` around the technical text, and `release-<tag>.md` adds the front matter around the customer text; neither is in the file. So the customer page's description is not in the file either.
- **An empty string** when the release has no change for that audience ([A release with nothing to say](outputs.md#a-release-with-nothing-to-say)).
- **`<` is escaped as `\<`** outside code spans, so a placeholder such as `release-<tag>.md` is shown rather than read as an HTML tag. A Markdown renderer shows the bracket; a consumer that uses the text as plain text removes the backslash.
- **The product rendering is here only,** since it has no file of its own.

## Reading it with `jq`

The customer text, ready to post:

```console
$ jq -r '.renderings.customer' changelog.json
### What's New

- **Run range in records:** A version 2 run record now shows how its commit range was chosen, the starting reference, and the commits at both ends. You can use those commits to identify the range the run read, even if a reference moves later.
- **Pull requests on review flags:** Review flags now show a pull request number when the check links a claim to a pull request in the release. You can use that number to find the fact behind the flag, but it reflects the check's reading rather than a verified match.

### Bug Fixes

- **Reasons and limits in review text:** Review flags could quote a claim without explaining why it was flagged, while customer entries could promise outcomes beyond what the facts supported. Flags now include their reasons, and customer entries are instructed to keep outcomes within the limits stated by the facts.
- **Review mode configuration:** Review mode previously approved generated text without showing it to a person when enabled. It is now refused with a configuration error rather than approving text unseen; remove `review.enabled: true` from `chartula.yaml` or set it to `false`.
```

Each change with its category, one per line:

```console
$ jq -r '.changes[] | "\(.category)\t#\(.number)\t\(.title)"' changelog.json
Fix	#252	fix: keep the reason in each flag, and bound the customer outcome by the facts
Feature	#251	feat(observability): record the range a run read in its run record
Feature	#250	feat(faithfulness): name the pull request behind each flag
Documentation	#248	docs: rewrite comments for readability
Fix	#247	fix(cli): refuse review mode until an interactive reviewer exists
Documentation	#246	docs: add the changelog Chartula generated for 0.1.0-preview.2
```

`select(.breaking) |` after `.changes[] |` keeps the breaking changes only, and `select(.userVisible) |` the ones a reader can meet.

## Characters

The file escapes only what JSON requires, so backticks, quotes, `+` and characters outside ASCII, such as `ü`, appear as themselves and the file reads and diffs as text ([#226](https://github.com/goldbarth/chartula/issues/226)).
An emoji is still written as two `\u` codes.
The strings are not escaped for HTML: `<`, `>` and `&` appear as written, and they come from pull request titles, which anyone who can open a pull request writes, and from the text a model wrote from them.
Escape them where you put them into a page, as you would any text you did not write.

## Stability

- `schemaVersion` is the contract. Consumers should read it and reject versions they do not understand.
- The fields of a change entry and the top-level fields other than `provenance` are always present, including when their value is `null`.
- New optional fields may be added without bumping `schemaVersion`; removing or renaming a field, or
  changing a field's meaning, bumps it.

Version 2 removed a change's `description`, so the file can be published without the notes authors wrote for reviewers ([#260](https://github.com/goldbarth/chartula/issues/260)).
A consumer of version 1 that read `description` finds it in the run record's [`facts`](run-record.md#facts).

## Example

The file a real run wrote for Chartula's own `v0.1.0-preview.3`, rendered by `gpt-6-sol` on 2026-10-01, as written:

```json
{
  "schemaVersion": 2,
  "tag": "v0.1.0-preview.3",
  "changes": [
    {
      "title": "fix: keep the reason in each flag, and bound the customer outcome by the facts",
      "number": 252,
      "url": "https://github.com/goldbarth/chartula/pull/252",
      "category": "Fix",
      "userVisible": true,
      "breaking": false,
      "linkedIssues": [],
      "labels": []
    },
    {
      "title": "feat(observability): record the range a run read in its run record",
      "number": 251,
      "url": "https://github.com/goldbarth/chartula/pull/251",
      "category": "Feature",
      "userVisible": true,
      "breaking": false,
      "linkedIssues": [],
      "labels": []
    },
    {
      "title": "feat(faithfulness): name the pull request behind each flag",
      "number": 250,
      "url": "https://github.com/goldbarth/chartula/pull/250",
      "category": "Feature",
      "userVisible": true,
      "breaking": false,
      "linkedIssues": [],
      "labels": []
    },
    {
      "title": "docs: rewrite comments for readability",
      "number": 248,
      "url": "https://github.com/goldbarth/chartula/pull/248",
      "category": "Documentation",
      "userVisible": false,
      "breaking": false,
      "linkedIssues": [],
      "labels": []
    },
    {
      "title": "fix(cli): refuse review mode until an interactive reviewer exists",
      "number": 247,
      "url": "https://github.com/goldbarth/chartula/pull/247",
      "category": "Fix",
      "userVisible": true,
      "breaking": false,
      "linkedIssues": [],
      "labels": []
    },
    {
      "title": "docs: add the changelog Chartula generated for 0.1.0-preview.2",
      "number": 246,
      "url": "https://github.com/goldbarth/chartula/pull/246",
      "category": "Documentation",
      "userVisible": false,
      "breaking": false,
      "linkedIssues": [],
      "labels": []
    }
  ],
  "renderings": {
    "technical": "### Added\n\n- Record the read range beside `tag` in schema version 2 run records, including its start type, source ref, and resolved commit hashes. ([#251](https://github.com/goldbarth/chartula/pull/251))\n- Add a `pullRequest` reference to each thorough-check flag when its number exists in the fact base. ([#250](https://github.com/goldbarth/chartula/pull/250))\n\n### Fixed\n\n- Fix thorough-check flags to include the reason alongside the quoted claim. ([#252](https://github.com/goldbarth/chartula/pull/252))\n- Refuse `review.enabled: true` until an interactive reviewer exists. ([#247](https://github.com/goldbarth/chartula/pull/247))",
    "customer": "### What's New\n\n- **Run range in records:** A version 2 run record now shows how its commit range was chosen, the starting reference, and the commits at both ends. You can use those commits to identify the range the run read, even if a reference moves later.\n- **Pull requests on review flags:** Review flags now show a pull request number when the check links a claim to a pull request in the release. You can use that number to find the fact behind the flag, but it reflects the check's reading rather than a verified match.\n\n### Bug Fixes\n\n- **Reasons and limits in review text:** Review flags could quote a claim without explaining why it was flagged, while customer entries could promise outcomes beyond what the facts supported. Flags now include their reasons, and customer entries are instructed to keep outcomes within the limits stated by the facts.\n- **Review mode configuration:** Review mode previously approved generated text without showing it to a person when enabled. It is now refused with a configuration error rather than approving text unseen; remove `review.enabled: true` from `chartula.yaml` or set it to `false`."
  },
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
  }
}
```
