# Configuration

Chartula runs without a configuration file.
A `chartula.yaml` refines its defaults, and every setting can also be given as an environment variable.
This page lists every setting with its default and its valid values.

## Where the file is read from

Chartula reads `chartula.yaml`, or `chartula.yml`, from the directory the run starts in.
It does not search parent directories, so a run started in a subdirectory of your repository reads no file; start it from the directory that holds the file, usually the repository root.
When both names exist, the run stops with a configuration error that names both files, because the settings in one of them would not be in force:

```console
$ chartula preview
Configuration error: Both /work/my-repo/chartula.yaml and /work/my-repo/chartula.yml exist, and a run reads one configuration file. Keep one: move the settings you want into it and delete the other.
```

Keys are matched regardless of case.
An empty value (`model:`, `~` or `null`) leaves the setting at its default, as leaving the key out does.

[`chartula.example.yaml`](../chartula.example.yaml) lists every key, commented out; copy it to `chartula.yaml` and uncomment what you need.

## Settings as environment variables

Every setting can be given as an environment variable named `Chartula__<section>__<key>`, which overrides the file:

```console
$ export Chartula__Faithfulness__Thorough=false
$ export Chartula__Llm__Model=claude-opus-5
```

A list takes one variable per element, numbered from `0`, and a map takes one variable per name:

```console
$ export Chartula__Filter__ExcludeCategories__0=Internal
$ export Chartula__Filter__ExcludeCategories__1=Documentation
$ export Chartula__Categories__Names__Fix="Bug Fixes"
```

A variable overrides one element, not the whole list, so `__0` replaces the first element of a list from the file and leaves the others in place.

## Environment-only settings

Four settings decide where release data and credentials are sent: the two endpoints, and the names of the variables whose values go to them.
Chartula reads them from the environment only, and refuses a `chartula.yaml` that sets one, naming the variable to use instead.
The file is repository content that anyone whose pull request is merged can change, and one value in it could otherwise send any variable of your environment to any host.

