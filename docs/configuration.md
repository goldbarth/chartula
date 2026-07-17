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
| `model` | `claude-opus-4-8` | The model id passed to the provider. |
| `apiKeyEnvironmentVariable` | `ANTHROPIC_API_KEY` | Name of the environment variable holding the API key. |
| `maxOutputTokens` | `16000` | Ceiling on the tokens the model may produce per call. |

Raise `maxOutputTokens` for releases whose changelog runs long.
A ceiling that is too low truncates the generated text mid-sentence rather than failing, so a run that ends abruptly is the signal to raise it.

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
