# Troubleshooting

The situations a run most often stops in, each as the message you see, its cause, and the fix.
Search this page for the first words of your message.

## Start with `chartula doctor`

`chartula doctor` checks everything a run needs, before anything is spent, and prints the message a run would print for each problem:

```console
$ chartula doctor
chartula · doctor
Checkout  /work/my-repo

Repository
  ✓ ok    git           /usr/bin/git
  ✓ ok    checkout      /work/my-repo, with its full history
  ✓ ok    tag           v1.3.0, the nearest tag reachable from HEAD. Pass --tag to choose another.
  ✓ ok    repository    owner/name, from the 'origin' remote. Pass --repo to choose another.
  ✓ ok    config        no chartula.yaml in /work/my-repo, so the defaults apply
                        To change them, copy https://github.com/goldbarth/chartula/blob/v0.1.0-preview.4/chartula.example.yaml to chartula.yaml.

Model
  × fail  model         anthropic at its default endpoint, model claude-sonnet-5
                        No Anthropic API key found in ANTHROPIC_API_KEY. Set one with: export ANTHROPIC_API_KEY=<your key> (create one at https://console.anthropic.com/settings/keys). For an endpoint that needs no key, set llm.provider to openai-compatible.
  – skip  endpoint      not checked: needs the model settings and a key

GitHub
  ✓ ok    GitHub read   owner/name at https://api.github.com/, token from GITHUB_TOKEN
  ✓ ok    GitHub write  the token can publish release notes to owner/name, which generate needs
  ✓ ok    PR titles     all of the last 3 merged pull requests carry a Conventional Commits prefix

A run would stop. Fix the failed checks, then run chartula doctor again.
```

