# Providers

Chartula establishes the [facts](glossary.md#fact) of a release without a model, then sends them to a model twice per [audience](glossary.md#audience): once to rephrase them, once to check the text against them.
This page sets up that model: Anthropic, OpenAI, a hosted endpoint that speaks OpenAI's dialect, or a server on your own machine.

Two values of `llm.provider` cover all four.
`anthropic`, the default, talks to Anthropic's API.
`openai-compatible` talks to anything that serves the OpenAI chat-completions dialect at a URL you give it: OpenAI itself, a hosted alternative, or Ollama, LM Studio, llama.cpp and vLLM on your machine.

## What each provider needs

| | `anthropic` | `openai-compatible` |
| --- | --- | --- |
| `llm.model` in `chartula.yaml` | optional, default `claude-sonnet-5` | required |
| `Chartula__Llm__BaseUrl` in the environment | optional, default Anthropic's API | required |
| Key variable | `ANTHROPIC_API_KEY` | `OPENAI_API_KEY`, when the endpoint needs a key |

`Chartula__Llm__ApiKeyEnvironmentVariable` names another variable to read the key from, such as `GROQ_API_KEY`.
The endpoint and the name of the key variable come from the environment only, never from `chartula.yaml`, because they decide where release data and credentials go ([Environment-only settings](configuration.md#environment-only-settings)).

`openai-compatible` has no default model or endpoint, not even OpenAI's.
The provider exists so release data can stay on your machine, and a default endpoint would send it off the machine for anyone who set only the model.
A run that lacks either stops with a message that names the missing setting.

An `anthropic` run without a key stops before it reads anything, naming the variable.
An `openai-compatible` run starts without one, because a local server needs none; a hosted endpoint that does need one answers the first call with `401`, and the run names the variable it read the key from (see [A model call fails](troubleshooting.md#a-model-call-fails)).

Every run prints what is in effect to stderr before its first request, so a setting inherited from a shell profile or a CI runner shows before it costs anything:

```text
Model:  anthropic at its default endpoint, key from ANTHROPIC_API_KEY
        claude-sonnet-5, thinking provider-default
GitHub: https://api.github.com/, token from GITHUB_TOKEN
```

The header names the variable a key is read from, not whether it is set.

## Anthropic

Nothing goes into `chartula.yaml` for the default model.
To pick another one:

```yaml
llm:
  model: claude-opus-5
```

```console
$ export ANTHROPIC_API_KEY=<your key>
$ chartula preview
Model:  anthropic at its default endpoint, key from ANTHROPIC_API_KEY
        claude-opus-5, thinking provider-default
```

Create a key at [console.anthropic.com](https://console.anthropic.com/settings/keys).
For a proxy or a gateway in front of Anthropic's API, set its URL in `Chartula__Llm__BaseUrl`.

## OpenAI

```yaml
llm:
  provider: openai-compatible
  model: gpt-6-sol
```

```console
$ export Chartula__Llm__BaseUrl=https://api.openai.com/v1
$ export OPENAI_API_KEY=<your key>
$ chartula preview
Model:  openai-compatible at https://api.openai.com/v1, key from OPENAI_API_KEY
        gpt-6-sol, thinking provider-default
```

Create a key at [platform.openai.com](https://platform.openai.com/api-keys).

## A hosted compatible endpoint

A hosted endpoint differs from OpenAI in the URL, the model id and usually the name of the key variable.
With Groq, for example:

```yaml
llm:
  provider: openai-compatible
  model: llama-3.3-70b-versatile
```

```console
$ export Chartula__Llm__BaseUrl=https://api.groq.com/openai/v1
$ export Chartula__Llm__ApiKeyEnvironmentVariable=GROQ_API_KEY
$ export GROQ_API_KEY=<your key>
$ chartula preview
Model:  openai-compatible at https://api.groq.com/openai/v1, key from GROQ_API_KEY
        llama-3.3-70b-versatile, thinking provider-default
```

The model ids an endpoint serves are its own, so take them from its documentation or from `GET /v1/models`.
This setup is not part of the [model comparison](#choosing-a-model); read the output of its first runs closely.

Watch the [thorough check](#what-to-watch-the-thorough-check) on an endpoint you have not used before: some accept the request and ignore the response format it asks for.

## A local server

On your own machine the release data never leaves it, and no key is involved.
With Ollama:

```yaml
llm:
  provider: openai-compatible
  model: <model>
```

```console
$ export Chartula__Llm__BaseUrl=http://localhost:11434/v1
$ OLLAMA_CONTEXT_LENGTH=24576 ollama serve &
$ ollama pull <model>
$ chartula preview
Model:  openai-compatible at http://localhost:11434/v1, key from OPENAI_API_KEY
        <model>, thinking provider-default
```

`ollama list` shows the ids of the models you have pulled.
LM Studio serves the same dialect at `http://localhost:1234/v1`.
The header names `OPENAI_API_KEY` although no key is set, because a local server does not read the `Authorization` header and the run needs none.

No local model has passed the [model comparison](costs-and-checks.md#local-models) yet: `qwen3:14b` failed there as the rephrasing model and as the checker, and `granite4.1-guardian:8b` as the checker.
Until a local model passes, read a local run's output line by line before you publish anything from it.

### The context window is the first thing to get right

Chartula sends the whole [fact base](glossary.md#fact-base) in one call.
For a release of 18 changes with their descriptions, one call was around 11,000 tokens in the [comparison of 2026-09-25](costs-and-checks.md#the-model-comparison-of-2026-09-25), and it grows with the release.

A local server does not refuse a prompt that is too long for its context window.
It cuts the prompt and answers from what is left.
Ollama's default window is 4,096 tokens, so it discards most of the prompt before the model sees it.
The instructions sit at the front and the cut keeps the end, so the model loses the task first and writes something plausible from material it has no instructions for.

Raise the window on the server, not in `chartula.yaml`, because the OpenAI dialect has no field for it:

```console
$ OLLAMA_CONTEXT_LENGTH=24576 ollama serve
```

Or pin it to a model, which survives a server restart:

```console
$ printf 'FROM <model>\nPARAMETER num_ctx 24576\n' > Modelfile
$ ollama create my-changelog-model -f Modelfile
```

Chartula catches a cut prompt when the endpoint reports how many tokens it read.
A prompt's length in characters sets a lower bound on its token count, and a reported count far below that bound fails the call with a message that points here.
An endpoint that reports the uncut length whatever it processed leaves nothing to catch.
In the [run summary](costs-and-checks.md#reading-the-run-summary), an input-token count that is identical across runs, or that sits exactly on a power of two, is the window's limit rather than your prompt, and the sign to raise it.

### What to watch: the thorough check

The [thorough check](glossary.md#thorough-check) asks the model for a verdict in a fixed JSON format, and two things can go wrong.

An endpoint that ignores the format answers in prose.
Chartula reports that verdict as not evaluated, never as a clean check, so the run says the text went unverified.

An endpoint that enforces the format by constrained decoding, as Ollama does, returns a well-formed verdict from any model, whether or not it understood the task.
A verdict of `0 claims` from such a model looks exactly like a clean check.
On such an endpoint, a clean check says less than it seems.

## Choosing a model

`llm.model` is passed to the provider as written, so any id the provider serves works.
These are the ids Chartula has been run with:

| Model id | Provider | Compared | Notes |
| --- | --- | --- | --- |
| `gpt-6-sol` | OpenAI | 2026-09-25 | The steadier checker. |
| `gpt-6-luna` | OpenAI | 2026-09-25 | The cheapest to rephrase with. |
| `claude-sonnet-5` | Anthropic | pending | The default. |
| `claude-opus-5` | Anthropic | pending | The top tier. |
| `claude-opus-4-8` | Anthropic | pending | |
| `claude-haiku-4-5` | Anthropic | pending | A 200K context and 64K output: keep `maxOutputTokens` at or below 64000. |

Anthropic's model ids are complete as written, so do not append a date suffix.
Every model in the table supports the structured output the thorough check needs.
Prices change, so look them up at the source: [Anthropic](https://www.anthropic.com/pricing#api), [OpenAI](https://openai.com/api/pricing/).

The [model comparison of 2026-09-25](costs-and-checks.md#the-model-comparison-of-2026-09-25) has the figures behind the notes, what they cannot tell, and the recommendations drawn from them.
In short, `gpt-6-luna` rendered at the lowest cost, `gpt-6-sol` was the steadier checker, and thinking lowered no [flag](glossary.md#flag) while it raised cost and duration.
Read the output of a cheaper setup before you adopt it, because changelog quality is what the saving could cost.
The comparison runs for the Claude models follow.

## `thinking`

Thinking is reasoning a model does before it answers.
It never appears in the changelog, and the provider bills it as output tokens.

`llm.thinking` means the same on every provider, and Chartula translates it for each:

| `thinking` | Anthropic | OpenAI and compatible endpoints |
| --- | --- | --- |
| `provider-default` | nothing sent | nothing sent |
| `disabled` | thinking disabled | `reasoning_effort: none` |
| `low`, `medium`, `high`, `xhigh` | adaptive thinking at that effort | `reasoning_effort` at that level |

`adaptive` and `on` are read as `high`, because adaptive thinking without an effort is high effort.

The default, `provider-default`, sends nothing and leaves each model on its own behavior.
Models behave differently there: some think by default and some do not, so the same setting costs a different amount on each.
Set a value explicitly to make runs on different models comparable, for example:

```yaml
llm:
  thinking: disabled
```

Not every value works on every model, and a rejected value stops the run instead of falling back:

- An effort level needs Claude 4.6 or newer. Haiku 4.5 has no adaptive mode and rejects it.
- `xhigh` needs Claude Opus 4.7 or newer; the 4.6 models reject it.
- Claude Fable 5 always thinks and rejects `disabled`; leave `provider-default` there.

Chartula refuses these when it reads the configuration, before it fetches anything.
A model id it cannot read, such as a gateway alias, is passed through, and the API rejects a bad combination on the first request instead.
The same goes for every OpenAI-compatible endpoint, because its model ids say nothing reliable about what they accept.
