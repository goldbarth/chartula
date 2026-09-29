# Configuration

Chartula runs with sensible defaults and needs no configuration to work.
A `chartula.yaml` in the repository root refines that default behavior; it is never required.
Environment variables override the file, so anything here can be set with `Chartula__Section__Key` too.

## A file that cannot be read as meant

A `chartula.yaml` is refused before the run starts when any part of it would not be read as written, naming the file, the line and the column:

- YAML that does not parse - most often indentation: a key indented under a key that already has a value, a line that lines up with no key above it, or a tab.
- A key that is not listed below, including a key indented into the wrong section; the refusal names where it belongs, or the key it most likely is.
- A value of the wrong kind: `true`/`false` keys take only those, list keys a list such as `[Internal]`, the others a single value.
- The same key twice, or a second YAML document after `---`.

Nothing in the file is ignored silently: a setting that is not in force would otherwise read as one that is.
Every problem is named at once, one per line:

```console
$ chartula preview
Configuration error: chartula.yaml, line 3, column 3: 'faithfulness' is not a key of 'llm' but a section of its own. Check the indentation: 'faithfulness:' starts at the beginning of its line, like 'llm:'.
chartula.yaml, line 6, column 12: 'review.enabled' is 'yes'; expected true or false.
```

An empty value (`model:`, `~` or `null`) leaves the setting at its default, as leaving the key out does.
Keys are matched regardless of case.

## Environment-only settings

Four settings decide where release data and credentials are sent: the two endpoints, and the names of the variables whose values go to them.
They are read from the environment only, and a `chartula.yaml` that sets one is refused, naming the variable to use instead.
The file is repository content that anyone whose pull request is merged can change, and one value in it could otherwise send any variable of your environment to any host.

| Variable | Default | Description |
| --- | --- | --- |
| `Chartula__Llm__BaseUrl` | per provider | The endpoint the model provider is reached at. See [Providers](providers.md). |
| `Chartula__Llm__ApiKeyEnvironmentVariable` | per provider | Name of the environment variable holding the API key. |
| `Chartula__GitHub__ApiBaseUrl` | `https://api.github.com/` | REST API base URL (override for GitHub Enterprise). |
| `Chartula__GitHub__TokenEnvironmentVariable` | `GITHUB_TOKEN` | Name of the environment variable holding the API token. |

Both endpoints must use `https`.
Plain `http` is accepted only for this machine (`localhost`, `127.0.0.1`, `::1`), which is where local model servers run; anywhere else it would send the key or token in cleartext, so the run is refused before its first request.
A model server elsewhere on your network needs `https` too.

`Chartula__Llm__BaseUrl` is also refused when its host belongs to another provider's API: `api.openai.com` with `llm.provider: anthropic`, and `api.anthropic.com` with `openai-compatible`.
Any other host is accepted, because a proxy or a gateway in front of a provider lives at a host of its own; a host that serves another provider's API is never one, and the key sent there would have to be rotated.
The usual way into this is an endpoint set in the environment while `llm.provider` sits in a `chartula.yaml` that is not there, so the refusal says when the provider was not set:

```console
Configuration error: Chartula__Llm__BaseUrl 'https://api.openai.com/v1' is OpenAI's API, but llm.provider is not set and defaults to 'anthropic', so the key in ANTHROPIC_API_KEY would be sent to OpenAI. For OpenAI's API, set llm.provider to openai-compatible (in chartula.yaml, or as Chartula__Llm__Provider); otherwise unset Chartula__Llm__BaseUrl.
```

Chartula does not load the rest of your environment: besides `Chartula__` settings it reads only `ANTHROPIC_API_KEY`, `OPENAI_API_KEY`, `GITHUB_TOKEN` and the variables named by the two settings above.
Every run starts by printing the endpoints and credential variable names in force, never their values, and the model and thinking mode it asks for:

```console
Model:  anthropic at its default endpoint, key from ANTHROPIC_API_KEY
        claude-sonnet-5, thinking provider-default
GitHub: https://api.github.com/, token from GITHUB_TOKEN
```

A minimal starting point is shipped as [`chartula.example.yaml`](../chartula.example.yaml) - copy it to `chartula.yaml` and uncomment only what you need.

## Sections

### `llm`