Fix the lines marked `fail` from the top, because a later check often fails only for an earlier one.
`doctor` finds most situations under [Setup](#setup) and [The range](#the-range) on this page, all but a spent rate limit and a first tag.
The situations under [Model calls](#model-calls) and [A slow run](#a-slow-run) show only once the model writes a whole release.
So run `doctor` first, and read on here when a run fails after it passed.
[`chartula doctor`](cli.md#chartula-doctor) describes each check and what it costs.

## Setup

### No model key

```text
Configuration error: No Anthropic API key found in ANTHROPIC_API_KEY. Set one with: export ANTHROPIC_API_KEY=<your key> (create one at https://console.anthropic.com/settings/keys). For an endpoint that needs no key, set llm.provider to openai-compatible.
```

**Cause:** `llm.provider` is `anthropic`, the default, and `ANTHROPIC_API_KEY` is empty.
`generate` refuses before it reads anything, because every [audience](glossary.md#audience) would fail on the missing key.

**Fix:** export the key, or set up another provider as [Providers](providers.md) shows.
A local server with `llm.provider: openai-compatible` needs no key.
`preview` makes no model call, so it runs without a key.

### GitHub cannot read the repository

```text
Error: GitHub cannot read owner/name (404 Not Found).
  The request carried the token from GITHUB_TOKEN.
  Likely causes:
  - --repo is misspelled
  - the token cannot read owner/name, or lacks the Pull requests (read) permission
```

**Cause:** GitHub answers `404` for a repository that does not exist and for one the token may not see, so it cannot tell the two apart.

**Fix:** check the owner and name in the message.
When they are right, give the token access to the repository with Contents and Pull requests read ([The token](github.md#the-token)).
So a private repository needs a token that was granted that repository, not only one that exists.

### GitHub rejects the token

```text
Error: GitHub rejected the token in GITHUB_TOKEN (401 Unauthorized): it is expired, revoked or mistyped.
```

**Cause:** the token is no longer valid, or the variable holds something that is not a token.

**Fix:** create a new token ([The token](github.md#the-token)) and export it again.

### The token cannot publish

```text
Error: GitHub does not let this run publish the release notes for v1.3.0 to owner/name (403 Forbidden).
  The request carried the token from GITHUB_TOKEN.
  GitHub says the token needs: contents=write
  Publishing needs a token with Contents read and write on owner/name.
  The run stopped before any model call. --no-publish writes the files without publishing them.
```

**Cause:** the token can read the repository, but not write its releases.
`generate` checks that before its first model call, so a token that cannot publish costs no tokens.

**Fix:** give the token Contents read and write on the repository.
Or run `generate --no-publish`, which writes the files and publishes nothing, and publish the notes yourself.
`doctor` shows this as `warn`, not `fail`, because `preview` and `--no-publish` still run.

### The rate limit is spent

```text
Error: GitHub's rate limit is spent (403 Forbidden). Wait until it resets and run again.
```

Without a token, the run warns at its start, and the message names the token as the remedy:

```text
Error: GitHub's rate limit is spent (403 Forbidden). Without a token GitHub allows 60 requests an hour; a token in GITHUB_TOKEN raises that to 5000.
```

**Cause:** a run makes one GitHub request per commit in the [range](glossary.md#range).
Without a token, GitHub allows 60 requests an hour; with one, 5,000.

**Fix:** set `GITHUB_TOKEN`, or wait for the limit to reset.
A release merged with merge commits has more commits than pull requests, so it spends more of the limit than the same release squashed.
[The rate limit](github.md#the-rate-limit) has the figures.

### A configuration error

```text
Configuration error: chartula.yaml, line 3, column 3: 'faithfulness' is not a key of 'llm' but a section of its own. Check the indentation: 'faithfulness:' starts at the beginning of its line, like 'llm:'.
```

**Cause:** `chartula.yaml` cannot be read as written: invalid YAML, an unknown or misplaced key, or a value of the wrong kind.
Chartula stops rather than guess what a line meant, so a typo never runs with a default you did not choose.

**Fix:** correct the file at the line and column the message names.
[A file that cannot be read as meant](configuration.md#a-file-that-cannot-be-read-as-meant) lists the checks and their messages.

## The range

### A shallow clone

```text
Error: The checkout is a shallow clone: its history ends at the fetch depth, not where 'v1.3.0' starts, so neither the previous tag nor the first commit can be read from it.
  Fetch the full history and tags: git fetch --unshallow --tags
  In GitHub Actions, check out with fetch-depth: 0; in GitLab CI, set GIT_DEPTH: 0.
```

**Cause:** the checkout holds only the last commits, so the range of the release cannot be read from it.
Most CI systems check out this way by default.

**Fix:** run the command in the message, or check out with the full history in CI ([CI](ci.md)).
[A shallow clone](what-goes-into-a-release.md#a-shallow-clone) explains why the run stops rather than reading what is there.

### A first tag

```text
Range:   every commit up to v0.1.0 (1 commit), the first tag
         It costs 1 GitHub request. A preview sends nothing to the model.
         To start later, pass --since <ref>: the commits after <ref>, up to v0.1.0.
         No terminal to confirm this. Pass --yes to confirm it up front.
Stopped: The range of v0.1.0 was not confirmed, so nothing was read from GitHub or sent to the model.
```

**Cause:** the tag is the repository's first, so the release reaches back to the first commit.
In a terminal the run asks before it reads that range; without one, as in CI, it stops.
A range with more commits than `range.confirmAboveCommits` is asked about the same way.

**Fix:** pass `--yes` to confirm the range, or `--since <ref>` to start the release later.
[A first tag](what-goes-into-a-release.md#a-first-tag) explains the range and the question.

## Model calls

`doctor` sends one short request to each model a run uses, so it finds a wrong key, model id or address.
The situations below show only once the model writes a whole release.

### A model call fails

A failed call names the provider, the address it asked, the model and the status, then what that status usually means.
Audiences that failed for the same reason say it once:

```text
Technical
  (failed) Changelog generation for 'v1.3.0' failed: openai-compatible at http://localhost:11434/v1/chat/completions answered 404 Not Found for model 'gpt-luna'.
           Either the endpoint does not serve that model id (check llm.model), or Chartula__Llm__BaseUrl is not the address of this provider's API - another provider's, or a wrong path such as a missing /v1.
           The endpoint said: ...

Customer
  (failed) The same as Technical.
```

The line after `The endpoint said:` is the endpoint's own message, so its wording depends on the provider.

| Status | What the message says | Fix |
| --- | --- | --- |
| `404 Not Found` | The endpoint does not serve that model id, or the address is not this provider's API. | Check `llm.model` against the ids the endpoint serves (`ollama list` on Ollama), and `Chartula__Llm__BaseUrl`, including its `/v1` ([Providers](providers.md)). |
| `401 Unauthorized`, no key set | `The endpoint needs a key, and OPENAI_API_KEY is not set.` | Export the key the provider issued. |
| `401 Unauthorized`, a key set | `The endpoint rejected the key in OPENAI_API_KEY: it is expired, revoked or mistyped, or it is another provider's key.` | Create a new key, and check that it belongs to the provider at that address. |
| `403 Forbidden` | The key may not make this request, for example because it has no access to the model. | Grant the key access to the model at the provider, or choose another model. |

An endpoint that is not reachable at all is named as configured, with the reason the connection failed:

```text
  (failed) Changelog generation for 'v1.3.0' failed: openai-compatible at http://localhost:11434/v1 could not be reached for model 'qwen3:8b': Connection refused (127.0.0.1:11434)
```

**Fix:** start the server, or correct the address.
The run summary shows the attempts as `Retries`, because the provider client sends a refused request again before it gives up.

An endpoint that answers, but in a form the provider's SDK cannot read, has a message of its own:

```text
  (failed) Changelog generation for 'v1.3.0' failed: openai-compatible at http://localhost:11434/v1/chat/completions answered 200 OK for model 'qwen3:8b', but the answer could not be read: Unknown ChatFinishReason value 'content_filter_v2'.
           The endpoint is reachable, but its answer is not in the form the provider's SDK expects.
```

**Cause:** the endpoint speaks a dialect close to the provider's API, but not the same one.
So the address is right, and the network is not the problem.

**Fix:** update the server, or use one that follows the provider's API more closely.

A failed call of the [thorough check](glossary.md#thorough-check) does not fail the audience.
The [rendering](glossary.md#rendering) is kept and flagged as `The thorough check could not be evaluated`, with the same explanation, and names `faithfulness.model` when the check asks a model of its own.

### The answer does not match the facts

```text
  (failed) Changelog generation for 'v1.3.0' failed: the model's answer does not match the facts it was sent: no entry for fact 1, 2.
```

**Cause:** the model was sent one [fact](glossary.md#fact) per entry and returned entries for other facts, or none for some.
Chartula fails the audience rather than publish a rendering in which a change is missing or invented.

**Fix:** run again, since a model does not fail the same way every time.
When it keeps failing, the model does not follow the task: choose another one ([Choosing a model](providers.md#choosing-a-model)).

### The answer was cut off

```text
  (failed) Changelog generation for 'v1.3.0' failed: the model's answer did not match the expected entry format
```

**Cause:** the answer is not in the requested format.
The usual reason is an answer cut off at `llm.maxOutputTokens`: the model thinks before it writes, and thinking counts against the same limit.
When the rephrasing `out` tokens in the run summary come close to `maxOutputTokens` per call, the limit cut the answer.
A model that does not hold to the format gives the same message.

**Fix:** raise `llm.maxOutputTokens`, or lower `llm.thinking` ([`llm`](configuration.md#llm)).
The default of 32,000 leaves the text as much room as the thinking.
When the `out` tokens stay far below the limit, choose another model instead.

### The context window is too small

```text
  (failed) Changelog generation for 'v1.3.0' failed: the endpoint reported 120 input tokens for a prompt of 6314 characters, which no tokenizer produces fewer than 789 tokens for - the endpoint's context window is too small for what Chartula sends. See "The context window is the first thing to get right" in docs/providers.md.
```

**Cause:** the endpoint cut the prompt to fit its context window and answered from what was left.
A cut rendering fails its audience, and a cut thorough check fails the run, so a changelog written from part of a release never passes for the whole of it.
Chartula can prove the cut only when the endpoint reports how many tokens it read.

**Fix:** raise the context window on the server, as [The context window is the first thing to get right](providers.md#the-context-window-is-the-first-thing-to-get-right) shows.
An endpoint that reports the uncut length whatever it processed, or no usage at all, leaves nothing to detect.

### A thorough check that verified nothing

```text
Flagged for review:
  ! The thorough check could not be evaluated: the response did not match the expected format.
```

The run summary says the same for the whole run:

```text
  Thorough check:   2 runs, 0 with findings, 0 claims, 673 in / 80 out, 0.0 s (longest 0.0 s)
    of which cached input not reported, reasoning not reported
    2 of 2 runs came back unreadable and verified nothing
```

**Cause:** the check's model answered in prose, or in a shape other than the verdict it was asked for.
Read it as a check that did not happen, not as a clean one: the tokens were spent, and nothing was verified.

**Fix:** check with a model that holds to the response format, with `faithfulness.model` ([Configuration](configuration.md#faithfulness)).
An endpoint that enforces the format by constrained decoding returns a well-formed verdict from any model, which is why a clean check there says less than it seems ([What to watch: the thorough check](providers.md#what-to-watch-the-thorough-check)).

## A slow run

A run shows each step while it works, on stderr.
In a terminal, the current step's time keeps counting next to a spinner while the model call runs, and a finished step keeps its line with its time:

```text
  · done Reading pull requests   3/3 commits      0 s
  · done Rendering technical                      4 s
  · done Rendering customer                       4 s
  · done Checking technical                       4 s
  ⠴      Checking customer                        4 s
```

So a step whose time still counts is working, and a run that is about to finish is not aborted and paid for again.
[While a run works](cli.md#while-a-run-works) shows the lines without a terminal, as in CI.

Which step takes long points to the cause:

- **`Reading pull requests`:** one GitHub request per commit, so a long range takes long. Narrow it with `--since`.
- **`Rendering`:** the model writes the whole release in one call per audience. A long release, a slow model, or `thinking` at a high level.
- **`Checking`:** the thorough check's model reads the facts and the rendering. A slow checker, which `faithfulness.model` can replace.

The [run summary](costs-and-checks.md#reading-the-run-summary) tells three causes of a slow step apart:

- **One slow call:** the `longest` time is most of the operation's time. A long release, or a model thinking at length.
- **Uniformly slow calls:** the `longest` time is close to the average. A slow model or endpoint.
- **Retried calls:** `Retries` above zero. The provider was overloaded, rate-limited the key, or a request timed out and was sent again.

Every provider client retries on its own, and Chartula counts the requests the transport actually sends, the same way for every provider.
So a retried call does not pass for one slow call.
`not observed` means the retries could not be counted, which is not the same as none: it appears only for a model client Chartula did not build itself.

This summary is from a real run on a local server, where one check call was retried and took most of the run:

```text
  Thorough check:   2 runs, 1 with findings, 3 claims, 22,790 in / 999 out, 10 min 18 s (longest 10 min 17 s)
    of which 10,220 in cached, reasoning not reported
    caught 3 claims the rule-based check missed, for 23,789 tokens in 2 calls
  Rephrasing:       2 calls, 21,863 in / 2,340 out, 49.7 s (longest 27.7 s)
    of which 207 in cached, reasoning not reported
  Total:            47,992 tokens in 11 min 17 s
  Retries:          1 (rephrasing 0, thorough check 1)
```

Here the checker was the slow part, so a faster `faithfulness.model` is the fix, not another rendering model.

## Still stuck

[Open an issue](https://github.com/goldbarth/chartula/issues/new/choose) with the output of `chartula --version` and `chartula doctor`, and the message the run printed.
`doctor` names variables, never their values, so its output is safe to paste.
