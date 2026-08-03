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
| `provider` | `anthropic` | The LLM provider: `anthropic` or `openai-compatible`. Any other value fails the run. |
| `model` | per provider | The model id passed to the provider. See [Choosing a model](#choosing-a-model). |
| `baseUrl` | per provider | The endpoint the provider is reached at. See [Running against your own endpoint](#running-against-your-own-endpoint). |
| `apiKeyEnvironmentVariable` | per provider | Name of the environment variable holding the API key. |
| `maxOutputTokens` | `16000` | Ceiling on the tokens the model may produce per call. |
| `thinking` | `provider-default` | Whether the model reasons before answering. One of `provider-default`, `disabled`, `adaptive`. `anthropic` only. |

Three of those defaults depend on the provider, because a default that is right for one is wrong for the other:

| Key | `anthropic` | `openai-compatible` |
| --- | --- | --- |
| `model` | `claude-opus-4-8` | none - required |
| `baseUrl` | the Anthropic API | none - required |
| `apiKeyEnvironmentVariable` | `ANTHROPIC_API_KEY` | `OPENAI_API_KEY` |

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

| Model | Thinks by default | Cost as it comes | With `thinking: disabled` |
| --- | --- | --- | --- |
| `claude-haiku-4-5` | no | $0.12 | - |
| `claude-sonnet-5` | **yes** | $0.55 | **$0.44** |
| `claude-opus-4-8` | no | $0.89 | - |
| `claude-opus-5` | **yes** | $1.34 | **$1.03** |

Opus 5 and Opus 4.8 cost the same per token, and the run still costs 50% more on Opus 5. Turning thinking off closes most of that gap.

Setting it explicitly is also what makes the models comparable at all: read the middle column and Sonnet 5 looks like it sits between Haiku and Opus 4.8, when in fact it undercuts Opus 4.8 by half once both are measured the same way.

The thorough check shows the mechanism most clearly, because its answer is a short JSON verdict whose length tracks how many claims it found:

| Run | Thorough-check output | Claims found |
| --- | --- | --- |
| `claude-opus-4-8` | 69 tokens | 0 |
| `claude-sonnet-5`, thinking off | 237 tokens | 2 |
| `claude-haiku-4-5` | 238 tokens | 7 |
| `claude-opus-5`, thinking off | 377 tokens | 4 |
| `claude-opus-5`, thinking on | 4,967 tokens | 0 |
| `claude-sonnet-5`, thinking on | 6,982 tokens | 0 |

Everything in the top half scales with what was found. The two thinking runs spent thousands of tokens to report nothing - and the same models with thinking off reported two and four claims on the same text.

Not every value works on every model, and a rejected value fails the run rather than falling back:

- `adaptive` needs Claude 4.6 or newer. Haiku 4.5 has no adaptive mode and rejects it.
- `disabled` is fine on the models above, but Claude Fable 5 always thinks and rejects an explicit off - leave `provider-default` there.

Set `disabled` or `adaptive` to make the behavior the same on every model rather than a property of the one you picked. On the evidence so far, thinking costs 20-25% more and found fewer claims, not more - but that is one release, measured once, so treat it as a reason to set the value deliberately rather than as a settled answer. Read the run metrics after you change it.

`thinking` is an Anthropic setting.
It travels in a request field that has no equivalent in the OpenAI dialect, so setting anything but `provider-default` together with `provider: openai-compatible` fails the run instead of being quietly dropped.

#### Running against your own endpoint

`provider: openai-compatible` reaches anything that speaks the OpenAI chat-completions dialect at the URL you give it.
That is one setting for two quite different situations: hosted endpoints that are cheaper than a first-party API, and a server on your own machine, where the release data never leaves it.
Ollama, LM Studio, llama.cpp and vLLM all serve that dialect, so they need no adapter of their own.

Neither `model` nor `baseUrl` has a default here, and both failures say so by name.
The model cannot be guessed because the ids an endpoint serves are its own - ask it with `ollama list` or `GET /v1/models`.
The endpoint is deliberately left without a default rather than pointed at a well-known hosted one: this provider exists so release data can stay on your machine, and a default would send it off the machine for anyone who set only the model.

A local setup end to end, with nothing else configured:

```yaml
llm:
  provider: openai-compatible
  model: qwen3:8b
  baseUrl: http://localhost:11434/v1
```

```console
$ ollama serve &
$ ollama pull qwen3:8b
$ chartula preview --tag v1.2.0 --repo owner/name
```

No API key is involved.
Local servers do not read the `Authorization` header, so `apiKeyEnvironmentVariable` can be left unset and the run starts without one.

A hosted endpoint differs in two lines, and does need a key:

```yaml
llm:
  provider: openai-compatible
  model: llama-3.3-70b-versatile
  baseUrl: https://api.groq.com/openai/v1
  apiKeyEnvironmentVariable: GROQ_API_KEY
```

If the key is missing or wrong, the endpoint answers `401` and the run fails there.
Chartula does not check the key itself, because whether one is needed is the endpoint's business, not Chartula's.

#### The context window is the first thing to get right

Chartula sends the whole fact base in one call.
For a release of this repository's size that is around 12,000 tokens, and it grows with the release.

A local server does not refuse a prompt that is too long for its context window.
It cuts it and answers from what is left.
Ollama's default is 4,096 tokens, so most of the prompt is discarded before the model ever sees it - and because the instruction sits at the front while the truncation keeps the end, what is lost first is the task itself.
The model then receives material with no idea what to do with it, and writes something plausible.

Raise it on the server, not in `chartula.yaml` - the OpenAI dialect has no field for it, so Chartula cannot send it:

```console
$ OLLAMA_CONTEXT_LENGTH=24576 ollama serve
```

Or pin it to a model, which survives a server restart:

```console
$ printf 'FROM qwen2.5:14b\nPARAMETER num_ctx 24576\n' > Modelfile
$ ollama create my-changelog-model -f Modelfile
```

Whether your endpoint truncated is visible in the run metrics: an input-token count that is identical across runs, or that sits exactly on a power of two, is the context limit rather than your prompt.

**What to watch: the thorough check.**
Two different things can go wrong, and they do not look alike.

The obvious one is an endpoint that ignores the JSON schema and answers in prose.
An unreadable verdict is reported as *not evaluated*, never as a clean check, so the run says the text went unverified rather than implying it passed.

The quiet one is a verdict that reads perfectly and means nothing.
Endpoints that enforce the schema by constrained decoding - Ollama does - will produce a well-formed answer from any model, whether or not it understood the task.
`0 claims` from such a model looks exactly like a clean check.
Read it together with the rule-based check: if that one is finding claims and the thorough check is not, the thorough check is not earning its tokens.

Measured on this repository's `v0.1.0`, on 2026-08-03, with `qwen2.5:14b` at a 24,576-token context:

| Check | Claims found | Tokens |
| --- | --- | --- |
| Rule-based | 34 | none |
| Thorough | 1 | 39,310 |

That is one release on one model, measured once - treat it as a reason to read your own run metrics, not as a verdict on local models.
What it does show is which way to look first: the free check was the more useful one here, and `faithfulness.thorough: false` is a reasonable starting point on a local setup until your own numbers say otherwise.

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
