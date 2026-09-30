# The `chartula` CLI

Chartula is controlled entirely through the `chartula` command.
There is no daemon, no interactive shell and no menu - every run is one command that reads pull requests and produces output, then exits.

This page lists every command and option the CLI accepts today.
`chartula --help` prints a short version of the same thing from the binary itself.

## Command overview

```console
$ chartula --help
Chartula - multi-audience, grounded changelog generator.

Usage:
  chartula preview  [options]   Show what would be produced (dry run).
  chartula generate [options]   Produce and write the outputs.

Run it from a checkout of the repository the release belongs to.

Options:
  --tag <tag>    The release tag. Default: the nearest tag reachable from HEAD.
  --repo <o/n>   The GitHub repository, as owner/name. Default: read from
                 the 'origin' remote.
  --since <ref>  Render the commits after this tag or commit, up to the
                 release tag. Default: after the previous tag, or from the
                 first commit for a first tag.
  --yes          Confirm a first tag or a large range up front, for a run
                 without a terminal to ask on.
  --no-publish   Write every file, but publish no GitHub release notes.
  --audience <a> Render only this audience: technical, customer or product.
                 Repeat it, or separate them with commas. Default:
                 technical and customer; product renders only when named.
                 An output whose audience was not rendered is not written.
```

`-h`, `--help` and `help` all print this text, and so does running `chartula` with no arguments at all.
An unrecognized first word is not silently ignored - it prints `Unknown command '<word>'.`, the same usage text, and exits with status 1.

Two commands exist: `preview` and `generate`.
Both run the identical pipeline against the same facts; they differ only in what happens at the very end.

## `chartula preview`

```console
$ chartula preview [--tag <release-tag>] [--repo <owner/name>]
```

Runs the full pipeline - curation, filtering, labeling, categorization, then one rephrasing and one faithfulness check per audience - and prints the result to stdout.
Nothing is written to disk and nothing is published.
Use it to see what a release would look like, or to try a configuration change without touching `CHANGELOG.md`.
It makes the same model calls as `generate` and costs the same.

## `chartula generate`

```console
$ chartula generate [--tag <release-tag>] [--repo <owner/name>]
```

Runs the same pipeline as `preview` and then writes the outputs:

- **`CHANGELOG.md`** - the technical rendering, prepended to whatever is already there.
- **`release-<tag>.md`** - the customer rendering as a standalone page, with YAML front matter (title, date, one-sentence description) ahead of the entries.
- **`changelog.json`** - every audience's text plus the fact base behind it, in the [documented, stable format](changelog-json.md).
- **GitHub release notes** - the technical rendering, as a **draft** release for `<release-tag>` that you publish on GitHub after reading it. A release that already exists for the tag keeps its state: a draft stays a draft, a published release stays published, and only its notes are replaced. The output marks a draft with `(draft)` after its link.

It also keeps a [run record](run-record.md) in `chartula-runs/`: what the run cost and what it was made with, for comparing runs later.
It stays on your machine, never published.

A field with no source behind it is left out rather than filled in - no description when the facts do not support one, no date when the tag has none.

Publishing is the last step, so when GitHub refuses it - most often a token without Contents read and write - the files are already written.
The summary lists them under "Wrote:", names the refusal under "Not published:", and the run exits with 1.
A re-run replaces this release's entries in `CHANGELOG.md` and its draft rather than adding to them, but pays for the model calls again.

### `--no-publish`

```console
$ chartula generate --tag v1.2.0 --repo owner/name --no-publish
```

Writes every file `generate` writes - `CHANGELOG.md`, `release-<tag>.md`, `changelog.json` and the run record - and leaves the GitHub release notes alone.
Use it to keep the record of a run without announcing a release - measuring a prompt change, say, or generating a changelog for a tag that was never shipped.
The run's summary lists this under "Skipped (--no-publish)" so a deliberately unpublished run still reads as complete rather than as a partial failure.

### `--audience <a>`

```console
$ chartula generate --tag v1.2.0 --repo owner/name --audience customer
$ chartula generate --tag v1.2.0 --repo owner/name --audience technical,product
$ chartula generate --tag v1.2.0 --repo owner/name --audience technical --audience customer
```

Renders only the named audiences instead of the default two.
Valid values are `technical`, `customer` and `product`, case-insensitive; a misspelled name fails the run with `Unknown audience '<name>'.` rather than quietly producing nothing.
Repeat the flag or separate values with a comma - both forms are accepted and can be mixed.

Each audience is its own rephrasing call and its own faithfulness check, so a run that asks for one audience pays for one.
An output whose audience was not rendered is not written: no `CHANGELOG.md` without `technical`, no `release-<tag>.md` without `customer`.
The run's summary lists the skipped outputs next to the written ones.

With no `--audience` given, `technical` and `customer` render: the two with somewhere to go, `CHANGELOG.md` and the release notes, and `release-<tag>.md`.
`product` renders only when named, since it has no output file of its own and no evaluation yet, and would otherwise cost a third of every run.
All three are `--audience technical,customer,product`.

## Release and repository

A run starts from a checkout of the repository the release belongs to, because the commit range is read with `git` from the current directory.
That checkout already knows the release and the repository, so both options default from it and only need to be passed to choose something else.

| Option | Value | Default |
| --- | --- | --- |
| `--tag` | `<release-tag>` | The nearest tag reachable from `HEAD` (`git describe --tags --abbrev=0`). |
| `--repo` | `<owner/name>` | Owner and name from the `origin` remote, in the `https://`, `ssh://` or `git@host:` form. |