| Variable | Default | Description |
| --- | --- | --- |
| `Chartula__Llm__BaseUrl` | per provider | The endpoint the model provider is reached at. See [Providers](providers.md). |
| `Chartula__Llm__ApiKeyEnvironmentVariable` | per provider | The name of the variable holding the API key. |
| `Chartula__GitHub__ApiBaseUrl` | `https://api.github.com/` | The GitHub REST API. See [GitHub Enterprise](github.md#github-enterprise). |
| `Chartula__GitHub__TokenEnvironmentVariable` | `GITHUB_TOKEN` | The name of the variable holding the GitHub token. |

Both endpoints must use `https`.
Plain `http` is accepted only for this machine (`localhost`, `127.0.0.1`, `::1`), where local model servers run; anywhere else it would send the key or token in cleartext, so the run is refused before its first request.

`Chartula__Llm__BaseUrl` is also refused when its host belongs to another provider's API: `api.openai.com` with `llm.provider: anthropic`, and `api.anthropic.com` with `openai-compatible`.
A proxy or a gateway lives at a host of its own, so any other host is accepted.
The usual way into this refusal is an endpoint set in the environment while `llm.provider` sits in a `chartula.yaml` the run does not read, so the message says when the provider was not set:

```console
Configuration error: Chartula__Llm__BaseUrl 'https://api.openai.com/v1' is OpenAI's API, but llm.provider is not set and defaults to 'anthropic', so the key in ANTHROPIC_API_KEY would be sent to OpenAI. For OpenAI's API, set llm.provider to openai-compatible (in chartula.yaml, or as Chartula__Llm__Provider); otherwise unset Chartula__Llm__BaseUrl.
```

Chartula does not load the rest of your environment: besides `Chartula__` settings it reads only `ANTHROPIC_API_KEY`, `OPENAI_API_KEY`, `GITHUB_TOKEN` and the variables the two name settings point to.
Every run prints the endpoints and variable names in force before its first request, never their values ([Providers](providers.md#what-each-provider-needs) shows the header).

## A file that cannot be read as meant

Chartula refuses a `chartula.yaml` before the run starts when any part of it would not be read as written, and names the file, the line and the column:

- YAML that does not parse - most often indentation: a key indented under a key that already has a value, a line that lines up with no key above it, or a tab.
- A key that is not listed below, including a key indented into the wrong section; the refusal names where it belongs, or the key it most likely is.
- A value of the wrong kind: boolean keys take only `true` or `false`, list keys a list such as `[Internal]`, map keys a map, and the others a single value.
- The same key twice, or a second YAML document after `---`.

Nothing in the file is ignored silently, because a setting that is not in force would otherwise read as one that is.
Every problem is named at once, one per line:

```console
$ chartula preview
Configuration error: chartula.yaml, line 3, column 3: 'faithfulness' is not a key of 'llm' but a section of its own. Check the indentation: 'faithfulness:' starts at the beginning of its line, like 'llm:'.
chartula.yaml, line 6, column 12: 'review.enabled' is 'yes'; expected true or false.
```

A value outside its valid values, such as an unknown category or `thinking: maximum`, stops the run with a configuration error before any GitHub request or model call.

## Sections

Values are matched regardless of case, and `provider`, `thinking` and `depth` also ignore `-` and `_`, so `openai_compatible` is `openai-compatible`.
The categories are `Feature`, `Fix`, `Performance`, `Documentation`, `Refactor`, `Internal` and `Other`.

### `llm`

The model provider and the model.
The endpoint and the name of the key variable are [environment-only](#environment-only-settings).

| Key | Default | Valid values | Description |
| --- | --- | --- | --- |
| `provider` | `anthropic` | `anthropic`, `openai-compatible` | The provider. [Providers](providers.md) sets up each. |
| `model` | per provider | a model id the provider serves | The model that writes the renderings. [Choosing a model](providers.md#choosing-a-model) lists the tested ids. |
| `maxOutputTokens` | `32000` | a positive whole number | The ceiling on the tokens the model may produce per call, thinking included. |
| `thinking` | `provider-default` | `provider-default`, `disabled`, `low`, `medium`, `high`, `xhigh` | How much the model reasons before it answers. See [`thinking`](providers.md#thinking). |

`thinking` also reads these aliases: `default` for `provider-default`; `off`, `false` and `none` for `disabled`; `adaptive`, `on` and `true` for `high`; `extrahigh` for `xhigh`.

Three defaults depend on the provider, because a default that is right for one is wrong for the other:

| Setting | `anthropic` | `openai-compatible` |
| --- | --- | --- |
| `model` | `claude-sonnet-5` | none, required |
| `Chartula__Llm__BaseUrl` | Anthropic's API | none, required |
| `Chartula__Llm__ApiKeyEnvironmentVariable` | `ANTHROPIC_API_KEY` | `OPENAI_API_KEY` |

The model produces its thinking before its text, so a `maxOutputTokens` that only fits the thinking leaves no text.
A ceiling that is too low cuts the text off mid-sentence rather than failing, so a rendering that ends abruptly is the sign to raise it.

### `github`

The section has no keys in `chartula.yaml`: both of its settings are [environment-only](#environment-only-settings).
[GitHub](github.md) covers the token, its permissions, the rate limit and GitHub Enterprise.

### `labels`

Rules on the labels of a pull request.
With no rules, labels are ignored.
Label names are yours, such as `visibility:internal`, `no-changelog` or `chore`, and are matched regardless of case.

| Key | Default | Valid values | Description |
| --- | --- | --- | --- |
| `exclude` | none | a list of label names | A pull request with one of these labels is dropped. |
| `category` | none | a map of label name to category | A pull request with one of these labels gets that category, whatever its title says. |
| `onlyIncludeLabeled` | `false` | `true`, `false` | When `true`, a change without labels is dropped. |
| `internal` | none | a list of label names | A change no reader can meet. It is kept out of the customer rendering. |
| `userFacing` | none | a list of label names | A change a reader can meet, whatever its category. |
| `actionRequired` | none | a list of label names | A change the reader has to act on although it is not breaking. It stands under "What needs action" in the customer rendering. |

[What goes into a release](what-goes-into-a-release.md) explains the order in which labels, category and filter decide, and which audience a change reaches.

### `filter`

| Key | Default | Valid values | Description |
| --- | --- | --- | --- |
| `excludeCategories` | `[Internal]` | a list of categories | Changes in these categories are dropped, unless they are breaking. A list replaces the default. An empty list, `[]`, drops no category, so internal work stays in. |

### `factBase`

| Key | Default | Valid values | Description |
| --- | --- | --- | --- |
| `depth` | `title-and-description` | `title-only`, `title-and-description`, `title-description-and-issues` | How much of each change the model reads. See [How much of each change the model reads](what-goes-into-a-release.md#8-how-much-of-each-change-the-model-reads). |

`depth` also reads these aliases: `title` for `title-only`, `description` for `title-and-description`, and `full` or `issues` for `title-description-and-issues`.

### `categories`

| Key | Default | Valid values | Description |
| --- | --- | --- | --- |
| `order` | `[Feature, Fix, Performance, Documentation, Refactor, Other, Internal]` | a list of categories | The order of entries within each group of the technical and customer renderings, and across the product rendering. Unlisted categories sort last. |
| `names` | the category names | a map of category to name | The name the model is given for a category in each fact, such as `Fix: Bug Fix`. The headings of the renderings do not change. |
| `breakingProminent` | `true` | `true`, `false` | Whether breaking changes stand first in the product rendering. The technical and customer renderings always put a breaking change first. |

### `faithfulness`

The two checks of every rendering against the facts.
The rule-based check always runs and has no settings.

| Key | Default | Valid values | Description |
| --- | --- | --- | --- |
| `thorough` | `true` | `true`, `false` | Whether the thorough check, a second model call per audience, runs. |
| `model` | `llm.model` | a model id the provider serves | The model the thorough check asks, at the same provider, endpoint and key as the rendering. |
| `thinking` | `llm.thinking` | the values of `llm.thinking` | How much the thorough check's model reasons. |

Rendering and checking are different jobs, one writing prose and one comparing a finished text with the facts, so they can run on different models.
The run metrics show each one's tokens and time on its own line, so the trade-off can be read from a run.
A combination the Claude model is known to reject is refused when the configuration is read, naming the key it came from (`faithfulness.thinking`, or `llm.thinking` when the check inherits it).
The run header names the check's model when it differs from the rendering's, and the [provenance](changelog-json.md#provenance) records it either way.

[Run metrics](run-metrics.md) shows how to decide whether the thorough check earns its tokens.

### `review`

Review mode would present each rendering for sign-off before it is written.
No interactive reviewer exists yet, so `enabled: true` is refused instead of approving every text unseen.

| Key | Default | Valid values | Description |
| --- | --- | --- | --- |
| `enabled` | `false` | `false` | Whether review mode is on. |

### `range`

A first tag, or a range with more commits than this, is confirmed twice before any GitHub request or model call, and a run without a terminal stops there unless it passes `--yes`.
[A large range is confirmed](what-goes-into-a-release.md#a-large-range-is-confirmed) shows the question.

| Key | Default | Valid values | Description |
| --- | --- | --- | --- |
| `confirmAboveCommits` | `200` | a whole number, `0` or more | A range with more commits than this is confirmed. `0` confirms every range. A first tag is confirmed whatever its size. |

So a release of a few dozen pull requests runs without a question, and a forgotten `--since` on a long history is caught before it costs.

## Example

A `chartula.yaml` that sets every key:

```yaml
llm:
  provider: anthropic
  model: claude-opus-5
  maxOutputTokens: 32000
  thinking: disabled

labels:
  exclude: [wontfix, duplicate]
  category:
    security: Fix
  onlyIncludeLabeled: false
  internal: [visibility:internal]
  userFacing: [visibility:user-facing]
  actionRequired: [needs-migration]

filter:
  excludeCategories: [Internal, Documentation]

factBase:
  depth: title-description-and-issues

categories:
  order: [Feature, Fix, Performance, Documentation, Refactor, Other, Internal]
  names:
    Fix: Bug Fix
  breakingProminent: true

faithfulness:
  thorough: true
  model: claude-sonnet-5
  thinking: disabled

review:
  enabled: false

range:
  confirmAboveCommits: 200
```
