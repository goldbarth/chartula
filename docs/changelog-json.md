# `changelog.json` format

Chartula writes the release fact base to `changelog.json` as a durable, machine-readable record.
It is the grounded source that other outputs (the JS widget, the RSS feed, the audience texts) build on.

The file is UTF-8, indented JSON.

## Schema

| Field | Type | Description |
| --- | --- | --- |
| `schemaVersion` | integer | The format version. Bumped only on a breaking change. Currently `1`. |
| `tag` | string | The release tag the facts belong to. |
| `changes` | array | One entry per included change (see below). |
| `renderings` | object | The rendered audience texts, keyed by audience (`technical`, `customer`, `product`). Empty when no texts were generated. |

### Change entry

| Field | Type | Description |
| --- | --- | --- |
| `title` | string | The change title, verbatim from the source. |
| `number` | integer or null | The pull request number, or `null` for commit-based changes. |
| `url` | string or null | The pull request link, or `null` for commit-based changes. |
| `category` | string | One of `Feature`, `Fix`, `Performance`, `Documentation`, `Refactor`, `Internal`, `Other`. |
| `userVisible` | boolean | Whether the change is visible to end users. |
| `breaking` | boolean | Whether the change is a breaking change. |
| `linkedIssues` | array of integers | Linked issue numbers. Empty unless the fact-base depth includes issues. |
| `description` | string or null | The source description, or `null` when the depth excludes it. |

Every field of a change entry is an established fact derived deterministically from the pull request or commit.
The `renderings` object holds the audience texts the LLM produced by rephrasing those facts; the facts themselves are never LLM-generated.

## Stability

- `schemaVersion` is the contract. Consumers should read it and reject versions they do not understand.
- Fields are always present, including when their value is `null`.
- New optional fields may be added without bumping `schemaVersion`; removing or renaming a field, or
  changing a field's meaning, bumps it.

## Example

```json
{
  "schemaVersion": 1,
  "tag": "v1.2.0",
  "changes": [
    {
      "title": "feat: add dark mode",
      "number": 42,
      "url": "https://github.com/owner/repo/pull/42",
      "category": "Feature",
      "userVisible": true,
      "breaking": false,
      "linkedIssues": [12],
      "description": "Adds a dark theme toggle."
    }
  ],
  "renderings": {
    "technical": "- feat: add dark mode (#42) - adds a dark theme toggle",
    "customer": "- Dark mode is here.",
    "product": "- Dark mode toggle added."
  }
}
```
