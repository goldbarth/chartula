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
  chartula preview  [options]   Show the facts and what generate would send. Free.
  chartula generate [options]   Produce and write the outputs.
  chartula doctor   [options]   Check the setup a run needs, before a run spends anything.
  chartula --version            Print the version and its commit.

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
`chartula --version` prints the version and the commit it was built from, such as `chartula 0.1.0-preview.3+7620e6d...`, and exits with status 0.
It is the same text a run records as `toolVersion` in `changelog.json`, so a report and a run file name the same build.
An unrecognized first word is not silently ignored - it prints `Unknown command '<word>'.`, the same usage text, and exits with status 1.

Two commands make a changelog: `preview` and `generate`.
Both establish the same facts the same way; `preview` stops there, and `generate` goes on to the model.
A third, `doctor`, checks the setup both need.

## `chartula doctor`

```console
$ chartula doctor [--tag <release-tag>] [--repo <owner/name>]
```

Checks everything a run needs, one line per check, and says whether a run would start:

```console
$ chartula doctor
Checking the setup for a run in /work/my-repo

  ok    git           /usr/bin/git
  ok    checkout      /work/my-repo, with its full history
  ok    tag           v1.3.0, the nearest tag reachable from HEAD. Pass --tag to choose another.
  ok    repository    owner/name, from the 'origin' remote. Pass --repo to choose another.
  ok    config        /work/my-repo/chartula.yaml
  ok    model         openai-compatible at http://localhost:11434/v1, model qwen3:14b-ctx24k, no key in OPENAI_API_KEY: a local server needs none
  ok    endpoint      qwen3:14b-ctx24k answered, 98 tokens
  ok    GitHub read   owner/name at https://api.github.com/, token from GITHUB_TOKEN
  warn  GitHub write  GitHub does not let this run publish the release notes for v1.3.0 to owner/name (403 Forbidden).
                      The request carried the token from GITHUB_TOKEN.
                      Publishing needs a token with Contents read and write on owner/name.
                      generate stops before its first model call. preview and generate --no-publish publish nothing and still run.
  warn  PR titles     24 of the last 30 merged pull requests carry a Conventional Commits prefix, such as feat: or fix:.
                      Without one, a change is Other, and internal work is not filtered out. For example: 'Update readme', 'Bump deps'
                      docs/writing-pull-requests.md shows what a prefix changes in the output.

A run would start. The warnings above do not stop it, but read them before generate.
```

Each check asks what a run asks, through the same code, so a failed check shows the message the run would print.
A check that needs an earlier one that failed shows as `skip`, so a missing `git` does not bury the report under follow-on errors.

- **`ok`** - the check passed.
- **`warn`** - a run starts, but something limits it: no GitHub token, a token that cannot publish, or pull request titles without a prefix.
- **`fail`** - a run would stop there.

`doctor` exits with `0` when nothing failed, and with `1` otherwise, so a CI job can run it before `generate`.
It writes no file and publishes nothing.
It names variables, never their values.

The endpoint check is the one that costs: it asks each model a run uses for an answer of at most 16 tokens, about a hundred with the prompt, because only an answer proves the key, the model id and the endpoint together.
The GitHub checks cost three requests: the pull requests of the tag's commit, the check that the token may publish (the same one `generate` makes, which stores nothing), and the last 30 merged pull requests for the title check.

## `chartula preview`

```console
$ chartula preview [--tag <release-tag>] [--repo <owner/name>]
```

Reads the range and its pull requests, and decides every fact the way `generate` does - curation, filtering, labeling, categorization, visibility.
Then it stops, before the model, and prints what `generate` would render from and what it would send:

```console
$ chartula preview --tag v1.3.0
Preview of v1.3.0 - no model call was made, and nothing was written or published.

Release: 14 commits, 10 pull requests, 9 facts (7 with a description, 22,512 characters)

Facts (9):
  #412     Feature: feat(cli): add --since
           technical, customer
  #415     Documentation: docs: rewrite the install guide
           in no rendering
  ...
Dropped (1):
  #409     chore: bump dependencies
           Internal is in filter.excludeCategories

generate would make 4 model calls:
  technical 1 rephrasing call, 24,310 characters of prompt, then 1 thorough check
  customer  1 rephrasing call, 27,905 characters of prompt, then 1 thorough check
  A thorough check sends the rendering along with the facts, so its size is known only once the rendering is.
```