The model provider and which model to use. The endpoint and the name of the key variable are [environment-only](#environment-only-settings).

| Key | Default | Description |
| --- | --- | --- |
| `provider` | `anthropic` | The LLM provider: `anthropic` or `openai-compatible`. [Providers](providers.md) sets up each. Any other value fails the run. |
| `model` | per provider | The model id passed to the provider. See [Choosing a model](providers.md#choosing-a-model). |
| `maxOutputTokens` | `32000` | Ceiling on the tokens the model may produce per call, thinking included. Thinking is produced first, so a ceiling that only fits it leaves no text. |
| `thinking` | `provider-default` | How much the model reasons before answering. One of `provider-default`, `disabled`, `low`, `medium`, `high`, `xhigh`, the same for every provider. See [`thinking`](providers.md#thinking). |

Three of those defaults depend on the provider, because a default that is right for one is wrong for the other:

| Key | `anthropic` | `openai-compatible` |
| --- | --- | --- |
| `model` | `claude-sonnet-5` | none - required |
| `Chartula__Llm__BaseUrl` | the Anthropic API | none - required |
| `Chartula__Llm__ApiKeyEnvironmentVariable` | `ANTHROPIC_API_KEY` | `OPENAI_API_KEY` |

Raise `maxOutputTokens` for releases whose changelog runs long.
A ceiling that is too low truncates the generated text mid-sentence rather than failing, so a run that ends abruptly is the signal to raise it.

### `github`

How the GitHub API is reached. Both of its settings, the API base URL and the name of the token variable, are [environment-only](#environment-only-settings), so the section has no keys of its own in `chartula.yaml`.

The token is optional and the run says at startup when it is missing, naming whichever variable it looked in.
It is worth setting all the same: unauthenticated GitHub allows 60 requests an hour per IP address, and the budget is shared with every other unauthenticated request from that address.
A run spends one request per commit in the range, not per pull request.
A pull request merged with a merge commit costs one request for the merge commit and one for each commit on its branch, so a release merged with merge commits costs more requests than the same release squashed.
When the budget runs out, the run stops partway through with a message that names the rate limit.
A token raises the limit to 5000 an hour.

### `labels`

Steer curation with GitHub labels. All optional; with no rules, labels are ignored.

| Key | Default | Description |
| --- | --- | --- |
| `exclude` | (none) | Labels that exclude a pull request from the changelog. |
| `category` | (none) | Map of label name to category, forcing that change's category. |
| `onlyIncludeLabeled` | `false` | When true, only labeled pull requests are included. |
| `internal` | (none) | Labels marking a change no reader can come into contact with. It is kept out of the customer rendering. |
| `userFacing` | (none) | Labels marking a change a reader can come into contact with, whatever its category. |
| `actionRequired` | (none) | Labels marking a change the reader has to act on although it is not breaking. It stands under "What needs action" in the customer rendering. A breaking change always does and needs no label. |

The label names are yours. `visibility:internal` is one convention; `internal`,
`no-changelog` and `chore` are others, which is why the names are configured here
rather than built into the tool.

**What decides whether a reader can meet a change.** A breaking change always can,
whatever its labels say. Otherwise a visibility label answers it, because whoever
wrote the pull request knew the change. With no such label the category decides, as
it always has: `Feature`, `Fix`, `Performance` and `Other` count as something a
reader can meet, and `Documentation`, `Refactor` and `Internal` do not.

That fallback is why labelling nothing costs nothing. A category says what kind of
change something is, not whether a reader can meet it - a feature can be entirely
internal, a serialisation format or an output file's layout - so a label raises the
ceiling where the category cannot, without being a condition for the tool to work.
A change carrying both an `internal` and a `userFacing` label is treated as
internal: the two together are a contradiction, and that reading cannot put an
internal change in front of a reader.

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
| `order` | `[Feature, Fix, Performance, Documentation, Refactor, Other, Internal]` | The order categories appear in. Unlisted categories sort last. In the technical rendering the group comes first - Changed, Added, Fixed - and this order applies within a group. |
| `names` | (enum names) | Map of category name to display name (e.g. `Fix: Bug Fixes`). |
| `breakingProminent` | `true` | Whether breaking changes float to the top, shown near the top. The technical rendering always puts a breaking change first in its group, whatever this says. |

Valid category names: `Feature`, `Fix`, `Performance`, `Documentation`, `Refactor`, `Internal`, `Other`.

### `faithfulness`

The faithfulness checks. The rule-based check always runs and is not configurable.

| Key | Default | Description |
| --- | --- | --- |
| `thorough` | `true` | Whether the thorough (second-pass LLM) check runs. |
| `model` | `llm.model` | The model the thorough check asks, at the same provider, endpoint and key as the rendering. |
| `thinking` | `llm.thinking` | How much the thorough check's model reasons, in the values of [`llm.thinking`](providers.md#thinking). |

Rendering and checking are different jobs: one writes prose from the facts, the other compares a finished text with them and answers a short verdict.
They need not run on the same model.
A cheaper model can render and a stronger one check, or the reverse, and the run metrics show each one's tokens and time on its own line, so the trade-off can be read from a run.
Thinking is set apart for the same reason: on the check it has been measured to cost thousands of output tokens and find fewer claims, not more (see [`thinking`](providers.md#thinking)).

A combination the Claude model is known to reject is refused when the configuration is read, naming the key it came from (`faithfulness.thinking`, or `llm.thinking` when the check inherits it).
The run header names the check's model when it differs from the rendering's, and the [provenance](changelog-json.md#provenance) records it either way.

On by default, because it is cheap for what it finds.
On the three alpha runs in the README (`claude-sonnet-5`, technical and customer), it took 29%, 29% and 45% of the tokens of runs that cost about $0.06, $0.07 and $0.20 in total.
Its tokens are reported apart from rephrasing, so a run without it costs what is left: a few cents less.
On one of those runs it caught the only invented claim the rule-based check missed - the example in the README.

Every run reports what each check caught and what it cost - see [`run-metrics.md`](run-metrics.md) for deciding whether the thorough check earns its tokens.

### `review`

Review mode - present generated texts for human sign-off before writing.
Not available yet: no interactive reviewer exists, so `enabled: true` is refused with a configuration error instead of approving every text unseen.

| Key | Default | Description |
| --- | --- | --- |
| `enabled` | `false` | Whether review mode is on. Only `false` is accepted for now. |

## Example

```yaml
llm:
  model: claude-opus-4-8

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
    Fix: Bug Fixes
  breakingProminent: true

faithfulness:
  thorough: true

review:
  enabled: false
```
