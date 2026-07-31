# Configuration

Chartula runs with sensible defaults and needs no configuration to work.
A `chartula.yaml` in the repository root refines that default behavior; it is never required.
Environment variables override the file, so anything here can be set with `Chartula__Section__Key` too.

A minimal starting point is shipped as [`chartula.example.yaml`](../chartula.example.yaml) - copy it to `chartula.yaml` and uncomment only what you need.

## Sections

### `llm`

The model provider and which model to use. API keys are read by environment-variable name, never from this file.

| Key | Default | Description |
| --- | --- | --- |
| `provider` | `anthropic` | The LLM provider. Only `anthropic` is implemented today. |
| `model` | `claude-opus-4-8` | The model id passed to the provider. See [Choosing a model](#choosing-a-model). |
| `apiKeyEnvironmentVariable` | `ANTHROPIC_API_KEY` | Name of the environment variable holding the API key. |
| `maxOutputTokens` | `16000` | Ceiling on the tokens the model may produce per call. |
| `thinking` | `provider-default` | Whether the model reasons before answering. One of `provider-default`, `disabled`, `adaptive`. |

Raise `maxOutputTokens` for releases whose changelog runs long.
A ceiling that is too low truncates the generated text mid-sentence rather than failing, so a run that ends abruptly is the signal to raise it.

#### Choosing a model

`model` is a free-text id passed straight to the provider, but not every id is worth using.
These are the ones Chartula is built for.

| Model id | Input / output per MTok | Context | Max output | Notes |
| --- | --- | --- | --- | --- |
| `claude-opus-5` | $5 / $25 | 1M | 128K | The current top tier. |
| `claude-opus-4-8` | $5 / $25 | 1M | 128K | The default. |
| `claude-sonnet-5` | $3 / $15 | 1M | 128K | $2 / $10 introductory through 2026-08-31. |
| `claude-haiku-4-5` | $1 / $5 | 200K | 64K | The cheapest. Keep `maxOutputTokens` at or below 64000. |

Prices are Anthropic's first-party rates as of 2026-07-31 and change over time; treat the table as a starting point, not a quote.
Model ids are complete as written - do not append a date suffix.

All four support the structured output the thorough faithfulness check needs, so a cheaper model does not cost you that check.
What it can cost you is changelog quality, which is the whole product - so read the output of a cheaper run before adopting it, rather than assuming the saving is free.

The reason to care: a run pays per token twice over, once to rephrase and once for the thorough check, and prompt iteration means running it repeatedly.
Generating the v0.1.0 changelog of this repository cost $1.78 on Opus 4.8.
The same run on Haiku 4.5 costs a fifth of that at list prices, which is the difference between iterating freely and rationing runs.

Haiku 4.5's 200K context is the one hard limit in the table.
A release with many changes at `factBase.depth: title-and-description` produces a long fact list, and that list is sent once per audience plus once per thorough check.
If a run fails on context rather than on quality, that is the signal to move up a tier rather than to trim the facts.

#### `thinking`

Thinking is reasoning the model does before it answers. You never see it in the changelog, and it is billed as output tokens.

The default, `provider-default`, sends no thinking field at all and leaves every model on its own behavior - which is **not** the same behavior across models. Measured on the same release, same command, on 2026-07-31:

| Model | Thinks by default | Thorough-check output for an identical "no findings" verdict |
| --- | --- | --- |
| `claude-opus-4-8` | no | 69 tokens |
| `claude-haiku-4-5` | no | 238 tokens |
| `claude-opus-5` | yes | 4,967 tokens |
| `claude-sonnet-5` | yes | 6,982 tokens |

That run cost $0.89 on Opus 4.8 and $1.34 on Opus 5 at an identical per-token price. Most of the difference is thinking.

Not every value works on every model, and a rejected value fails the run rather than falling back:

- `adaptive` needs Claude 4.6 or newer. Haiku 4.5 has no adaptive mode and rejects it.
- `disabled` is fine on the models above, but Claude Fable 5 always thinks and rejects an explicit off - leave `provider-default` there.

Set `disabled` or `adaptive` to make the behavior the same on every model rather than a property of the one you picked. Which is better for a changelog is an open question: nothing measured so far shows thinking finding claims the non-thinking runs missed, but that was one clean release and is weak evidence either way. Change it deliberately and read the run metrics afterwards.

### `github`

How the GitHub API is reached. The token is read by environment-variable name, never from this file.

| Key | Default | Description |
| --- | --- | --- |
| `apiBaseUrl` | `https://api.github.com/` | REST API base URL (override for GitHub Enterprise). |
| `tokenEnvironmentVariable` | `GITHUB_TOKEN` | Name of the environment variable holding the API token. |

### `labels`

Steer curation with GitHub labels. All optional; with no rules, labels are ignored.

| Key | Default | Description |
| --- | --- | --- |
| `exclude` | (none) | Labels that exclude a pull request from the changelog. |
| `category` | (none) | Map of label name to category, forcing that change's category. |
| `onlyIncludeLabeled` | `false` | When true, only labeled pull requests are included. |

### `filter`

Which categories are dropped from the changelog.

| Key | Default | Description |
| --- | --- | --- |
| `excludeCategories` | `[Internal]` | Category names to exclude. An explicit (possibly empty) list replaces the default. |

Valid categories: `Feature`, `Fix`, `Performance`, `Documentation`, `Refactor`, `Internal`, `Other`.

### `factBase`

How much source material feeds the fact base.

| Key | Default | Description |
| --- | --- | --- |
| `depth` | `title-and-description` | One of `title-only`, `title-and-description`, `title-description-and-issues`. |

### `categories`

How categories are presented in the output.

| Key | Default | Description |
| --- | --- | --- |
| `order` | `[Feature, Fix, Performance, Documentation, Refactor, Other, Internal]` | The order categories appear in. Unlisted categories sort last. |
| `names` | (enum names) | Map of category name to display name (e.g. `Fix: Bug Fixes`). |
| `breakingProminent` | `true` | Whether breaking changes float to the top, shown near the top. |

Valid category names: `Feature`, `Fix`, `Performance`, `Documentation`, `Refactor`, `Internal`, `Other`.

### `faithfulness`

The faithfulness checks. The rule-based check always runs and is not configurable.

| Key | Default | Description |
| --- | --- | --- |
| `thorough` | `true` | Whether the thorough (second-pass LLM) check runs. |

Every run reports what each check caught and what it cost - see [`run-metrics.md`](run-metrics.md) for deciding whether the thorough check earns its tokens.

### `review`

Review mode - present generated texts for human sign-off before writing.

| Key | Default | Description |
| --- | --- | --- |
| `enabled` | `false` | Whether review mode is on. Opt-in; never forced. |

## Example

```yaml
llm:
  model: claude-opus-4-8

labels:
  exclude: [wontfix, duplicate]
  category:
    security: Fix
  onlyIncludeLabeled: false

filter:
  excludeCategories: [Internal, Documentation]

factBase:
  depth: title-description-and-issues

categories:
  order: [Feature, Fix, Performance, Documentation, Refactor, Other, Internal]
  names:
    Fix: Bug Fixes
  breakingProminent: true

faithfulness:
  thorough: true

review:
  enabled: false
```
