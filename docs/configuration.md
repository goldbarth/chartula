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

### `faithfulness`

The faithfulness checks. The rule-based check always runs and is not configurable.

| Key | Default | Description |
| --- | --- | --- |
| `thorough` | `true` | Whether the thorough (second-pass LLM) check runs. |

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

faithfulness:
  thorough: true

review:
  enabled: false
```