A value that was not passed is announced on stderr before the run starts, so `generate` never publishes to a release you did not see:

```console
$ cd my-repo
$ chartula preview
Using tag v1.3.0, the nearest tag reachable from HEAD. Pass --tag to choose another.
Using repository owner/name, from the 'origin' remote. Pass --repo to choose another.
```

When a default cannot be read, the run fails before any network call and names the option to pass:

```console
$ chartula preview
No --tag given, and no tag is reachable from HEAD in '/work/my-repo'. Pass --tag <release-tag>, or run from a checkout of the repository with its tags fetched (git fetch --tags).

$ chartula preview --repo name-only
Invalid option --repo 'name-only'. Expected <owner/name>.
```

### Where a release starts

A release is the commits after its start, up to the release tag.
It starts after the previous tag, or at the first commit when the tag is the first.
A first tag, or a range with more commits than `range.confirmAboveCommits`, is confirmed before any GitHub request or model call, and a shallow clone stops the run because its history is cut off.

| Option | Value | Effect |
| --- | --- | --- |
| `--since` | `<tag-or-commit>` | The release starts after this ref instead of the previous tag or the first commit. It has to be an ancestor of the release tag. |
| `--yes` | - | Confirms a range that would be asked about, for a run without a terminal, such as a CI job. |

A run without a terminal and without `--yes` stops at that question with nothing spent, so a CI job fails on its first tag until you pass one of the two options.

[What goes into a release](what-goes-into-a-release.md#1-the-range) explains the range, the first tag, the confirmation, a shallow clone, and which changes of the range appear.

## Environment

Three environment variables carry credentials, and none is ever read from `chartula.yaml`:

| Variable | Used for |
| --- | --- |
| `ANTHROPIC_API_KEY` | The model that rephrases the facts, with `llm.provider: anthropic` (the default). |
| `OPENAI_API_KEY` | The model that rephrases the facts, with `llm.provider: openai-compatible`, when the endpoint needs a key. `Chartula__Llm__ApiKeyEnvironmentVariable` names another variable instead. |
| `GITHUB_TOKEN` | Reading pull requests and writing release notes. [GitHub](github.md) covers its permissions and the rate limit. |

A run without `ANTHROPIC_API_KEY` is refused before it reads anything, naming the variable, because every audience would fail on it.
The `openai-compatible` provider is exempt: a local server needs no key.

```console
$ chartula preview
Configuration error: No Anthropic API key found in ANTHROPIC_API_KEY. Set one with: export ANTHROPIC_API_KEY=<your key> (create one at https://console.anthropic.com/settings/keys). For an endpoint that needs no key, set llm.provider to openai-compatible.
```

A run starts without `GITHUB_TOKEN` and prints a warning to stderr rather than refusing, because a small release of a public repository fits GitHub's unauthenticated rate limit.
[GitHub](github.md) says when you need a token, which permissions it takes, and what a run spends of the rate limit.

The variable names above are the defaults; all three can be renamed, in the environment only - see [Environment-only settings](configuration.md#environment-only-settings).

## Configuration file

`chartula.yaml` in the repository root refines the default behavior of both commands - which model is used, which labels affect curation, which categories are excluded, and more.
It is never required; every command above works with no file present.
See [Configuration](configuration.md) for every key and its default.
A file that cannot be read as written - invalid YAML, an unknown or misplaced key, a value of the wrong kind - stops the run with a configuration error naming the file, the line and the column; see [A file that cannot be read as meant](configuration.md#a-file-that-cannot-be-read-as-meant).

## Exit status

`0` when every audience the run asked for rendered.
`1` on a usage error (missing option, unknown command, unknown audience), a run-time failure (bad configuration, a pipeline error), or a run in which any requested audience failed.

An audience that failed does not hold back the ones that rendered: `generate` still writes their outputs and says `<n> of <m> audiences failed.`
When no audience rendered, no output is written, so an earlier run's `changelog.json` is not replaced by one without renderings; only the [run record](run-record.md) is kept, since the tokens were spent.
Errors are written to stderr when they are about the invocation itself, and to stdout as `Error: <message>` when the pipeline started running and then failed.

### When a model call fails

A failed model call names the provider, the address it asked, the model and the status, then what that status usually means, then the endpoint's own message.
Audiences that failed for the same reason say it once:

```console
--- Technical ---
  (failed) Changelog generation for 'v1.2.0' failed: openai-compatible at http://localhost:11434/v1/chat/completions answered 404 Not Found for model 'gpt-luna'.
           Either the endpoint does not serve that model id (check llm.model), or Chartula__Llm__BaseUrl is not the address of this provider's API - another provider's, or a wrong path such as a missing /v1.
           The endpoint said: The model `gpt-luna` does not exist or you do not have access to it.

--- Customer ---
  (failed) The same as Technical.
```

A `401` or `403` names the variable the key was read from, and says so when that variable is not set.
An endpoint that cannot be reached at all is named as configured, with the transport's reason (`Connection refused`, a failed name lookup).

A failed call of the thorough check does not fail the audience: the rendering is kept and flagged as `The thorough check could not be evaluated`, with the same explanation, and names `faithfulness.model` when the check asks a model of its own.

## After a run

Every run - `preview` or `generate` - ends with a report of what it did and what it cost in tokens.
See [Run metrics](run-metrics.md) for how to read it.
`generate` keeps the same report in a [run record](run-record.md) and names the file below it.