Each fact names the audiences whose rendering carries it, and each dropped change the setting that dropped it.
The prompt is counted in characters, exactly as it would be sent; how many tokens that is depends on the model's tokenizer.

A preview makes no model call, so it costs no tokens and needs no model key, only the GitHub requests.
Nothing is written to disk and nothing is published.
Use it to check a release, or a change to labels, filters or categories, before a run is paid for.
To see the prose, run `generate --no-publish`: it writes the files and publishes nothing.

## `chartula generate`

```console
$ chartula generate [--tag <release-tag>] [--repo <owner/name>]
```

Establishes the same facts as `preview`, renders each audience with the model, checks the renderings, and writes the outputs:

- **`CHANGELOG.md`** - the technical rendering, prepended to whatever is already there.
- **`release-<tag>.md`** - the customer rendering as a standalone page, with YAML front matter (title, date, one-sentence description) ahead of the entries.
- **`changelog.json`** - every audience's text plus the facts behind it, without the pull request descriptions, in the [documented, stable format](changelog-json.md). It is meant to be published.
- **GitHub release notes** - the technical rendering, as a **draft** release for `<release-tag>` that you publish on GitHub after reading it. A release that already exists for the tag keeps its state: a draft stays a draft, a published release stays published, and only its notes are replaced. The output marks a draft with `(draft)` after its link.

It also keeps a [run record](run-record.md) in `chartula-runs/`: what the run cost, what it was made with, and the complete facts, descriptions included, for comparing runs later.
It stays on your machine, never published.

A field with no source behind it is left out rather than filled in - no description when the facts do not support one, no date when the tag has none.

A token without Contents read and write stops the run before the first model call ([What `generate` writes to GitHub](github.md#what-generate-writes-to-github)).
Publishing is still the last step, so when GitHub refuses it for another reason, the files are already written.
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

A `generate` run without `ANTHROPIC_API_KEY` is refused before it reads anything, naming the variable, because every audience would fail on it.
The `openai-compatible` provider is exempt: a local server needs no key.
`preview` makes no model call and needs no key.
[No model key](troubleshooting.md#no-model-key) shows the message.

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

A failed model call names the provider, the address it asked, the model and the status, then what that status usually means.
[A model call fails](troubleshooting.md#a-model-call-fails) shows the messages and what fixes each.

## While a run works

After the header, a run shows each step as it starts, with its count where it has one and the time it has taken so far:

```console
$ chartula generate --tag v1.3.0
...
Range:  the commits after v1.2.0, up to v1.3.0 (100 commits)
Reading pull requests   100/100 commits  4 s
Rendering technical                      12 s
Rendering customer                       14 s
Checking technical                       3 s
Checking customer                        5 s
```

In a terminal the current line updates in place, and its time keeps counting while a model call runs.
So a slow step still shows it is working, and a run that is about to finish is not aborted and paid for again.
The steps are the ones the run takes: one GitHub request per commit, then one rendering per audience, then the checks of each rendering; a `preview` shows the first step only.
There is no estimate of the time remaining, because how long a model call takes is not known before it returns.

The steps go to stderr, like the header, so a changelog redirected from stdout stays clean.
Without a terminal, as in CI, each step is one plain line as it starts - `Reading pull requests (100 commits)`, `Rendering technical` - with no control characters, so a job log stays readable.
When a step fails, it is the last line shown, and the error follows below it.

## After a run

Every `generate` run ends with a report of what it did and what it cost in tokens.
See [Costs and checks](costs-and-checks.md#reading-the-run-summary) for how to read it.
A `preview` spends no tokens, so it ends with what `generate` would send instead.
`generate` keeps the same report in a [run record](run-record.md) and names the file below it.
