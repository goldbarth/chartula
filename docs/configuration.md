# Configuration

Chartula runs with sensible defaults and needs no configuration to work.
A `chartula.yaml` in the repository root refines that default behavior; it is never required.
Environment variables override the file, so anything here can be set with `Chartula__Section__Key` too.

## Environment-only settings

Four settings decide where release data and credentials are sent: the two endpoints, and the names of the variables whose values go to them.
They are read from the environment only, and a `chartula.yaml` that sets one is refused, naming the variable to use instead.
The file is repository content that anyone whose pull request is merged can change, and one value in it could otherwise send any variable of your environment to any host.

| Variable | Default | Description |
| --- | --- | --- |
| `Chartula__Llm__BaseUrl` | per provider | The endpoint the model provider is reached at. See [Running against your own endpoint](#running-against-your-own-endpoint). |
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
| `provider` | `anthropic` | The LLM provider: `anthropic` or `openai-compatible` (**experimental**, see below). Any other value fails the run. |
| `model` | per provider | The model id passed to the provider. See [Choosing a model](#choosing-a-model). |
| `maxOutputTokens` | `32000` | Ceiling on the tokens the model may produce per call, thinking included. Thinking is produced first, so a ceiling that only fits it leaves no text. |
| `thinking` | `provider-default` | How much the model reasons before answering. One of `provider-default`, `disabled`, `low`, `medium`, `high`, `xhigh`, the same for every provider. |

Three of those defaults depend on the provider, because a default that is right for one is wrong for the other:

| Key | `anthropic` | `openai-compatible` |
| --- | --- | --- |
| `model` | `claude-sonnet-5` | none - required |
| `Chartula__Llm__BaseUrl` | the Anthropic API | none - required |
| `Chartula__Llm__ApiKeyEnvironmentVariable` | `ANTHROPIC_API_KEY` | `OPENAI_API_KEY` |

Raise `maxOutputTokens` for releases whose changelog runs long.
A ceiling that is too low truncates the generated text mid-sentence rather than failing, so a run that ends abruptly is the signal to raise it.

#### Choosing a model

`model` is a free-text id passed straight to the provider, but not every id is worth using.
These are the ones Chartula is built for.

| Model id | Input / output per MTok | Context | Max output | Notes |
| --- | --- | --- | --- | --- |
| `claude-opus-5` | $5 / $25 | 1M | 128K | The current top tier. |
| `claude-opus-4-8` | $5 / $25 | 1M | 128K | |
| `claude-sonnet-5` | $2 / $10 | 1M | 128K | The default. |
| `claude-haiku-4-5` | $1 / $5 | 200K | 64K | The cheapest. Keep `maxOutputTokens` at or below 64000. |

Prices are Anthropic's first-party rates as of 2026-07-31 and change over time; treat the table as a starting point, not a quote.
Model ids are complete as written - do not append a date suffix.

All four support the structured output the thorough faithfulness check needs, so a cheaper model does not cost you that check.
What it can cost you is changelog quality, which is the whole product - so read the output of a cheaper run before adopting it, rather than assuming the saving is free.

The reason to care: every audience is one rephrasing call and one thorough-check call, and prompt iteration means running them repeatedly.
Haiku 4.5's list prices are a fifth of Opus's, which is the difference between iterating freely and rationing runs.

Measured on this repository's `0.1.0-preview.1`, on 2026-09-17, with `claude-sonnet-5`, all three audiences, the thorough check on, and 48 changes:

| `llm.thinking` | Rephrasing | Thorough check | Total tokens | Cost |
| --- | --- | --- | --- | --- |
| `disabled` | 41,959 in / 3,611 out | 50,325 in / 2,790 out | 98,685 | $0.25 |
| `provider-default` (thinks) | 41,959 in / 6,476 out | 49,003 in / 4,149 out | 101,587 | $0.29 |

These are single runs taken during development, not settled figures, and work with cheaper models has barely started.
Rephrasing and the thorough check each ran once per audience, so every figure is the sum of three calls, and a run for a single audience costs roughly a third.

Haiku 4.5's 200K context is the one hard limit in the table.
A release with many changes at `factBase.depth: title-and-description` produces a long fact list, and that list is sent once per audience plus once per thorough check.
If a run fails on context rather than on quality, that is the signal to move up a tier rather than to trim the facts.

#### `thinking`

Thinking is reasoning the model does before it answers. You never see it in the changelog, and it is billed as output tokens.

One value means the same on every provider. Chartula sends it in the provider-neutral form, and each provider's adapter translates it:

| `thinking` | Anthropic | OpenAI and compatible endpoints |
| --- | --- | --- |
| `provider-default` | nothing sent | nothing sent |
| `disabled` | thinking disabled | `reasoning_effort: none` |
| `low`, `medium`, `high`, `xhigh` | adaptive thinking at that effort | `reasoning_effort` at that level |

`adaptive` and `on` are read as `high`: adaptive thinking without an effort is high effort, so a file written before the levels existed asks for what it asked for.

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

- An effort level needs Claude 4.6 or newer. Haiku 4.5 has no adaptive mode and rejects it.
- `xhigh` needs Claude Opus 4.7 or newer; the 4.6 models reject it.
- `disabled` is fine on the models above, but Claude Fable 5 always thinks and rejects an explicit off - leave `provider-default` there.

These are refused when the configuration is read, before anything is fetched.
A model id Chartula cannot read, such as a gateway alias, is passed through, and the API rejects a bad combination on the first request instead.
The same goes for every OpenAI-compatible endpoint: its model ids say nothing reliable about what they accept, so a model that does not reason, or does not know a level, answers the first request with an error rather than being refused in advance.

Set a value explicitly to make the behavior the same on every model rather than a property of the one you picked. On the evidence so far, thinking costs 20-25% more and found fewer claims, not more - but that is one release, measured once, so treat it as a reason to set the value deliberately rather than as a settled answer. Read the run metrics after you change it.


#### Running against your own endpoint

**Experimental.** This provider is exercised end to end far less than `anthropic` and its interaction with the faithfulness checks is the newest part of Chartula - read [The context window is the first thing to get right](#the-context-window-is-the-first-thing-to-get-right) before relying on it for a real release.

`provider: openai-compatible` reaches anything that speaks the OpenAI chat-completions dialect at the URL you give it.
That is one setting for two quite different situations: hosted endpoints that are cheaper than a first-party API, and a server on your own machine, where the release data never leaves it.
Ollama, LM Studio, llama.cpp and vLLM all serve that dialect, so they need no adapter of their own.

Neither `model` nor the endpoint has a default here, and both failures say so by name.
The model cannot be guessed because the ids an endpoint serves are its own - ask it with `ollama list` or `GET /v1/models`.
The endpoint is deliberately left without a default rather than pointed at a well-known hosted one: this provider exists so release data can stay on your machine, and a default would send it off the machine for anyone who set only the model.

A local setup end to end, with nothing else configured:

```yaml
llm:
  provider: openai-compatible
  model: qwen3:8b
```

```console
$ export Chartula__Llm__BaseUrl=http://localhost:11434/v1
$ ollama serve &
$ ollama pull qwen3:8b
$ chartula preview
```

No API key is involved.
Local servers do not read the `Authorization` header, so `Chartula__Llm__ApiKeyEnvironmentVariable` can be left unset and the run starts without one.

A hosted endpoint differs in the model and two variables, and does need a key:

```yaml
llm:
  provider: openai-compatible
  model: llama-3.3-70b-versatile
```

```console
$ export Chartula__Llm__BaseUrl=https://api.groq.com/openai/v1
$ export Chartula__Llm__ApiKeyEnvironmentVariable=GROQ_API_KEY
$ export GROQ_API_KEY=<your key>
```

If the key is missing or wrong, the endpoint answers `401` and the run fails there, naming the variable it read the key from (see [When a model call fails](cli.md#when-a-model-call-fails)).
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

The quiet one used to be a verdict that read perfectly and meant nothing: endpoints that enforce the schema by constrained decoding - Ollama does - produce a well-formed answer from any model, whether or not it understood the task, and `0 claims` from such a model looked exactly like a clean check.
Chartula now catches this when the endpoint reports usage: the prompt's character count bounds the token count from below, so a reported `prompt_tokens` far under that bound fails the run outright rather than returning a verdict.
See [When the prompt never arrived](run-metrics.md#when-the-prompt-never-arrived).
An endpoint that reports the untruncated length regardless of what it actually processed still gives nothing to detect this way.

Measured on this repository's `v0.1.0`, on 2026-08-03, with `qwen2.5:14b` at a 24,576-token context:

| Check | Claims found | Tokens |
| --- | --- | --- |
| Rule-based | 34 | none |
| Thorough | 1 | 39,310 |

That is one release on one model, measured once - treat it as a reason to read your own run metrics, not as a verdict on local models.
What it does show is which way to look first: the free check was the more useful one here, and `faithfulness.thorough: false` is a reasonable starting point on a local setup until your own numbers say otherwise.

### `github`

How the GitHub API is reached. Both of its settings, the API base URL and the name of the token variable, are [environment-only](#environment-only-settings), so the section has no keys of its own in `chartula.yaml`.

The token is optional and the run says at startup when it is missing, naming whichever variable it looked in.
It is worth setting all the same: unauthenticated GitHub allows 60 requests an hour per IP address, a run spends roughly one per pull request, and the budget is shared with every other unauthenticated request from that address.
When it runs out the run fails partway through with a 403 that names a commit rather than the cause.
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
| `thinking` | `llm.thinking` | How much the thorough check's model reasons, in the values of [`llm.thinking`](#thinking). |

Rendering and checking are different jobs: one writes prose from the facts, the other compares a finished text with them and answers a short verdict.
They need not run on the same model.
A cheaper model can render and a stronger one check, or the reverse, and the run metrics show each one's tokens and time on its own line, so the trade-off can be read from a run.
Thinking is set apart for the same reason: on the check it has been measured to cost thousands of output tokens and find fewer claims, not more (see [`thinking`](#thinking)).

A combination the Claude model is known to reject is refused when the configuration is read, naming the key it came from (`faithfulness.thinking`, or `llm.thinking` when the check inherits it).
The run header names the check's model when it differs from the rendering's, and the [provenance](changelog-json.md#provenance) records it either way.

On by default, because it is cheap for what it finds.
On the three alpha runs in the README (`claude-sonnet-5`, technical and customer), it took 29%, 29% and 45% of the tokens of runs that cost about $0.06, $0.07 and $0.20 in total.
Its tokens are reported apart from rephrasing, so a run without it costs what is left: a few cents less.
On one of those runs it caught the only invented claim the rule-based check missed - the example in the README.

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
